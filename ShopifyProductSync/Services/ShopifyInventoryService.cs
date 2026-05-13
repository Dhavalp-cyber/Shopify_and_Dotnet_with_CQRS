using System.Text;
using System.Text.Json;

namespace ShopifyProductSync.Services
{
    /// <summary>
    /// Calls the Shopify GraphQL Admin API to:
    ///   1. Fetch the inventory item ID for a product (auto-resolved from product ID).
    ///   2. Update inventory levels for a specific location.
    ///
    /// Uses the same HttpClient ("ShopifyGraphQL") and configuration keys
    /// (Shopify:ShopUrl, Shopify:AccessToken) as the existing ShopifyGraphQLService.
    /// </summary>
    public class ShopifyInventoryService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ShopifyInventoryService> _logger;

        private readonly string _graphqlEndpoint;
        private readonly string _accessToken;

        public ShopifyInventoryService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ShopifyInventoryService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            var shopUrl = configuration["Shopify:ShopUrl"]
                ?? throw new InvalidOperationException("Shopify:ShopUrl is not configured.");

            _accessToken = configuration["Shopify:AccessToken"]
                ?? throw new InvalidOperationException("Shopify:AccessToken is not configured.");

            _graphqlEndpoint = $"https://{shopUrl}/admin/api/2024-01/graphql.json";
        }

        /// <summary>
        /// Fetches the location name from Shopify for a given location ID.
        /// Uses the Shopify REST Admin API (simpler and more reliable than GraphQL for locations).
        /// Returns "Unknown Location" only if Shopify genuinely cannot return the name.
        /// </summary>
        public async Task<string> GetLocationNameAsync(long shopifyLocationId)
        {
            _logger.LogInformation("Fetching location name for LocationId: {LocationId}", shopifyLocationId);

            try
            {
                var shopUrl = _graphqlEndpoint.Replace("/admin/api/2024-01/graphql.json", "");
                var restEndpoint = $"{shopUrl}/admin/api/2024-01/locations/{shopifyLocationId}.json";

                var client = _httpClientFactory.CreateClient("ShopifyGraphQL");
                var request = new HttpRequestMessage(HttpMethod.Get, restEndpoint);
                request.Headers.Add("X-Shopify-Access-Token", _accessToken);

                var httpResponse = await client.SendAsync(request);
                var responseBody = await httpResponse.Content.ReadAsStringAsync();

                _logger.LogInformation(
                    "Shopify location REST response — Status: {Status}, Body: {Body}",
                    httpResponse.StatusCode, responseBody);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Shopify location REST returned {Status} for LocationId: {LocationId}. Body: {Body}",
                        httpResponse.StatusCode, shopifyLocationId, responseBody);
                    return "Unknown Location";
                }

                // REST response: { "location": { "id": 123, "name": "Main Warehouse", ... } }
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                if (root.TryGetProperty("location", out var locationNode) &&
                    locationNode.TryGetProperty("name", out var nameNode))
                {
                    var name = nameNode.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        _logger.LogInformation(
                            "Location name fetched successfully — LocationId: {LocationId}, Name: {Name}",
                            shopifyLocationId, name);
                        return name;
                    }
                }

                _logger.LogWarning(
                    "Location name not found in Shopify response for LocationId: {LocationId}. Response: {Body}",
                    shopifyLocationId, responseBody);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Exception while fetching location name for LocationId: {LocationId}", shopifyLocationId);
            }

            return "Unknown Location";
        }

        /// <summary>
        /// Fetches the inventory item ID for the first variant of a Shopify product.
        /// Shopify ties inventory to variants, not products directly.
        /// Throws if the product or inventory item is not found.
        /// </summary>
        public async Task<long> GetInventoryItemIdAsync(long shopifyProductId)
        {
            _logger.LogInformation(
                "Fetching inventory item ID for ShopifyProductId: {ProductId}", shopifyProductId);

            var productGid = $"gid://shopify/Product/{shopifyProductId}";

            var query = new
            {
                query = $@"{{
                    product(id: ""{productGid}"") {{
                        variants(first: 1) {{
                            edges {{
                                node {{
                                    inventoryItem {{
                                        id
                                    }}
                                }}
                            }}
                        }}
                    }}
                }}"
            };

            var responseBody = await SendGraphQLRequestAsync(JsonSerializer.Serialize(query));

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Navigate: data → product → variants → edges[0] → node → inventoryItem → id
            if (!root.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("product", out var productNode) ||
                productNode.ValueKind == JsonValueKind.Null)
            {
                throw new Exception($"Product with Shopify ID {shopifyProductId} not found in Shopify.");
            }

            var edges = productNode
                .GetProperty("variants")
                .GetProperty("edges");

            if (edges.GetArrayLength() == 0)
                throw new Exception($"No variants found for Shopify product {shopifyProductId}.");

            // GID format: "gid://shopify/InventoryItem/987654321"
            var inventoryItemGid = edges[0]
                .GetProperty("node")
                .GetProperty("inventoryItem")
                .GetProperty("id")
                .GetString() ?? string.Empty;

            var lastSlash = inventoryItemGid.LastIndexOf('/');
            if (lastSlash < 0 || !long.TryParse(inventoryItemGid[(lastSlash + 1)..], out var inventoryItemId))
                throw new Exception($"Could not parse inventory item ID from GID: {inventoryItemGid}");

            _logger.LogInformation(
                "Resolved InventoryItemId: {ItemId} for ShopifyProductId: {ProductId}",
                inventoryItemId, shopifyProductId);

            return inventoryItemId;
        }

        /// <summary>
        /// Sets the absolute inventory quantity for the given inventory item
        /// at the given Shopify location using the inventorySetQuantities GraphQL mutation.
        /// Returns the actual available quantity after the update.
        /// Throws an exception if the Shopify API call fails.
        /// </summary>
        public async Task<int> UpdateInventoryAsync(
            long inventoryItemId,
            long locationId,
            int quantity)
        {
            _logger.LogInformation(
                "Shopify GraphQL mutation started — InventoryItemId: {ItemId}, LocationId: {LocationId}, Quantity: {Qty}",
                inventoryItemId, locationId, quantity);

            var inventoryItemGid = $"gid://shopify/InventoryItem/{inventoryItemId}";
            var locationGid = $"gid://shopify/Location/{locationId}";

            var mutation = new
            {
                query = @"mutation inventorySetQuantities($input: InventorySetQuantitiesInput!) {
                    inventorySetQuantities(input: $input) {
                        inventoryAdjustmentGroup {
                            reason
                            changes {
                                name
                                delta
                                quantityAfterChange
                            }
                        }
                        userErrors {
                            field
                            message
                        }
                    }
                }",
                variables = new
                {
                    input = new
                    {
                        name = "available",
                        reason = "correction",
                        ignoreCompareQuantity = true,
                        quantities = new[]
                        {
                            new
                            {
                                inventoryItemId = inventoryItemGid,
                                locationId = locationGid,
                                quantity
                            }
                        }
                    }
                }
            };

            var responseBody = await SendGraphQLRequestAsync(JsonSerializer.Serialize(mutation));

            // Check for GraphQL-level userErrors (HTTP 200 but logical failure)
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("inventorySetQuantities", out var mutationResult) &&
                mutationResult.TryGetProperty("userErrors", out var userErrors) &&
                userErrors.GetArrayLength() > 0)
            {
                var errorMessages = string.Join("; ",
                    userErrors.EnumerateArray()
                              .Select(e => e.GetProperty("message").GetString()));

                _logger.LogError(
                    "Shopify inventory mutation returned userErrors: {Errors}", errorMessages);

                throw new Exception($"Shopify inventory userErrors: {errorMessages}");
            }

            // Extract quantityAfterChange from the response
            // Navigate: data → inventorySetQuantities → inventoryAdjustmentGroup → changes[0] → quantityAfterChange
            int availableAfterUpdate = quantity; // fallback to requested quantity
            try
            {
                var changes = root
                    .GetProperty("data")
                    .GetProperty("inventorySetQuantities")
                    .GetProperty("inventoryAdjustmentGroup")
                    .GetProperty("changes");

                if (changes.GetArrayLength() > 0)
                {
                    // Find the "available" change entry
                    foreach (var change in changes.EnumerateArray())
                    {
                        if (change.TryGetProperty("name", out var name) &&
                            name.GetString() == "available" &&
                            change.TryGetProperty("quantityAfterChange", out var qtyAfter))
                        {
                            availableAfterUpdate = qtyAfter.GetInt32();
                            break;
                        }
                    }
                }
            }
            catch
            {
                // If parsing fails, fall back to the requested quantity
                availableAfterUpdate = quantity;
            }

            _logger.LogInformation(
                "Shopify inventory update success — InventoryItemId: {ItemId}, LocationId: {LocationId}, " +
                "Quantity: {Qty}, AvailableAfter: {Available}",
                inventoryItemId, locationId, quantity, availableAfterUpdate);

            return availableAfterUpdate;
        }

        /// <summary>
        /// Core HTTP method — sends the raw GraphQL JSON body to Shopify and returns the response string.
        /// Also checks for top-level GraphQL errors (e.g. auth failures) that come back as HTTP 200
        /// but contain an "errors" array instead of a "data" object.
        /// </summary>
        private async Task<string> SendGraphQLRequestAsync(string queryJson)
        {
            var client = _httpClientFactory.CreateClient("ShopifyGraphQL");

            var request = new HttpRequestMessage(HttpMethod.Post, _graphqlEndpoint);
            request.Headers.Add("X-Shopify-Access-Token", _accessToken);
            request.Content = new StringContent(queryJson, Encoding.UTF8, "application/json");

            var httpResponse = await client.SendAsync(request);
            var responseBody = await httpResponse.Content.ReadAsStringAsync();

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Shopify GraphQL returned {StatusCode}: {Body}",
                    httpResponse.StatusCode, responseBody);

                throw new Exception(
                    $"Shopify GraphQL error {httpResponse.StatusCode}: {responseBody}");
            }

            // Shopify sometimes returns HTTP 200 but with a top-level "errors" array
            // e.g. invalid access token, missing scopes, malformed query
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array &&
                errors.GetArrayLength() > 0)
            {
                var errorMessages = string.Join("; ",
                    errors.EnumerateArray()
                          .Select(e => e.TryGetProperty("message", out var msg)
                              ? msg.GetString()
                              : e.GetRawText()));

                _logger.LogError("Shopify GraphQL returned errors: {Errors}", errorMessages);
                throw new Exception($"Shopify GraphQL returned errors: {errorMessages}");
            }

            // Also handle the case where "errors" is a plain string (older Shopify API versions)
            if (doc.RootElement.TryGetProperty("errors", out var errorsStr) &&
                errorsStr.ValueKind == JsonValueKind.String)
            {
                var msg = errorsStr.GetString();
                _logger.LogError("Shopify GraphQL returned error: {Error}", msg);
                throw new Exception($"Shopify GraphQL returned error: {msg}");
            }

            return responseBody;
        }
    }
}

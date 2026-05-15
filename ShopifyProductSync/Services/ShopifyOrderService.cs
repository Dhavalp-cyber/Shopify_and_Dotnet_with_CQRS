using System.Text;
using System.Text.Json;
using ShopifyProductSync.DTOs;

namespace ShopifyProductSync.Services
{
    /// <summary>
    /// Fetches Shopify order data using the GraphQL Admin API.
    ///
    /// Responsibilities:
    ///   - Fetch orders that are unfulfilled or partially fulfilled
    ///   - Return all line items per order (both fulfilled and unfulfilled)
    ///   - Determine item-level fulfillment status from fulfillableQuantity
    ///
    /// Uses the same "ShopifyGraphQL" HttpClient and Shopify:ShopUrl / Shopify:AccessToken
    /// configuration keys as the existing ShopifyGraphQLService.
    /// </summary>
    public class ShopifyOrderService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ShopifyOrderService> _logger;

        private readonly string _graphqlEndpoint;
        private readonly string _accessToken;

        // Max orders per page — Shopify GraphQL allows up to 250
        private const int PageSize = 50;

        public ShopifyOrderService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ShopifyOrderService> logger)
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
        /// Fetches all Shopify orders where fulfillment status is unfulfilled or partial.
        /// Returns ALL line items per order — including already fulfilled items.
        /// Each item gets its own fulfillment status based on fulfillableQuantity.
        /// </summary>
        public async Task<List<UnfulfilledOrderDto>> GetUnfulfilledOrdersAsync()
        {
            _logger.LogInformation("Fetching unfulfilled/partial orders from Shopify GraphQL...");

            var allOrders = new List<UnfulfilledOrderDto>();
            string? cursor = null;
            bool hasNextPage = true;
            int pageNumber = 1;

            while (hasNextPage)
            {
                _logger.LogInformation(
                    "Fetching unfulfilled orders page {Page} from Shopify GraphQL...", pageNumber);

                var queryJson = BuildOrdersQuery(cursor);
                var responseBody = await SendGraphQLRequestAsync(queryJson);

                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                // Check for top-level GraphQL errors
                if (root.TryGetProperty("errors", out var errors))
                {
                    var errMsg = errors.ValueKind == JsonValueKind.Array
                        ? string.Join("; ", errors.EnumerateArray()
                            .Select(e => e.TryGetProperty("message", out var m) ? m.GetString() : e.GetRawText()))
                        : errors.GetString();

                    _logger.LogError("Shopify GraphQL errors: {Errors}", errMsg);
                    throw new Exception($"Shopify GraphQL errors: {errMsg}");
                }

                if (!root.TryGetProperty("data", out var data) ||
                    !data.TryGetProperty("orders", out var ordersNode))
                {
                    _logger.LogWarning("No orders data in Shopify GraphQL response on page {Page}.", pageNumber);
                    break;
                }

                var edges = ordersNode.GetProperty("edges");

                foreach (var edge in edges.EnumerateArray())
                {
                    if (!edge.TryGetProperty("node", out var node)) continue;

                    var orderDto = MapOrderNode(node);

                    // Safety filter — only include unfulfilled or partially fulfilled orders
                    // Shopify query already filters, but we double-check here
                    var status = orderDto.FulfillmentStatus.ToUpperInvariant();
                    if (status == "FULFILLED") continue;

                    allOrders.Add(orderDto);
                }

                _logger.LogInformation(
                    "Page {Page}: fetched orders. Running total: {Total}", pageNumber, allOrders.Count);

                // Pagination
                var pageInfo = ordersNode.GetProperty("pageInfo");
                hasNextPage = pageInfo.GetProperty("hasNextPage").GetBoolean();
                cursor = pageInfo.TryGetProperty("endCursor", out var endCursor)
                    ? endCursor.GetString()
                    : null;

                pageNumber++;
            }

            _logger.LogInformation(
                "Unfulfilled orders fetch complete. Total: {Total}", allOrders.Count);

            return allOrders;
        }

        /// <summary>
        /// Builds the GraphQL query to fetch unfulfilled/partial orders with all line items.
        /// Uses cursor-based pagination.
        /// </summary>
        private string BuildOrdersQuery(string? cursor)
        {
            // after argument for pagination — only added from page 2 onwards
            var afterArg = cursor != null ? $", after: \\\"{cursor}\\\"" : "";

            // Query filters: fulfillment_status:unfulfilled OR fulfillment_status:partial
            // Shopify GraphQL query syntax uses OR between filter values
            return JsonSerializer.Serialize(new
            {
                query = $@"{{
                    orders(
                        first: {PageSize}{afterArg},
                        query: ""fulfillment_status:unfulfilled OR fulfillment_status:partial""
                    ) {{
                        edges {{
                            node {{
                                id
                                name
                                displayFulfillmentStatus
                                displayFinancialStatus
                                createdAt
                                totalPriceSet {{
                                    shopMoney {{
                                        amount
                                        currencyCode
                                    }}
                                }}
                                customer {{
                                    firstName
                                    lastName
                                    email
                                }}
                                lineItems(first: 50) {{
                                    edges {{
                                        node {{
                                            id
                                            name
                                            variantTitle
                                            sku
                                            quantity
                                            fulfillableQuantity
                                            variant {{
                                                id
                                                product {{
                                                    id
                                                }}
                                            }}
                                        }}
                                    }}
                                }}
                            }}
                        }}
                        pageInfo {{
                            hasNextPage
                            endCursor
                        }}
                    }}
                }}"
            });
        }

        /// <summary>
        /// Maps a single GraphQL order node to UnfulfilledOrderDto.
        /// Parses all fields and maps each line item with its fulfillment status.
        /// </summary>
        private UnfulfilledOrderDto MapOrderNode(JsonElement node)
        {
            // Parse numeric order ID from GID: "gid://shopify/Order/123456789"
            var orderGid = node.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            var orderId = ParseNumericId(orderGid);

            // Order name (e.g. "#1009")
            var orderName = node.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";

            // Fulfillment and financial status
            var fulfillmentStatus = node.TryGetProperty("displayFulfillmentStatus", out var fsProp)
                ? fsProp.GetString() ?? ""
                : "";
            var financialStatus = node.TryGetProperty("displayFinancialStatus", out var finProp)
                ? finProp.GetString() ?? ""
                : "";

            // Created at
            var createdAt = node.TryGetProperty("createdAt", out var caProp)
                ? caProp.GetString() ?? ""
                : "";

            // Total price and currency
            var totalPrice = "";
            var currency = "";
            if (node.TryGetProperty("totalPriceSet", out var priceSet) &&
                priceSet.TryGetProperty("shopMoney", out var shopMoney))
            {
                totalPrice = shopMoney.TryGetProperty("amount", out var amt) ? amt.GetString() ?? "" : "";
                currency = shopMoney.TryGetProperty("currencyCode", out var cur) ? cur.GetString() ?? "" : "";
            }

            // Customer name and email
            var customerName = "";
            var email = "";
            if (node.TryGetProperty("customer", out var customer) &&
                customer.ValueKind != JsonValueKind.Null)
            {
                var firstName = customer.TryGetProperty("firstName", out var fn) ? fn.GetString() ?? "" : "";
                var lastName = customer.TryGetProperty("lastName", out var ln) ? ln.GetString() ?? "" : "";
                customerName = $"{firstName} {lastName}".Trim();
                email = customer.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "";
            }

            // Line items
            var items = new List<OrderLineItemDto>();
            if (node.TryGetProperty("lineItems", out var lineItems) &&
                lineItems.TryGetProperty("edges", out var lineEdges))
            {
                foreach (var lineEdge in lineEdges.EnumerateArray())
                {
                    if (!lineEdge.TryGetProperty("node", out var lineNode)) continue;

                    var item = MapLineItemNode(lineNode);
                    items.Add(item);
                }
            }

            return new UnfulfilledOrderDto
            {
                OrderId = orderId,
                OrderName = orderName,
                CustomerName = customerName,
                Email = email,
                FinancialStatus = financialStatus,
                FulfillmentStatus = fulfillmentStatus,
                CreatedAt = createdAt,
                TotalPrice = totalPrice,
                Currency = currency,
                Items = items
            };
        }

        /// <summary>
        /// Maps a single GraphQL line item node to OrderLineItemDto.
        /// Determines item-level fulfillment status from fulfillableQuantity:
        ///   fulfillableQuantity == 0 → FULFILLED / "Already fulfilled"
        ///   fulfillableQuantity  > 0 → UNFULFILLED / "Ready to fulfill"
        /// </summary>
        private OrderLineItemDto MapLineItemNode(JsonElement lineNode)
        {
            // Line item ID
            var lineGid = lineNode.TryGetProperty("id", out var lid) ? lid.GetString() ?? "" : "";
            var lineItemId = ParseNumericId(lineGid);

            // Product and variant IDs
            long productId = 0;
            long variantId = 0;
            if (lineNode.TryGetProperty("variant", out var variant) &&
                variant.ValueKind != JsonValueKind.Null)
            {
                var variantGid = variant.TryGetProperty("id", out var vid) ? vid.GetString() ?? "" : "";
                variantId = ParseNumericId(variantGid);

                if (variant.TryGetProperty("product", out var product) &&
                    product.ValueKind != JsonValueKind.Null)
                {
                    var productGid = product.TryGetProperty("id", out var pid) ? pid.GetString() ?? "" : "";
                    productId = ParseNumericId(productGid);
                }
            }

            var productName = lineNode.TryGetProperty("name", out var pn) ? pn.GetString() ?? "" : "";
            var variantTitle = lineNode.TryGetProperty("variantTitle", out var vt) ? vt.GetString() ?? "" : "";
            var sku = lineNode.TryGetProperty("sku", out var skuProp) ? skuProp.GetString() ?? "" : "";
            var quantity = lineNode.TryGetProperty("quantity", out var qty) ? qty.GetInt32() : 0;
            var fulfillableQty = lineNode.TryGetProperty("fulfillableQuantity", out var fq) ? fq.GetInt32() : 0;

            // Determine item-level fulfillment status
            // fulfillableQuantity == 0 means all units of this item are already fulfilled
            // fulfillableQuantity  > 0 means some or all units are still pending
            var itemFulfillmentStatus = fulfillableQty == 0 ? "FULFILLED" : "UNFULFILLED";
            var message = fulfillableQty == 0 ? "Already fulfilled" : "Ready to fulfill";

            return new OrderLineItemDto
            {
                LineItemId = lineItemId,
                ProductId = productId,
                VariantId = variantId,
                ProductName = productName,
                VariantTitle = variantTitle ?? "",
                Sku = sku,
                Quantity = quantity,
                FulfillableQuantity = fulfillableQty,
                FulfillmentStatus = itemFulfillmentStatus,
                Message = message
            };
        }

        /// <summary>
        /// Parses the numeric ID from a Shopify GID string.
        /// Example: "gid://shopify/Order/123456789" → 123456789
        /// </summary>
        private static long ParseNumericId(string gid)
        {
            if (string.IsNullOrEmpty(gid)) return 0;
            var lastSlash = gid.LastIndexOf('/');
            if (lastSlash < 0) return 0;
            long.TryParse(gid[(lastSlash + 1)..], out var id);
            return id;
        }

        /// <summary>
        /// Sends a GraphQL request to Shopify and returns the raw response body string.
        /// Uses the named "ShopifyGraphQL" HttpClient registered in Program.cs.
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

            return responseBody;
        }
    }
}

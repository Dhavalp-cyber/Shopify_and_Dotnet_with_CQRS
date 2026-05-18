using ShopifySharp;
using ShopifyProductSync.Configuration;
using ShopifyProductSync.DTOs;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace ShopifyProductSync.Services
{
    /// <summary>
    /// Handles all Shopify fulfillment operations.
    ///
    /// Uses ShopifySharp for:
    ///   - Fetching order details (OrderService)
    ///   - Fetching fulfillment orders (FulfillmentOrderService)
    ///
    /// Uses Shopify GraphQL API for:
    ///   - Creating the actual fulfillment (fulfillmentCreate mutation)
    ///   - Reason: ShopifySharp v6.9.0 does not support the latest
    ///     fulfillment creation flow that requires FulfillmentOrderId.
    /// </summary>
    public class ShopifyFulfillmentService
    {
        private readonly ShopifySettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ShopifyFulfillmentService> _logger;

        private readonly string _graphqlEndpoint;

        public ShopifyFulfillmentService(
            IOptions<ShopifySettings> settings,
            IHttpClientFactory httpClientFactory,
            ILogger<ShopifyFulfillmentService> logger)
        {
            _settings = settings.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            _graphqlEndpoint = $"https://{_settings.ShopUrl}/admin/api/{_settings.ApiVersion}/graphql.json";
        }

        /// <summary>
        /// Fetches a Shopify order by its numeric ID using ShopifySharp.
        /// Returns null if the order is not found.
        /// </summary>
        public async Task<Order?> GetOrderAsync(long orderId)
        {
            _logger.LogInformation("Fetching Shopify order — OrderId: {OrderId}", orderId);

            try
            {
                var orderService = new OrderService(_settings.ShopUrl, _settings.AccessToken);
                var order = await orderService.GetAsync(orderId);
                return order;
            }
            catch (ShopifyException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Order not found in Shopify — OrderId: {OrderId}", orderId);
                return null;
            }
        }

        /// <summary>
        /// Fetches all fulfillment orders for a given Shopify order ID.
        /// Returns only the OPEN (unfulfilled) fulfillment order.
        /// Returns null if no open fulfillment order exists.
        ///
        /// Shopify Concept:
        ///   OrderId → FulfillmentOrder(s) → Fulfillment
        ///   A single order can have multiple fulfillment orders (e.g. different locations).
        ///   We need the FulfillmentOrderId to create a fulfillment.
        /// </summary>
        public async Task<FulfillmentOrder?> GetOpenFulfillmentOrderAsync(long orderId)
        {
            _logger.LogInformation(
                "Fetching fulfillment orders for OrderId: {OrderId}", orderId);

            var fulfillmentOrderService = new FulfillmentOrderService(_settings.ShopUrl, _settings.AccessToken);
            var fulfillmentOrders = await fulfillmentOrderService.ListAsync(orderId);

            // Find the first OPEN fulfillment order
            // "open" status means it has not been fulfilled yet
            var openFulfillmentOrder = fulfillmentOrders
                .FirstOrDefault(fo => fo.Status?.Equals("open", StringComparison.OrdinalIgnoreCase) == true);

            if (openFulfillmentOrder == null)
            {
                _logger.LogWarning(
                    "No open fulfillment order found for OrderId: {OrderId}", orderId);
            }
            else
            {
                _logger.LogInformation(
                    "Found open fulfillment order — FulfillmentOrderId: {FulfillmentOrderId}",
                    openFulfillmentOrder.Id);
            }

            return openFulfillmentOrder;
        }

        /// <summary>
        /// Fetches ALL orders from Shopify via GraphQL with automatic cursor-based pagination.
        /// Returns every order regardless of fulfillment or financial status.
        /// </summary>
        public async Task<List<OrderSummaryDto>> GetAllOrdersAsync()
        {
            var allOrders = new List<OrderSummaryDto>();
            string? cursor = null;
            bool hasNextPage = true;
            int pageNumber = 1;

            _logger.LogInformation("Starting GraphQL all-orders fetch from Shopify...");

            while (hasNextPage)
            {
                _logger.LogInformation(
                    "Fetching all orders page {Page} from Shopify GraphQL...", pageNumber);

                var queryBody = BuildAllOrdersQuery(cursor);
                var responseBody = await SendGraphQLRequestAsync(queryBody);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var response = JsonSerializer.Deserialize<ShopifyOrdersGraphQLResponse>(responseBody, options);

                if (response?.Data?.Orders == null)
                {
                    _logger.LogWarning(
                        "Empty or null orders response from Shopify GraphQL on page {Page}.", pageNumber);
                    break;
                }

                var edges = response.Data.Orders.Edges;

                foreach (var edge in edges)
                {
                    if (edge.Node == null) continue;
                    allOrders.Add(MapToOrderSummaryDto(edge.Node));
                }

                _logger.LogInformation(
                    "Page {Page}: fetched {Count} orders. Total so far: {Total}",
                    pageNumber, edges.Count, allOrders.Count);

                var pageInfo = response.Data.Orders.PageInfo;
                hasNextPage = pageInfo?.HasNextPage ?? false;
                cursor = pageInfo?.EndCursor;
                pageNumber++;
            }

            _logger.LogInformation(
                "GraphQL all-orders fetch complete. Total orders: {Total}", allOrders.Count);

            return allOrders;
        }

        /// <summary>
        /// Builds the GraphQL query JSON for fetching ALL orders (no status filter).
        /// Uses cursor for pagination — pass null for the first page.
        /// </summary>
        private static string BuildAllOrdersQuery(string? cursor)
        {
            var afterArg = cursor != null ? $", after: \\\"{cursor}\\\"" : "";

            // 50 orders per page, up to 50 line items and 10 fulfillment orders per order.
            return $$"""
            {
                "query": "{ orders(first: 50{{afterArg}}) { edges { cursor node { id name email displayFinancialStatus displayFulfillmentStatus createdAt totalPriceSet { shopMoney { amount currencyCode } } customer { firstName lastName email } lineItems(first: 50) { edges { node { id title variantTitle sku quantity fulfillableQuantity variant { id product { id } } } } } fulfillmentOrders(first: 10) { edges { node { id lineItems(first: 50) { edges { node { id lineItem { id } } } } } } } } } pageInfo { hasNextPage endCursor } } }"
            }
            """;
        }

        /// <summary>
        /// Fetches all UNFULFILLED and PARTIALLY_FULFILLED orders from Shopify via GraphQL.
        /// FULFILLED orders are excluded.
        /// For PARTIALLY_FULFILLED orders, ALL line items are returned (both fulfilled and pending).
        /// Uses cursor-based pagination to retrieve every matching order.
        /// </summary>
        public async Task<UnfulfilledOrdersResponseDto> GetUnfulfilledOrdersAsync()
        {
            var allOrders = new List<OrderSummaryDto>();
            string? cursor = null;
            bool hasNextPage = true;
            int pageNumber = 1;

            _logger.LogInformation("Starting GraphQL unfulfilled orders fetch from Shopify...");

            while (hasNextPage)
            {
                _logger.LogInformation(
                    "Fetching unfulfilled orders page {Page} from Shopify GraphQL...", pageNumber);

                var queryBody = BuildUnfulfilledOrdersQuery(cursor);
                var responseBody = await SendGraphQLRequestAsync(queryBody);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var response = JsonSerializer.Deserialize<ShopifyOrdersGraphQLResponse>(responseBody, options);

                if (response?.Data?.Orders == null)
                {
                    _logger.LogWarning(
                        "Empty or null orders response from Shopify GraphQL on page {Page}.", pageNumber);
                    break;
                }

                var edges = response.Data.Orders.Edges;

                foreach (var edge in edges)
                {
                    if (edge.Node == null) continue;

                    var node = edge.Node;

                    // Filter: only UNFULFILLED and PARTIALLY_FULFILLED
                    // Shopify GraphQL filter already excludes FULFILLED via the query,
                    // but we double-check here for safety.
                    var status = node.FulfillmentStatus?.ToUpperInvariant() ?? string.Empty;
                    if (status == "FULFILLED")
                        continue;

                    var orderDto = MapToOrderSummaryDto(node);
                    allOrders.Add(orderDto);
                }

                _logger.LogInformation(
                    "Page {Page}: fetched {Count} orders. Total so far: {Total}",
                    pageNumber, edges.Count, allOrders.Count);

                var pageInfo = response.Data.Orders.PageInfo;
                hasNextPage = pageInfo?.HasNextPage ?? false;
                cursor = pageInfo?.EndCursor;
                pageNumber++;
            }

            _logger.LogInformation(
                "GraphQL unfulfilled orders fetch complete. Total orders: {Total}", allOrders.Count);

            return new UnfulfilledOrdersResponseDto
            {
                TotalOrders = allOrders.Count,
                Orders = allOrders
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers for GetUnfulfilledOrdersAsync
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the GraphQL query JSON for fetching unfulfilled/partially-fulfilled orders.
        /// Uses Shopify's built-in filter: fulfillment_status:unshipped OR partial
        /// to avoid fetching fully-fulfilled orders from the API.
        /// </summary>
        private static string BuildUnfulfilledOrdersQuery(string? cursor)
        {
            // Shopify filter values:
            //   unshipped  → UNFULFILLED
            //   partial    → PARTIALLY_FULFILLED
            // We combine them with OR so a single query covers both statuses.
            const string filter = "fulfillment_status:unshipped OR fulfillment_status:partial";
            var afterArg = cursor != null ? $", after: \\\"{cursor}\\\"" : "";

            // We request 50 orders per page and up to 50 line items per order.
            // Adjust page sizes here if needed without touching any other file.
            return $$"""
            {
                "query": "{ orders(first: 50{{afterArg}}, query: \"{{filter}}\") { edges { cursor node { id name email displayFinancialStatus displayFulfillmentStatus createdAt totalPriceSet { shopMoney { amount currencyCode } } customer { firstName lastName email } lineItems(first: 50) { edges { node { id title variantTitle sku quantity fulfillableQuantity variant { id product { id } } } } } fulfillmentOrders(first: 10) { edges { node { id lineItems(first: 50) { edges { node { id lineItem { id } } } } } } } } } pageInfo { hasNextPage endCursor } } }"
            }
            """;
        }

        /// <summary>
        /// Maps a Shopify GraphQL order node to the clean OrderSummaryDto.
        /// Parses numeric IDs from GID strings and derives item fulfillment status
        /// from fulfillableQuantity.
        /// Resolves fulfillmentOrderId and fulfillmentOrderLineItemId by building
        /// a lookup from the order's fulfillmentOrders connection.
        /// </summary>
        private static OrderSummaryDto MapToOrderSummaryDto(ShopifyOrderNode node)
        {
            var orderId = ParseGidToLong(node.Gid);

            // Build customer display name
            var firstName = node.Customer?.FirstName?.Trim() ?? string.Empty;
            var lastName = node.Customer?.LastName?.Trim() ?? string.Empty;
            var customerName = $"{firstName} {lastName}".Trim();
            if (string.IsNullOrEmpty(customerName))
                customerName = "Guest";

            // Prefer order-level email, fall back to customer email
            var email = !string.IsNullOrWhiteSpace(node.Email)
                ? node.Email
                : node.Customer?.Email;

            var totalPrice = node.TotalPriceSet?.ShopMoney?.Amount ?? "0.00";
            var currency = node.TotalPriceSet?.ShopMoney?.CurrencyCode ?? string.Empty;

            // Build a lookup: lineItemGid → (fulfillmentOrderId, fulfillmentOrderLineItemId)
            // A fulfillmentOrder contains fulfillmentOrderLineItems, each of which references
            // the original lineItem via lineItem.id. We use this to match them up.
            var fulfillmentLookup = BuildFulfillmentLookup(node.FulfillmentOrders);

            var items = new List<OrderLineItemDto>();
            foreach (var lineEdge in node.LineItems?.Edges ?? new List<ShopifyLineItemEdge>())
            {
                if (lineEdge.Node == null) continue;

                fulfillmentLookup.TryGetValue(lineEdge.Node.Gid, out var foIds);
                items.Add(MapToLineItemDto(lineEdge.Node, foIds.FulfillmentOrderId, foIds.FulfillmentOrderLineItemId));
            }

            return new OrderSummaryDto
            {
                OrderId = orderId,
                OrderName = node.Name,
                CustomerName = customerName,
                Email = email,
                FinancialStatus = node.FinancialStatus,
                FulfillmentStatus = node.FulfillmentStatus,
                CreatedAt = node.CreatedAt,
                TotalPrice = totalPrice,
                Currency = currency,
                Items = items
            };
        }

        /// <summary>
        /// Builds a dictionary keyed by lineItem GID that maps to its
        /// fulfillmentOrderId and fulfillmentOrderLineItemId.
        /// Iterates all fulfillmentOrders and their lineItems to build the mapping.
        /// </summary>
        private static Dictionary<string, (long FulfillmentOrderId, long FulfillmentOrderLineItemId)>
            BuildFulfillmentLookup(ShopifyFulfillmentOrderConnection? fulfillmentOrders)
        {
            var lookup = new Dictionary<string, (long, long)>(StringComparer.OrdinalIgnoreCase);

            if (fulfillmentOrders == null) return lookup;

            foreach (var foEdge in fulfillmentOrders.Edges)
            {
                if (foEdge.Node == null) continue;

                var fulfillmentOrderId = ParseGidToLong(foEdge.Node.Gid);

                foreach (var foliEdge in foEdge.Node.LineItems?.Edges ?? new List<ShopifyFulfillmentOrderLineItemEdge>())
                {
                    if (foliEdge.Node == null) continue;

                    var fulfillmentOrderLineItemId = ParseGidToLong(foliEdge.Node.Gid);
                    var lineItemGid = foliEdge.Node.LineItem?.Gid ?? string.Empty;

                    if (!string.IsNullOrEmpty(lineItemGid))
                        lookup[lineItemGid] = (fulfillmentOrderId, fulfillmentOrderLineItemId);
                }
            }

            return lookup;
        }

        /// <summary>
        /// Maps a single Shopify line item node to OrderLineItemDto.
        /// Derives fulfillmentStatus and message from fulfillableQuantity:
        ///   fulfillableQuantity == 0 → "FULFILLED" / "Already fulfilled"
        ///   fulfillableQuantity  > 0 → "UNFULFILLED" / "Ready to fulfill"
        /// </summary>
        private static OrderLineItemDto MapToLineItemDto(
            ShopifyLineItemNode node,
            long fulfillmentOrderId,
            long fulfillmentOrderLineItemId)
        {
            var lineItemId = ParseGidToLong(node.Gid);
            var variantId = ParseGidToLong(node.Variant?.Gid ?? string.Empty);
            var productId = ParseGidToLong(node.Variant?.Product?.Gid ?? string.Empty);

            var isFulfilled = node.FulfillableQuantity == 0;

            return new OrderLineItemDto
            {
                LineItemId = lineItemId,
                ProductId = productId,
                VariantId = variantId,
                ProductName = node.Title,
                VariantTitle = node.VariantTitle,
                Sku = node.Sku,
                Quantity = node.Quantity,
                FulfillableQuantity = node.FulfillableQuantity,
                FulfillmentOrderId = fulfillmentOrderId,
                FulfillmentOrderLineItemId = fulfillmentOrderLineItemId,
                FulfillmentStatus = isFulfilled ? "FULFILLED" : "UNFULFILLED",
                Message = isFulfilled ? "Already fulfilled" : "Ready to fulfill"
            };
        }

        /// <summary>
        /// Parses the numeric ID from a Shopify GID string.
        /// e.g. "gid://shopify/Order/6726812303404" → 6726812303404
        /// Returns 0 if parsing fails.
        /// </summary>
        private static long ParseGidToLong(string gid)
        {
            if (string.IsNullOrEmpty(gid)) return 0;
            var lastSlash = gid.LastIndexOf('/');
            if (lastSlash < 0) return 0;
            long.TryParse(gid[(lastSlash + 1)..], out var id);
            return id;
        }

        /// <summary>
        /// Updates the note and note_attributes on a Shopify order using the
        /// GraphQL orderUpdate mutation.
        /// Sends the complete merged list of note_attributes (Shopify replaces, not appends).
        /// Throws InvalidOperationException if Shopify returns userErrors.
        /// </summary>
        public async Task UpdateOrderNoteAsync(
            long shopifyOrderId,
            string? note,
            List<(string Name, string Value)> noteAttributes)
        {
            _logger.LogInformation(
                "Updating order note via GraphQL — ShopifyOrderId: {OrderId}, " +
                "AttributeCount: {Count}",
                shopifyOrderId, noteAttributes.Count);

            var orderGid = $"gid://shopify/Order/{shopifyOrderId}";

            // Build the note_attributes array for the mutation variables
            var attributeObjects = noteAttributes
                .Select(a => new { key = a.Name, value = a.Value })
                .ToArray();

            var mutation = new
            {
                query = @"mutation orderUpdate($input: OrderInput!) {
                    orderUpdate(input: $input) {
                        order {
                            id
                            note
                            customAttributes {
                                key
                                value
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
                        id = orderGid,
                        note = note ?? string.Empty,
                        customAttributes = attributeObjects
                    }
                }
            };

            var responseBody = await SendGraphQLRequestAsync(JsonSerializer.Serialize(mutation));

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Check for mutation-level userErrors
            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("orderUpdate", out var orderUpdate) &&
                orderUpdate.TryGetProperty("userErrors", out var userErrors) &&
                userErrors.GetArrayLength() > 0)
            {
                var errorMessages = string.Join("; ",
                    userErrors.EnumerateArray()
                              .Select(e => e.GetProperty("message").GetString()));

                _logger.LogError(
                    "Shopify orderUpdate userErrors — ShopifyOrderId: {OrderId}: {Errors}",
                    shopifyOrderId, errorMessages);

                throw new InvalidOperationException(
                    $"Shopify orderUpdate failed: {errorMessages}");
            }

            _logger.LogInformation(
                "Order note updated successfully in Shopify — ShopifyOrderId: {OrderId}",
                shopifyOrderId);
        }

        /// <summary>
        /// Fulfills ONE specific line item from a Shopify order using the GraphQL
        /// fulfillmentCreate mutation with lineItemsByFulfillmentOrder.
        ///
        /// This targets exactly one FulfillmentOrderLineItem — it does NOT fulfill
        /// the entire order or all items in the fulfillment order.
        ///
        /// Returns the numeric fulfillment ID on success.
        /// Throws an exception if Shopify returns errors or userErrors.
        /// </summary>
        public async Task<long> FulfillSingleItemAsync(
            long fulfillmentOrderId,
            long fulfillmentOrderLineItemId,
            int quantity,
            string trackingNumber,
            string shippingCarrierName,
            bool notifyCustomer)
        {
            _logger.LogInformation(
                "FulfillSingleItem — FulfillmentOrderId: {FoId}, " +
                "FulfillmentOrderLineItemId: {FoliId}, Quantity: {Qty}, " +
                "Carrier: {Carrier}, TrackingNumber: {Tracking}",
                fulfillmentOrderId, fulfillmentOrderLineItemId,
                quantity, shippingCarrierName, trackingNumber);

            var fulfillmentOrderGid = $"gid://shopify/FulfillmentOrder/{fulfillmentOrderId}";
            var fulfillmentOrderLineItemGid = $"gid://shopify/FulfillmentOrderLineItem/{fulfillmentOrderLineItemId}";

            // GraphQL fulfillmentCreate mutation targeting a single line item.
            // lineItemsByFulfillmentOrder with fulfillmentOrderLineItems scopes
            // the fulfillment to exactly the specified item + quantity.
            var mutation = new
            {
                query = @"mutation fulfillmentCreate($fulfillment: FulfillmentInput!) {
                    fulfillmentCreate(fulfillment: $fulfillment) {
                        fulfillment {
                            id
                            status
                            trackingInfo {
                                number
                                company
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
                    fulfillment = new
                    {
                        notifyCustomer,
                        trackingInfo = new
                        {
                            number = trackingNumber,
                            company = shippingCarrierName
                        },
                        lineItemsByFulfillmentOrder = new[]
                        {
                            new
                            {
                                fulfillmentOrderId = fulfillmentOrderGid,
                                fulfillmentOrderLineItems = new[]
                                {
                                    new
                                    {
                                        id = fulfillmentOrderLineItemGid,
                                        quantity
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var responseBody = await SendGraphQLRequestAsync(JsonSerializer.Serialize(mutation));

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Check for mutation-level userErrors (HTTP 200 but logical failure)
            // e.g. item already fulfilled, invalid carrier, quantity exceeds fulfillable
            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("fulfillmentCreate", out var fulfillmentCreate) &&
                fulfillmentCreate.TryGetProperty("userErrors", out var userErrors) &&
                userErrors.GetArrayLength() > 0)
            {
                var errorMessages = string.Join("; ",
                    userErrors.EnumerateArray()
                              .Select(e => e.GetProperty("message").GetString()));

                _logger.LogError(
                    "Shopify fulfillmentCreate userErrors — FulfillmentOrderId: {FoId}: {Errors}",
                    fulfillmentOrderId, errorMessages);

                throw new InvalidOperationException(
                    $"Shopify fulfillmentCreate failed: {errorMessages}");
            }

            // Extract the fulfillment GID and parse numeric ID
            // GID format: "gid://shopify/Fulfillment/123456789"
            var fulfillmentGid = root
                .GetProperty("data")
                .GetProperty("fulfillmentCreate")
                .GetProperty("fulfillment")
                .GetProperty("id")
                .GetString() ?? string.Empty;

            var fulfillmentId = ParseGidToLong(fulfillmentGid);

            _logger.LogInformation(
                "Single item fulfilled successfully — FulfillmentId: {FulfillmentId}, " +
                "FulfillmentOrderLineItemId: {FoliId}",
                fulfillmentId, fulfillmentOrderLineItemId);

            return fulfillmentId;
        }

        /// <summary>
        /// Creates a fulfillment in Shopify using the GraphQL fulfillmentCreate mutation.
        ///
        /// Why GraphQL instead of ShopifySharp REST?
        ///   ShopifySharp v6.9.0 uses the legacy REST fulfillment endpoint which does not
        ///   support the FulfillmentOrderId-based flow required by Shopify API 2022-07+.
        ///   The GraphQL fulfillmentCreate mutation is the current recommended approach.
        ///
        /// Returns the fulfillment GID (e.g. "gid://shopify/Fulfillment/123456789").
        /// </summary>
        public async Task<string> CreateFulfillmentAsync(
            long fulfillmentOrderId,
            string trackingNumber,
            string shippingCarrierName,
            bool notifyCustomer)
        {
            _logger.LogInformation(
                "Creating fulfillment via GraphQL — FulfillmentOrderId: {FulfillmentOrderId}, " +
                "TrackingNumber: {TrackingNumber}, Carrier: {Carrier}",
                fulfillmentOrderId, trackingNumber, shippingCarrierName);

            var fulfillmentOrderGid = $"gid://shopify/FulfillmentOrder/{fulfillmentOrderId}";

            // GraphQL fulfillmentCreate mutation
            // This is the current Shopify-recommended way to create fulfillments
            var mutation = new
            {
                query = @"mutation fulfillmentCreate($fulfillment: FulfillmentInput!) {
                    fulfillmentCreate(fulfillment: $fulfillment) {
                        fulfillment {
                            id
                            status
                            trackingInfo {
                                number
                                company
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
                    fulfillment = new
                    {
                        notifyCustomer,
                        trackingInfo = new
                        {
                            number = trackingNumber,
                            company = shippingCarrierName
                        },
                        lineItemsByFulfillmentOrder = new[]
                        {
                            new { fulfillmentOrderId = fulfillmentOrderGid }
                        }
                    }
                }
            };

            var jsonBody = JsonSerializer.Serialize(mutation);

            var client = _httpClientFactory.CreateClient("ShopifyGraphQL");
            var request = new HttpRequestMessage(HttpMethod.Post, _graphqlEndpoint);
            request.Headers.Add("X-Shopify-Access-Token", _settings.AccessToken);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var httpResponse = await client.SendAsync(request);
            var responseBody = await httpResponse.Content.ReadAsStringAsync();

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Shopify GraphQL fulfillmentCreate failed — Status: {Status}",
                    httpResponse.StatusCode);
                throw new Exception(
                    $"Shopify GraphQL fulfillmentCreate failed ({httpResponse.StatusCode}).");
            }

            // Parse the response
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Check for top-level GraphQL errors
            if (root.TryGetProperty("errors", out var topErrors))
            {
                var errMsg = topErrors.ValueKind == JsonValueKind.Array
                    ? string.Join("; ", topErrors.EnumerateArray()
                        .Select(e => e.TryGetProperty("message", out var m) ? m.GetString() : e.GetRawText()))
                    : topErrors.GetString();

                _logger.LogError("Shopify GraphQL errors: {Errors}", errMsg);
                throw new Exception($"Shopify GraphQL errors: {errMsg}");
            }

            // Check for mutation-level userErrors
            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("fulfillmentCreate", out var fulfillmentCreate) &&
                fulfillmentCreate.TryGetProperty("userErrors", out var userErrors) &&
                userErrors.GetArrayLength() > 0)
            {
                var errorMessages = string.Join("; ",
                    userErrors.EnumerateArray()
                              .Select(e => e.GetProperty("message").GetString()));

                _logger.LogError("Shopify fulfillmentCreate userErrors: {Errors}", errorMessages);
                throw new Exception($"Shopify fulfillmentCreate userErrors: {errorMessages}");
            }

            // Extract the fulfillment GID
            var fulfillmentGid = root
                .GetProperty("data")
                .GetProperty("fulfillmentCreate")
                .GetProperty("fulfillment")
                .GetProperty("id")
                .GetString() ?? string.Empty;

            // Parse numeric ID from "gid://shopify/Fulfillment/123456789"
            var lastSlash = fulfillmentGid.LastIndexOf('/');
            var fulfillmentId = lastSlash >= 0
                ? fulfillmentGid[(lastSlash + 1)..]
                : fulfillmentGid;

            _logger.LogInformation(
                "Fulfillment created successfully — FulfillmentId: {FulfillmentId}",
                fulfillmentId);

            return fulfillmentId;
        }

        /// <summary>
        /// Core HTTP method — sends the raw GraphQL JSON body to Shopify and returns the response string.
        /// Throws on non-2xx HTTP status or top-level GraphQL errors.
        /// </summary>
        private async Task<string> SendGraphQLRequestAsync(string queryJson)
        {
            var client = _httpClientFactory.CreateClient("ShopifyGraphQL");

            var request = new HttpRequestMessage(HttpMethod.Post, _graphqlEndpoint);
            request.Headers.Add("X-Shopify-Access-Token", _settings.AccessToken);
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

            // Shopify sometimes returns HTTP 200 with a top-level "errors" array
            // (e.g. invalid access token, missing scopes, malformed query)
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

            return responseBody;
        }
    }
}

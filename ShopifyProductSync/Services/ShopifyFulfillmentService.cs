using ShopifySharp;
using ShopifyProductSync.Configuration;
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
    }
}

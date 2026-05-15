using ShopifySharp;
using ShopifyProductSync.Configuration;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace ShopifyProductSync.Services
{
    /// <summary>
    /// Handles all Shopify fulfillment operations.
    /// Uses ShopifySharp for order/fulfillment order fetch.
    /// Uses Shopify GraphQL for fulfillment creation.
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

        public async Task<Order?> GetOrderAsync(long orderId)
        {
            _logger.LogInformation("Fetching Shopify order — OrderId: {OrderId}", orderId);
            try
            {
                var orderService = new OrderService(_settings.ShopUrl, _settings.AccessToken);
                return await orderService.GetAsync(orderId);
            }
            catch (ShopifyException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Order not found in Shopify — OrderId: {OrderId}", orderId);
                return null;
            }
        }

        public async Task<FulfillmentOrder?> GetOpenFulfillmentOrderAsync(long orderId)
        {
            _logger.LogInformation("Fetching fulfillment orders for OrderId: {OrderId}", orderId);
            var fulfillmentOrderService = new FulfillmentOrderService(_settings.ShopUrl, _settings.AccessToken);
            var fulfillmentOrders = await fulfillmentOrderService.ListAsync(orderId);
            return fulfillmentOrders
                .FirstOrDefault(fo => fo.Status?.Equals("open", StringComparison.OrdinalIgnoreCase) == true);
        }

        public async Task<string> CreateFulfillmentAsync(
            long fulfillmentOrderId,
            string trackingNumber,
            string shippingCarrierName,
            bool notifyCustomer)
        {
            _logger.LogInformation(
                "Creating fulfillment via GraphQL — FulfillmentOrderId: {FulfillmentOrderId}",
                fulfillmentOrderId);

            var fulfillmentOrderGid = $"gid://shopify/FulfillmentOrder/{fulfillmentOrderId}";

            var mutation = new
            {
                query = @"mutation fulfillmentCreate($fulfillment: FulfillmentInput!) {
                    fulfillmentCreate(fulfillment: $fulfillment) {
                        fulfillment { id status }
                        userErrors { field message }
                    }
                }",
                variables = new
                {
                    fulfillment = new
                    {
                        notifyCustomer,
                        trackingInfo = new { number = trackingNumber, company = shippingCarrierName },
                        lineItemsByFulfillmentOrder = new[] { new { fulfillmentOrderId = fulfillmentOrderGid } }
                    }
                }
            };

            var client = _httpClientFactory.CreateClient("ShopifyGraphQL");
            var request = new HttpRequestMessage(HttpMethod.Post, _graphqlEndpoint);
            request.Headers.Add("X-Shopify-Access-Token", _settings.AccessToken);
            request.Content = new StringContent(JsonSerializer.Serialize(mutation), Encoding.UTF8, "application/json");

            var httpResponse = await client.SendAsync(request);
            var responseBody = await httpResponse.Content.ReadAsStringAsync();

            if (!httpResponse.IsSuccessStatusCode)
                throw new Exception($"Shopify GraphQL fulfillmentCreate failed ({httpResponse.StatusCode}).");

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("fulfillmentCreate", out var fc) &&
                fc.TryGetProperty("userErrors", out var ue) && ue.GetArrayLength() > 0)
            {
                var errs = string.Join("; ", ue.EnumerateArray().Select(e => e.GetProperty("message").GetString()));
                throw new Exception($"Shopify fulfillmentCreate userErrors: {errs}");
            }

            var gid = root.GetProperty("data").GetProperty("fulfillmentCreate")
                .GetProperty("fulfillment").GetProperty("id").GetString() ?? "";
            var lastSlash = gid.LastIndexOf('/');
            return lastSlash >= 0 ? gid[(lastSlash + 1)..] : gid;
        }
    }
}

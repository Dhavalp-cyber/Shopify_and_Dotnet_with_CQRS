using MediatR;
using ShopifyProductSync.Configuration;
using ShopifyProductSync.Services;
using Microsoft.Extensions.Options;

namespace ShopifyProductSync.CQRS.Commands.FulfillOrder
{
    /// <summary>
    /// Handles FulfillOrderCommand.
    ///
    /// Full fulfillment flow:
    ///   1. Validate carrier name against AllowedTrackingCarriers from appsettings.json.
    ///   2. Fetch the Shopify order using ShopifySharp OrderService.
    ///   3. Check if order exists — return error if not found.
    ///   4. Check if order is already fulfilled — return error if so.
    ///   5. Fetch fulfillment orders using ShopifySharp FulfillmentOrderService.
    ///   6. Find the OPEN fulfillment order — return error if none found.
    ///   7. Create fulfillment via Shopify GraphQL fulfillmentCreate mutation.
    ///   8. Return success result with fulfillment details.
    /// </summary>
    public class FulfillOrderCommandHandler : IRequestHandler<FulfillOrderCommand, FulfillOrderResult>
    {
        private readonly ShopifyFulfillmentService _fulfillmentService;
        private readonly ShopifySettings _settings;
        private readonly ILogger<FulfillOrderCommandHandler> _logger;

        public FulfillOrderCommandHandler(
            ShopifyFulfillmentService fulfillmentService,
            IOptions<ShopifySettings> settings,
            ILogger<FulfillOrderCommandHandler> logger)
        {
            _fulfillmentService = fulfillmentService;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<FulfillOrderResult> Handle(
            FulfillOrderCommand command,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "FulfillOrder request received — OrderId: {OrderId}, Carrier: {Carrier}, " +
                "TrackingNumber: {TrackingNumber}, NotifyCustomer: {Notify}",
                command.OrderId, command.ShippingCarrierName,
                command.TrackingNumber, command.NotifyCustomer);

            // ── Step 1: Validate carrier name against allowed list ────────────
            // Carrier names are read from appsettings.json → Shopify:AllowedTrackingCarriers
            // This avoids hardcoding carrier names in code
            var isCarrierValid = _settings.AllowedTrackingCarriers
                .Any(c => c.Equals(command.ShippingCarrierName, StringComparison.OrdinalIgnoreCase));

            if (!isCarrierValid)
            {
                _logger.LogWarning(
                    "Invalid carrier name: {Carrier}. Allowed: {Allowed}",
                    command.ShippingCarrierName,
                    string.Join(", ", _settings.AllowedTrackingCarriers));

                return new FulfillOrderResult
                {
                    Message = "Shipping carrier is invalid."
                };
            }

            // ── Step 2: Fetch the Shopify order ──────────────────────────────
            var order = await _fulfillmentService.GetOrderAsync(command.OrderId);

            // ── Step 3: Check if order exists ────────────────────────────────
            if (order == null)
            {
                _logger.LogWarning("Order not found — OrderId: {OrderId}", command.OrderId);
                return new FulfillOrderResult { Message = "Order not found." };
            }

            _logger.LogInformation(
                "Order found — OrderId: {OrderId}, FulfillmentStatus: {Status}",
                command.OrderId, order.FulfillmentStatus ?? "null");

            // ── Step 4: Check if order is already fulfilled ──────────────────
            // Shopify sets FulfillmentStatus to "fulfilled" when all items are fulfilled
            if (order.FulfillmentStatus?.Equals("fulfilled", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogWarning(
                    "Order already fulfilled — OrderId: {OrderId}", command.OrderId);
                return new FulfillOrderResult { Message = "Order already fulfilled." };
            }

            // ── Step 5 & 6: Fetch open fulfillment order ─────────────────────
            // Shopify requires a FulfillmentOrderId (not OrderId) to create a fulfillment.
            // FulfillmentOrders represent groups of line items to be fulfilled together.
            var openFulfillmentOrder = await _fulfillmentService.GetOpenFulfillmentOrderAsync(command.OrderId);

            if (openFulfillmentOrder == null || openFulfillmentOrder.Id == null)
            {
                _logger.LogWarning(
                    "No open fulfillment order found — OrderId: {OrderId}", command.OrderId);
                return new FulfillOrderResult
                {
                    Message = "No open fulfillment order found for this order."
                };
            }

            // ── Step 7: Create fulfillment via Shopify GraphQL ───────────────
            var fulfillmentId = await _fulfillmentService.CreateFulfillmentAsync(
                openFulfillmentOrder.Id.Value,
                command.TrackingNumber,
                command.ShippingCarrierName,
                command.NotifyCustomer);

            _logger.LogInformation(
                "Order fulfilled successfully — OrderId: {OrderId}, FulfillmentId: {FulfillmentId}",
                command.OrderId, fulfillmentId);

            // ── Step 8: Return success result ────────────────────────────────
            return new FulfillOrderResult
            {
                Message = "Order fulfilled successfully.",
                OrderId = command.OrderId,
                FulfillmentId = fulfillmentId,
                TrackingNumber = command.TrackingNumber,
                ShippingCarrierName = command.ShippingCarrierName,
                NotifyCustomer = command.NotifyCustomer
            };
        }
    }
}

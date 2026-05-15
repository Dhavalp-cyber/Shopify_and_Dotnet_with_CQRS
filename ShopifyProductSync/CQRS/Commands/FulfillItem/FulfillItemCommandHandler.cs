using MediatR;
using Microsoft.Extensions.Logging;
using ShopifyProductSync.Configuration;
using ShopifyProductSync.Services;
using Microsoft.Extensions.Options;

namespace ShopifyProductSync.CQRS.Commands.FulfillItem
{
    /// <summary>
    /// Handles FulfillItemCommand.
    ///
    /// Single-item fulfillment flow:
    ///   1. Validate carrier name against AllowedTrackingCarriers from appsettings.json.
    ///   2. Fetch the Shopify order to verify it exists.
    ///   3. Check the order is not already fully fulfilled.
    ///   4. Call FulfillSingleItemAsync with the exact FulfillmentOrderLineItemId.
    ///   5. Return success result with fulfillment details.
    ///
    /// Does NOT fulfill the entire order — only the specified line item.
    /// </summary>
    public class FulfillItemCommandHandler : IRequestHandler<FulfillItemCommand, FulfillItemResult>
    {
        private readonly ShopifyFulfillmentService _fulfillmentService;
        private readonly ShopifySettings _settings;
        private readonly ILogger<FulfillItemCommandHandler> _logger;

        public FulfillItemCommandHandler(
            ShopifyFulfillmentService fulfillmentService,
            IOptions<ShopifySettings> settings,
            ILogger<FulfillItemCommandHandler> logger)
        {
            _fulfillmentService = fulfillmentService;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<FulfillItemResult> Handle(
            FulfillItemCommand command,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "FulfillItem request received — OrderId: {OrderId}, " +
                "FulfillmentOrderId: {FoId}, FulfillmentOrderLineItemId: {FoliId}, " +
                "Quantity: {Qty}, Carrier: {Carrier}",
                command.OrderId, command.FulfillmentOrderId,
                command.FulfillmentOrderLineItemId, command.Quantity,
                command.ShippingCarrierName);

            // ── Step 1: Validate carrier name against allowed list ────────────
            var isCarrierValid = _settings.AllowedTrackingCarriers
                .Any(c => c.Equals(command.ShippingCarrierName, StringComparison.OrdinalIgnoreCase));

            if (!isCarrierValid)
            {
                _logger.LogWarning(
                    "Invalid carrier name: {Carrier}. Allowed: {Allowed}",
                    command.ShippingCarrierName,
                    string.Join(", ", _settings.AllowedTrackingCarriers));

                return new FulfillItemResult
                {
                    Message = "Shipping carrier is invalid."
                };
            }

            // ── Step 2: Fetch the Shopify order to verify it exists ───────────
            var order = await _fulfillmentService.GetOrderAsync(command.OrderId);

            if (order == null)
            {
                _logger.LogWarning("Order not found — OrderId: {OrderId}", command.OrderId);
                return new FulfillItemResult { Message = "Order not found." };
            }

            _logger.LogInformation(
                "Order found — OrderId: {OrderId}, FulfillmentStatus: {Status}",
                command.OrderId, order.FulfillmentStatus ?? "null");

            // ── Step 3: Check if the entire order is already fulfilled ────────
            // If the order is fully fulfilled, no items can be fulfilled further.
            if (order.FulfillmentStatus?.Equals("fulfilled", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogWarning(
                    "Order already fully fulfilled — OrderId: {OrderId}", command.OrderId);
                return new FulfillItemResult { Message = "Order is already fully fulfilled." };
            }

            // ── Step 4: Fulfill only the specified line item via GraphQL ──────
            // FulfillSingleItemAsync uses lineItemsByFulfillmentOrder with
            // fulfillmentOrderLineItems to target exactly one item.
            // Throws InvalidOperationException if Shopify returns userErrors
            // (e.g. item already fulfilled, invalid quantity, carrier rejected).
            var fulfillmentId = await _fulfillmentService.FulfillSingleItemAsync(
                command.FulfillmentOrderId,
                command.FulfillmentOrderLineItemId,
                command.Quantity,
                command.TrackingNumber,
                command.ShippingCarrierName,
                command.NotifyCustomer);

            _logger.LogInformation(
                "Item fulfilled successfully — OrderId: {OrderId}, " +
                "FulfillmentId: {FulfillmentId}, FulfillmentOrderLineItemId: {FoliId}",
                command.OrderId, fulfillmentId, command.FulfillmentOrderLineItemId);

            // ── Step 5: Return success result ─────────────────────────────────
            return new FulfillItemResult
            {
                Message = "Item fulfilled successfully.",
                OrderId = command.OrderId,
                FulfillmentId = fulfillmentId,
                TrackingNumber = command.TrackingNumber,
                ShippingCarrierName = command.ShippingCarrierName,
                NotifyCustomer = command.NotifyCustomer
            };
        }
    }
}

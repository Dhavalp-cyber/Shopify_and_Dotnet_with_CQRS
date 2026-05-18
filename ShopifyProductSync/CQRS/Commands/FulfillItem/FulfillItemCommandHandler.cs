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
    /// Fulfillment flow:
    ///   1. Validate carrier name against AllowedTrackingCarriers from appsettings.json.
    ///   2. Fetch the Shopify order to verify it exists.
    ///   3. Check the order is not already fully fulfilled.
    ///   4. Call FulfillMultipleItemsAsync — sends ONE Shopify GraphQL call
    ///      regardless of how many items are in the request.
    ///   5. Return success result with fulfillment details.
    ///
    /// Does NOT fulfill the entire order — only the specified line items.
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
                "ItemCount: {Count}, Carrier: {Carrier}",
                command.OrderId, command.Items.Count, command.ShippingCarrierName);

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
            if (order.FulfillmentStatus?.Equals("fulfilled", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogWarning(
                    "Order already fully fulfilled — OrderId: {OrderId}", command.OrderId);
                return new FulfillItemResult { Message = "Order is already fully fulfilled." };
            }

            // ── Step 4: Fulfill all specified items in ONE Shopify API call ───
            // FulfillMultipleItemsAsync groups items by fulfillmentOrderId and sends
            // a single fulfillmentCreate mutation — no matter how many items are selected.
            var lineItems = command.Items
                .Select(i => (i.FulfillmentOrderId, i.FulfillmentOrderLineItemId, i.Quantity))
                .ToList();

            var fulfillmentId = await _fulfillmentService.FulfillMultipleItemsAsync(
                lineItems,
                command.TrackingNumber,
                command.ShippingCarrierName,
                command.NotifyCustomer);

            _logger.LogInformation(
                "Items fulfilled successfully — OrderId: {OrderId}, " +
                "FulfillmentId: {FulfillmentId}, ItemCount: {Count}",
                command.OrderId, fulfillmentId, command.Items.Count);

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

using MediatR;

namespace ShopifyProductSync.CQRS.Commands.FulfillItem
{
    /// <summary>
    /// Command to fulfill ONE specific line item from a Shopify order.
    /// Uses fulfillmentOrderId + fulfillmentOrderLineItemId to target exactly one item.
    /// Does NOT fulfill the entire order.
    /// </summary>
    public class FulfillItemCommand : IRequest<FulfillItemResult>
    {
        public long OrderId { get; set; }
        public long FulfillmentOrderId { get; set; }
        public long FulfillmentOrderLineItemId { get; set; }
        public int Quantity { get; set; }
        public string TrackingNumber { get; set; } = string.Empty;
        public string ShippingCarrierName { get; set; } = string.Empty;
        public bool NotifyCustomer { get; set; }
    }

    /// <summary>
    /// Result returned after a single item is successfully fulfilled.
    /// </summary>
    public class FulfillItemResult
    {
        public string Message { get; set; } = string.Empty;
        public long OrderId { get; set; }
        public long FulfillmentId { get; set; }
        public string TrackingNumber { get; set; } = string.Empty;
        public string ShippingCarrierName { get; set; } = string.Empty;
        public bool NotifyCustomer { get; set; }
    }
}

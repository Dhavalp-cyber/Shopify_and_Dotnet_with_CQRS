using MediatR;
using ShopifyProductSync.DTOs;

namespace ShopifyProductSync.CQRS.Commands.FulfillItem
{
    /// <summary>
    /// Command to fulfill one or more specific line items from a Shopify order.
    /// All items are fulfilled in a single Shopify fulfillmentCreate GraphQL call.
    /// Items belonging to different fulfillmentOrderIds are grouped automatically.
    /// Does NOT fulfill the entire order.
    /// </summary>
    public class FulfillItemCommand : IRequest<FulfillItemResult>
    {
        public long OrderId { get; set; }

        /// <summary>
        /// One or more line items to fulfill. Replaces the previous single-item fields.
        /// </summary>
        public List<FulfillLineItemDto> Items { get; set; } = new();

        public string TrackingNumber { get; set; } = string.Empty;
        public string ShippingCarrierName { get; set; } = string.Empty;
        public bool NotifyCustomer { get; set; }
    }

    /// <summary>
    /// Result returned after items are successfully fulfilled.
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

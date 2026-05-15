using MediatR;

namespace ShopifyProductSync.CQRS.Commands.FulfillOrder
{
    /// <summary>
    /// Command to fulfill an existing Shopify order.
    /// Sent by OrdersController via IMediator.
    /// Handled by FulfillOrderCommandHandler.
    /// </summary>
    public class FulfillOrderCommand : IRequest<FulfillOrderResult>
    {
        public long OrderId { get; set; }
        public string TrackingNumber { get; set; } = string.Empty;
        public string ShippingCarrierName { get; set; } = string.Empty;
        public bool NotifyCustomer { get; set; }
    }

    /// <summary>
    /// Result returned after a Shopify order is successfully fulfilled.
    /// </summary>
    public class FulfillOrderResult
    {
        public string Message { get; set; } = string.Empty;
        public long OrderId { get; set; }
        public string FulfillmentId { get; set; } = string.Empty;
        public string TrackingNumber { get; set; } = string.Empty;
        public string ShippingCarrierName { get; set; } = string.Empty;
        public bool NotifyCustomer { get; set; }
    }
}

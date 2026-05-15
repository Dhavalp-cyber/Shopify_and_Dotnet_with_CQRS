namespace ShopifyProductSync.DTOs
{
    /// <summary>
    /// Request body for POST /api/orders/fulfill-item.
    /// Fulfills ONE specific line item from a Shopify order.
    /// Use the fulfillmentOrderId and fulfillmentOrderLineItemId values
    /// returned by GET /api/orders or GET /api/orders/unfulfilled-with-items.
    /// </summary>
    public class FulfillItemRequest
    {
        /// <summary>The numeric Shopify order ID.</summary>
        public long OrderId { get; set; }

        /// <summary>
        /// The FulfillmentOrder ID that contains the line item.
        /// Returned as fulfillmentOrderId in the orders API response.
        /// </summary>
        public long FulfillmentOrderId { get; set; }

        /// <summary>
        /// The specific FulfillmentOrderLineItem ID to fulfill.
        /// Returned as fulfillmentOrderLineItemId in the orders API response.
        /// </summary>
        public long FulfillmentOrderLineItemId { get; set; }

        /// <summary>The quantity to fulfill. Must be greater than 0.</summary>
        public int Quantity { get; set; }

        /// <summary>The tracking number provided by the shipping carrier.</summary>
        public string TrackingNumber { get; set; } = string.Empty;

        /// <summary>
        /// The name of the shipping carrier (e.g. "DHL", "Aramex", "FedEx").
        /// Must match one of the values in appsettings.json → Shopify:AllowedTrackingCarriers.
        /// </summary>
        public string ShippingCarrierName { get; set; } = string.Empty;

        /// <summary>Whether to send a fulfillment notification email to the customer.</summary>
        public bool NotifyCustomer { get; set; } = true;
    }
}

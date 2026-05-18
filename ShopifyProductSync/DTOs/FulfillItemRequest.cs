namespace ShopifyProductSync.DTOs
{
    /// <summary>
    /// A single line item to fulfill within a POST /api/orders/fulfill-item request.
    /// </summary>
    public class FulfillLineItemDto
    {
        /// <summary>
        /// The FulfillmentOrder ID that contains this line item.
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
    }

    /// <summary>
    /// Request body for POST /api/orders/fulfill-item.
    /// Fulfills one or more specific line items from a Shopify order in a single Shopify API call.
    /// Use the fulfillmentOrderId and fulfillmentOrderLineItemId values
    /// returned by GET /api/orders or GET /api/orders/unfulfilled-with-items.
    /// </summary>
    public class FulfillItemRequest
    {
        /// <summary>The numeric Shopify order ID.</summary>
        public long OrderId { get; set; }

        /// <summary>
        /// One or more line items to fulfill in a single Shopify fulfillmentCreate call.
        /// Items belonging to different fulfillmentOrderIds are grouped automatically.
        /// </summary>
        public List<FulfillLineItemDto> Items { get; set; } = new();

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

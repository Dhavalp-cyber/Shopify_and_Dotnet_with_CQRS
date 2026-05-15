namespace ShopifyProductSync.DTOs
{
    /// <summary>
    /// Request body for POST /api/orders/fulfill.
    /// Contains all information needed to fulfill a Shopify order.
    /// </summary>
    public class FulfillOrderRequest
    {
        /// <summary>The numeric Shopify order ID to fulfill.</summary>
        public long OrderId { get; set; }

        /// <summary>The tracking number provided by the shipping carrier.</summary>
        public string TrackingNumber { get; set; } = string.Empty;

        /// <summary>
        /// The name of the shipping carrier (e.g. "Aramex", "DHL", "FedEx").
        /// Must match one of the values in appsettings.json → Shopify:AllowedTrackingCarriers.
        /// </summary>
        public string ShippingCarrierName { get; set; } = string.Empty;

        /// <summary>Whether to send a fulfillment notification email to the customer.</summary>
        public bool NotifyCustomer { get; set; } = true;
    }
}

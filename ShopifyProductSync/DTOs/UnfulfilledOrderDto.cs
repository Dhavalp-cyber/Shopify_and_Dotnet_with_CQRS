namespace ShopifyProductSync.DTOs
{
    /// <summary>
    /// Represents a single Shopify order that is unfulfilled or partially fulfilled.
    /// Returned as part of GET /api/orders/unfulfilled-with-items response.
    /// </summary>
    public class UnfulfilledOrderDto
    {
        public long OrderId { get; set; }
        public string OrderName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FinancialStatus { get; set; } = string.Empty;
        public string FulfillmentStatus { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string TotalPrice { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;

        /// <summary>
        /// ALL line items in this order — both fulfilled and unfulfilled.
        /// For partially fulfilled orders, both item types are included.
        /// </summary>
        public List<OrderLineItemDto> Items { get; set; } = new();
    }

    /// <summary>
    /// Represents a single line item (product) inside a Shopify order.
    /// Includes item-level fulfillment status based on fulfillableQuantity.
    /// </summary>
    public class OrderLineItemDto
    {
        public long LineItemId { get; set; }
        public long ProductId { get; set; }
        public long VariantId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string VariantTitle { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int Quantity { get; set; }

        /// <summary>
        /// How many units are still available to be fulfilled.
        /// 0 = already fulfilled, > 0 = still pending.
        /// </summary>
        public int FulfillableQuantity { get; set; }

        /// <summary>
        /// "FULFILLED" if fulfillableQuantity == 0, "UNFULFILLED" if > 0.
        /// </summary>
        public string FulfillmentStatus { get; set; } = string.Empty;

        /// <summary>
        /// "Already fulfilled" or "Ready to fulfill" — human-readable message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}

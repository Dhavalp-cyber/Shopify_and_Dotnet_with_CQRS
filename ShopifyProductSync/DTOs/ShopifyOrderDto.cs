using System.Text.Json.Serialization;

namespace ShopifyProductSync.DTOs
{
    // ─────────────────────────────────────────────────────────────────────────
    // Shopify GraphQL response wrappers — orders query
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Root GraphQL response wrapper for the orders query.
    /// Shopify always wraps responses in { "data": { ... } }
    /// </summary>
    public class ShopifyOrdersGraphQLResponse
    {
        [JsonPropertyName("data")]
        public ShopifyOrdersGraphQLData? Data { get; set; }
    }

    public class ShopifyOrdersGraphQLData
    {
        [JsonPropertyName("orders")]
        public ShopifyOrderConnection? Orders { get; set; }
    }

    /// <summary>
    /// Shopify Connection pattern for paginated order lists.
    /// </summary>
    public class ShopifyOrderConnection
    {
        [JsonPropertyName("edges")]
        public List<ShopifyOrderEdge> Edges { get; set; } = new();

        [JsonPropertyName("pageInfo")]
        public ShopifyPageInfo? PageInfo { get; set; }
    }

    public class ShopifyOrderEdge
    {
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; } = string.Empty;

        [JsonPropertyName("node")]
        public ShopifyOrderNode? Node { get; set; }
    }

    /// <summary>
    /// A single order node returned from Shopify GraphQL.
    /// </summary>
    public class ShopifyOrderNode
    {
        // GID format: "gid://shopify/Order/1234567890"
        [JsonPropertyName("id")]
        public string Gid { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        // Shopify GraphQL returns UPPERCASE: "PAID", "PENDING", "REFUNDED", etc.
        [JsonPropertyName("displayFinancialStatus")]
        public string FinancialStatus { get; set; } = string.Empty;

        // Shopify GraphQL returns UPPERCASE: "UNFULFILLED", "PARTIALLY_FULFILLED", "FULFILLED"
        [JsonPropertyName("displayFulfillmentStatus")]
        public string FulfillmentStatus { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("totalPriceSet")]
        public ShopifyMoneySet? TotalPriceSet { get; set; }

        [JsonPropertyName("customer")]
        public ShopifyOrderCustomer? Customer { get; set; }

        [JsonPropertyName("lineItems")]
        public ShopifyLineItemConnection? LineItems { get; set; }

        [JsonPropertyName("fulfillmentOrders")]
        public ShopifyFulfillmentOrderConnection? FulfillmentOrders { get; set; }
    }

    public class ShopifyMoneySet
    {
        [JsonPropertyName("shopMoney")]
        public ShopifyMoney? ShopMoney { get; set; }
    }

    public class ShopifyMoney
    {
        [JsonPropertyName("amount")]
        public string Amount { get; set; } = "0.00";

        [JsonPropertyName("currencyCode")]
        public string CurrencyCode { get; set; } = string.Empty;
    }

    public class ShopifyOrderCustomer
    {
        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }

    public class ShopifyLineItemConnection
    {
        [JsonPropertyName("edges")]
        public List<ShopifyLineItemEdge> Edges { get; set; } = new();
    }

    public class ShopifyLineItemEdge
    {
        [JsonPropertyName("node")]
        public ShopifyLineItemNode? Node { get; set; }
    }

    public class ShopifyLineItemNode
    {
        // GID format: "gid://shopify/LineItem/1234567890"
        [JsonPropertyName("id")]
        public string Gid { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("variantTitle")]
        public string? VariantTitle { get; set; }

        [JsonPropertyName("sku")]
        public string? Sku { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("fulfillableQuantity")]
        public int FulfillableQuantity { get; set; }

        [JsonPropertyName("variant")]
        public ShopifyLineItemVariant? Variant { get; set; }
    }

    public class ShopifyLineItemVariant
    {
        // GID format: "gid://shopify/ProductVariant/1234567890"
        [JsonPropertyName("id")]
        public string Gid { get; set; } = string.Empty;

        [JsonPropertyName("product")]
        public ShopifyLineItemProduct? Product { get; set; }
    }

    public class ShopifyLineItemProduct
    {
        // GID format: "gid://shopify/Product/1234567890"
        [JsonPropertyName("id")]
        public string Gid { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Shopify GraphQL — fulfillmentOrders connection (nested inside an order)
    // Used to resolve fulfillmentOrderId and fulfillmentOrderLineItemId per line item
    // ─────────────────────────────────────────────────────────────────────────

    public class ShopifyFulfillmentOrderConnection
    {
        [JsonPropertyName("edges")]
        public List<ShopifyFulfillmentOrderEdge> Edges { get; set; } = new();
    }

    public class ShopifyFulfillmentOrderEdge
    {
        [JsonPropertyName("node")]
        public ShopifyFulfillmentOrderNode? Node { get; set; }
    }

    public class ShopifyFulfillmentOrderNode
    {
        // GID format: "gid://shopify/FulfillmentOrder/1234567890"
        [JsonPropertyName("id")]
        public string Gid { get; set; } = string.Empty;

        [JsonPropertyName("lineItems")]
        public ShopifyFulfillmentOrderLineItemConnection? LineItems { get; set; }
    }

    public class ShopifyFulfillmentOrderLineItemConnection
    {
        [JsonPropertyName("edges")]
        public List<ShopifyFulfillmentOrderLineItemEdge> Edges { get; set; } = new();
    }

    public class ShopifyFulfillmentOrderLineItemEdge
    {
        [JsonPropertyName("node")]
        public ShopifyFulfillmentOrderLineItemNode? Node { get; set; }
    }

    public class ShopifyFulfillmentOrderLineItemNode
    {
        // GID format: "gid://shopify/FulfillmentOrderLineItem/1234567890"
        [JsonPropertyName("id")]
        public string Gid { get; set; } = string.Empty;

        /// <summary>
        /// Links this fulfillment order line item back to the order's lineItem.
        /// Used to match fulfillmentOrderId + fulfillmentOrderLineItemId to each lineItem.
        /// </summary>
        [JsonPropertyName("lineItem")]
        public ShopifyFulfillmentOrderLineItemRef? LineItem { get; set; }
    }

    public class ShopifyFulfillmentOrderLineItemRef
    {
        // GID format: "gid://shopify/LineItem/1234567890"
        [JsonPropertyName("id")]
        public string Gid { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Clean frontend-friendly response DTOs
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Top-level response returned to the API caller for GET /api/orders/unfulfilled-with-items.
    /// </summary>
    public class UnfulfilledOrdersResponseDto
    {
        public int TotalOrders { get; set; }
        public List<OrderSummaryDto> Orders { get; set; } = new();
    }

    /// <summary>
    /// A single order with all its line items.
    /// Returned for both UNFULFILLED and PARTIALLY_FULFILLED orders.
    /// </summary>
    public class OrderSummaryDto
    {
        public long OrderId { get; set; }
        public string OrderName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string FinancialStatus { get; set; } = string.Empty;
        public string FulfillmentStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string TotalPrice { get; set; } = "0.00";
        public string Currency { get; set; } = string.Empty;
        public List<OrderLineItemDto> Items { get; set; } = new();
    }

    /// <summary>
    /// A single line item within an order.
    /// fulfillmentStatus and message are derived from fulfillableQuantity.
    /// fulfillmentOrderId and fulfillmentOrderLineItemId are resolved from
    /// the order's fulfillmentOrders connection.
    /// </summary>
    public class OrderLineItemDto
    {
        public long LineItemId { get; set; }
        public long ProductId { get; set; }
        public long VariantId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? VariantTitle { get; set; }
        public string? Sku { get; set; }
        public int Quantity { get; set; }
        public int FulfillableQuantity { get; set; }

        /// <summary>
        /// The FulfillmentOrder ID this line item belongs to.
        /// Required when creating a fulfillment via the fulfillmentCreate mutation.
        /// 0 if not resolved (e.g. order has no open fulfillment order).
        /// </summary>
        public long FulfillmentOrderId { get; set; }

        /// <summary>
        /// The FulfillmentOrderLineItem ID for this specific line item.
        /// Used for partial fulfillments targeting individual line items.
        /// 0 if not resolved.
        /// </summary>
        public long FulfillmentOrderLineItemId { get; set; }

        /// <summary>
        /// "FULFILLED" when fulfillableQuantity == 0, otherwise "UNFULFILLED".
        /// </summary>
        public string FulfillmentStatus { get; set; } = string.Empty;

        /// <summary>
        /// "Already fulfilled" when fulfillableQuantity == 0, otherwise "Ready to fulfill".
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}

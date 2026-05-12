using System.Text.Json.Serialization;

namespace ShopifyProductSync.DTOs
{
    /// <summary>
    /// Root response wrapper from Shopify GraphQL API.
    /// Shopify always wraps the response in { "data": { ... } }
    /// </summary>
    public class ShopifyGraphQLResponse
    {
        [JsonPropertyName("data")]
        public ShopifyGraphQLData? Data { get; set; }
    }

    public class ShopifyGraphQLData
    {
        [JsonPropertyName("products")]
        public ShopifyProductConnection? Products { get; set; }
    }

    /// <summary>
    /// Response wrapper for single product query: { product(id: "...") { ... } }
    /// </summary>
    public class ShopifyGraphQLSingleResponse
    {
        [JsonPropertyName("data")]
        public ShopifyGraphQLSingleData? Data { get; set; }
    }

    public class ShopifyGraphQLSingleData
    {
        [JsonPropertyName("product")]
        public ShopifyGraphQLProduct? Product { get; set; }
    }

    /// <summary>
    /// Shopify uses a "Connection" pattern for paginated lists.
    /// edges = the list of items
    /// pageInfo = pagination info (has next page, cursor)
    /// </summary>
    public class ShopifyProductConnection
    {
        [JsonPropertyName("edges")]
        public List<ShopifyProductEdge> Edges { get; set; } = new();

        [JsonPropertyName("pageInfo")]
        public ShopifyPageInfo? PageInfo { get; set; }
    }

    public class ShopifyProductEdge
    {
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; } = string.Empty;

        [JsonPropertyName("node")]
        public ShopifyGraphQLProduct? Node { get; set; }
    }

    /// <summary>
    /// A single product returned from Shopify GraphQL.
    /// </summary>
    public class ShopifyGraphQLProduct
    {
        // Shopify GraphQL returns IDs as "gid://shopify/Product/1234567890"
        // We parse the numeric part out in the service
        [JsonPropertyName("id")]
        public string Gid { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("vendor")]
        public string Vendor { get; set; } = string.Empty;

        // GraphQL returns status in UPPERCASE: "ACTIVE", "DRAFT", "ARCHIVED"
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("productType")]
        public string ProductType { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        // Variants connection — we only ask for the first variant's price
        [JsonPropertyName("variants")]
        public ShopifyVariantConnection? Variants { get; set; }
    }

    public class ShopifyVariantConnection
    {
        [JsonPropertyName("edges")]
        public List<ShopifyVariantEdge> Edges { get; set; } = new();
    }

    public class ShopifyVariantEdge
    {
        [JsonPropertyName("node")]
        public ShopifyGraphQLVariant? Node { get; set; }
    }

    public class ShopifyGraphQLVariant
    {
        [JsonPropertyName("price")]
        public string Price { get; set; } = "0";
    }

    /// <summary>
    /// Pagination info returned by Shopify GraphQL.
    /// Used to fetch the next page of products.
    /// </summary>
    public class ShopifyPageInfo
    {
        [JsonPropertyName("hasNextPage")]
        public bool HasNextPage { get; set; }

        [JsonPropertyName("endCursor")]
        public string? EndCursor { get; set; }
    }

    /// <summary>
    /// Clean response DTO returned to the API caller.
    /// Flattened from the nested GraphQL structure.
    /// </summary>
    public class ShopifyProductResponseDto
    {
        public long ShopifyProductId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Source { get; set; } = "Shopify";
    }
}

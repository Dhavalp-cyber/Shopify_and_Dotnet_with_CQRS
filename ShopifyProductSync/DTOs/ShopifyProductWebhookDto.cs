using System.Text.Json.Serialization;

namespace ShopifyProductSync.DTOs
{
    /// <summary>
    /// DTO that maps the JSON payload Shopify sends in a product/create webhook.
    /// Shopify sends snake_case JSON, so we use JsonPropertyName to map correctly.
    /// Only the fields we need are mapped here.
    /// </summary>
    public class ShopifyProductWebhookDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("vendor")]
        public string Vendor { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("product_type")]
        public string ProductType { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        // Shopify sends variants array; we grab the price from the first variant
        [JsonPropertyName("variants")]
        public List<ShopifyVariantDto> Variants { get; set; } = new();
    }

    /// <summary>
    /// Represents a single product variant from Shopify webhook payload.
    /// </summary>
    public class ShopifyVariantDto
    {
        [JsonPropertyName("price")]
        public string Price { get; set; } = "0";
    }
}

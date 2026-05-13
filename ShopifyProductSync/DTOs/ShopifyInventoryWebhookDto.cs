using System.Text.Json.Serialization;

namespace ShopifyProductSync.DTOs
{
    /// <summary>
    /// DTO that maps the JSON payload Shopify sends in an inventory_levels/update webhook.
    /// Shopify sends snake_case JSON, so we use JsonPropertyName to map correctly.
    ///
    /// Example Shopify payload:
    /// {
    ///   "inventory_item_id": 987654321,
    ///   "location_id": 555555555,
    ///   "available": 25,
    ///   "updated_at": "2024-01-01T00:00:00Z"
    /// }
    /// </summary>
    public class ShopifyInventoryWebhookDto
    {
        [JsonPropertyName("inventory_item_id")]
        public long InventoryItemId { get; set; }

        [JsonPropertyName("location_id")]
        public long LocationId { get; set; }

        // "available" is the on-hand quantity Shopify reports
        [JsonPropertyName("available")]
        public int? Available { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}

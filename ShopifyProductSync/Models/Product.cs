namespace ShopifyProductSync.Models
{
    /// <summary>
    /// Represents a product stored in the local database.
    /// Source = "ShopifyWebhook" means it came from Shopify via webhook.
    /// Source = "Api" means it was created from our own API endpoint.
    /// </summary>
    public class Product
    {
        public int Id { get; set; }

        // The product ID assigned by Shopify (long because Shopify uses large numbers)
        public long ShopifyProductId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Vendor { get; set; } = string.Empty;

        // active, draft, archived
        public string Status { get; set; } = string.Empty;

        public string ProductType { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        // "ShopifyWebhook" or "Api"
        public string Source { get; set; } = string.Empty;
    }
}

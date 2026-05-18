namespace ShopifyProductSync.Models
{
    /// <summary>
    /// Represents a Shopify order stored in the local database.
    /// Stores the order's note and note_attributes for offline access
    /// and as a local cache of what was last synced to Shopify.
    /// </summary>
    public class Order
    {
        public int Id { get; set; }

        // The numeric order ID assigned by Shopify (e.g. 6726812303404)
        public long ShopifyOrderId { get; set; }

        // The human-readable order name (e.g. "#1009")
        public string OrderName { get; set; } = string.Empty;

        // Free-text note on the order (Shopify: order.note)
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        // Navigation property — one Order has many NoteAttributes
        public ICollection<OrderNoteAttribute> NoteAttributes { get; set; }
            = new List<OrderNoteAttribute>();
    }
}

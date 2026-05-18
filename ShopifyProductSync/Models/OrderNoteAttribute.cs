namespace ShopifyProductSync.Models
{
    /// <summary>
    /// Represents one name-value pair from Shopify's order.note_attributes array.
    /// Each Order can have many NoteAttributes (one row per key-value pair).
    ///
    /// Example Shopify payload:
    ///   "note_attributes": [
    ///     { "name": "gift_message", "value": "Happy Birthday!" },
    ///     { "name": "source",       "value": "mobile_app" }
    ///   ]
    /// </summary>
    public class OrderNoteAttribute
    {
        public int Id { get; set; }

        // Foreign key → Order table
        public int OrderId { get; set; }

        // The attribute key (e.g. "gift_message", "source")
        public string Name { get; set; } = string.Empty;

        // The attribute value (e.g. "Happy Birthday!", "mobile_app")
        public string Value { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        // Navigation property → the parent order
        public Order Order { get; set; } = null!;
    }
}

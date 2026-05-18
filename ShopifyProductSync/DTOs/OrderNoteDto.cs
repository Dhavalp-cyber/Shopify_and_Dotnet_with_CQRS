namespace ShopifyProductSync.DTOs
{
    /// <summary>
    /// Request body for POST /api/orders/{orderId}/note-attributes.
    /// Appends new note_attributes to the order (additive — does not remove existing ones).
    /// </summary>
    public class AddOrderNoteAttributesRequest
    {
        /// <summary>
        /// Optional free-text note to set on the order.
        /// If null or empty, the existing note is preserved.
        /// </summary>
        public string? Note { get; set; }

        /// <summary>
        /// List of name-value pairs to append to the order's note_attributes.
        /// If a key already exists locally it will be updated; new keys are inserted.
        /// All attributes are synced to Shopify via orderUpdate mutation.
        /// </summary>
        public List<NoteAttributeDto> NoteAttributes { get; set; } = new();
    }

    /// <summary>
    /// A single name-value pair for an order note attribute.
    /// </summary>
    public class NoteAttributeDto
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response returned by GET /api/orders/{orderId}/note
    /// and POST /api/orders/{orderId}/note-attributes.
    /// </summary>
    public class OrderNoteResponseDto
    {
        public long ShopifyOrderId { get; set; }
        public string OrderName { get; set; } = string.Empty;
        public string? Note { get; set; }
        public List<NoteAttributeDto> NoteAttributes { get; set; } = new();
    }
}

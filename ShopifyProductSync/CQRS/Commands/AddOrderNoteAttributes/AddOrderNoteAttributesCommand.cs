using MediatR;
using ShopifyProductSync.DTOs;

namespace ShopifyProductSync.CQRS.Commands.AddOrderNoteAttributes
{
    /// <summary>
    /// Command to append note_attributes (and optionally update the note) on a Shopify order.
    /// Additive: existing attributes are preserved; new keys are inserted; existing keys updated.
    /// Syncs the merged result to Shopify via the orderUpdate GraphQL mutation.
    /// Also upserts the order record in the local database.
    /// </summary>
    public class AddOrderNoteAttributesCommand : IRequest<AddOrderNoteAttributesResult>
    {
        /// <summary>
        /// The Shopify order ID. Accepts numeric or GID format.
        /// </summary>
        public string OrderId { get; set; } = string.Empty;

        /// <summary>
        /// Optional note text. If null or empty, the existing note is preserved.
        /// </summary>
        public string? Note { get; set; }

        /// <summary>
        /// Name-value pairs to append/update on the order.
        /// </summary>
        public List<NoteAttributeDto> NoteAttributes { get; set; } = new();
    }

    /// <summary>
    /// Result returned after note_attributes are successfully saved and synced.
    /// </summary>
    public class AddOrderNoteAttributesResult
    {
        public string Message { get; set; } = string.Empty;
        public OrderNoteResponseDto? Order { get; set; }
    }
}

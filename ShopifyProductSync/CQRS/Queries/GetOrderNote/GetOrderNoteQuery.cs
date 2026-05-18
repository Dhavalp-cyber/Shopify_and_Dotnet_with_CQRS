using MediatR;
using ShopifyProductSync.DTOs;

namespace ShopifyProductSync.CQRS.Queries.GetOrderNote
{
    /// <summary>
    /// Query to fetch the note and note_attributes for a Shopify order.
    /// Looks up the order in the local database by ShopifyOrderId.
    /// Supports flexible orderId input: numeric ID or GID string.
    /// Returns null if the order has not been synced locally yet.
    /// </summary>
    public class GetOrderNoteQuery : IRequest<OrderNoteResponseDto?>
    {
        /// <summary>
        /// The Shopify order ID. Accepts:
        ///   - Numeric:  6726812303404
        ///   - GID:      gid://shopify/Order/6726812303404
        /// </summary>
        public string OrderId { get; set; } = string.Empty;
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopifyProductSync.CQRS.Common.Interfaces;
using ShopifyProductSync.DTOs;

namespace ShopifyProductSync.CQRS.Queries.GetOrderNote
{
    /// <summary>
    /// Handles GetOrderNoteQuery.
    /// Reads the order's note and note_attributes from the local database.
    /// Returns null if the order has not been synced locally yet.
    /// </summary>
    public class GetOrderNoteQueryHandler
        : IRequestHandler<GetOrderNoteQuery, OrderNoteResponseDto?>
    {
        private readonly IAppDbContext _db;
        private readonly ILogger<GetOrderNoteQueryHandler> _logger;

        public GetOrderNoteQueryHandler(
            IAppDbContext db,
            ILogger<GetOrderNoteQueryHandler> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<OrderNoteResponseDto?> Handle(
            GetOrderNoteQuery request,
            CancellationToken cancellationToken)
        {
            var shopifyOrderId = ParseOrderId(request.OrderId);

            _logger.LogInformation(
                "GET /api/orders/{OrderId}/note — ShopifyOrderId: {ShopifyOrderId}",
                request.OrderId, shopifyOrderId);

            // Load order with its note_attributes in one query
            var order = await _db.Orders
                .Include(o => o.NoteAttributes)
                .FirstOrDefaultAsync(o => o.ShopifyOrderId == shopifyOrderId, cancellationToken);

            if (order == null)
            {
                _logger.LogWarning(
                    "Order not found locally — ShopifyOrderId: {ShopifyOrderId}. " +
                    "Use POST /api/orders/{OrderId}/note-attributes to create it.",
                    shopifyOrderId, request.OrderId);
                return null;
            }

            return new OrderNoteResponseDto
            {
                ShopifyOrderId = order.ShopifyOrderId,
                OrderName = order.OrderName,
                Note = order.Note,
                NoteAttributes = order.NoteAttributes
                    .Select(a => new NoteAttributeDto { Name = a.Name, Value = a.Value })
                    .ToList()
            };
        }

        /// <summary>
        /// Parses a flexible orderId string to a numeric long.
        /// Accepts:
        ///   - Plain numeric:  "6726812303404"
        ///   - GID format:     "gid://shopify/Order/6726812303404"
        /// Returns 0 if parsing fails.
        /// </summary>
        internal static long ParseOrderId(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId)) return 0;

            // GID format: "gid://shopify/Order/6726812303404"
            if (orderId.StartsWith("gid://", StringComparison.OrdinalIgnoreCase))
            {
                var lastSlash = orderId.LastIndexOf('/');
                if (lastSlash >= 0)
                    long.TryParse(orderId[(lastSlash + 1)..], out var gidId);
                var lastSlashIdx = orderId.LastIndexOf('/');
                return lastSlashIdx >= 0 && long.TryParse(orderId[(lastSlashIdx + 1)..], out var parsed)
                    ? parsed : 0;
            }

            // Plain numeric
            return long.TryParse(orderId, out var numericId) ? numericId : 0;
        }
    }
}

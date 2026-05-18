using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopifyProductSync.CQRS.Common.Interfaces;
using ShopifyProductSync.CQRS.Queries.GetOrderNote;
using ShopifyProductSync.DTOs;
using ShopifyProductSync.Models;
using ShopifyProductSync.Services;

namespace ShopifyProductSync.CQRS.Commands.AddOrderNoteAttributes
{
    /// <summary>
    /// Handles AddOrderNoteAttributesCommand.
    ///
    /// Flow:
    ///   1. Parse and validate the ShopifyOrderId from the flexible orderId input.
    ///   2. Upsert the Order record in the local database (insert if new, load if existing).
    ///   3. Merge incoming note_attributes additively:
    ///        - Existing key → update its value
    ///        - New key      → insert a new OrderNoteAttribute row
    ///   4. Update the note text if provided.
    ///   5. Save all local DB changes.
    ///   6. Sync the merged note + note_attributes to Shopify via orderUpdate GraphQL mutation.
    ///   7. Return the full updated order note response.
    /// </summary>
    public class AddOrderNoteAttributesCommandHandler
        : IRequestHandler<AddOrderNoteAttributesCommand, AddOrderNoteAttributesResult>
    {
        private readonly IAppDbContext _db;
        private readonly ShopifyFulfillmentService _fulfillmentService;
        private readonly ILogger<AddOrderNoteAttributesCommandHandler> _logger;

        public AddOrderNoteAttributesCommandHandler(
            IAppDbContext db,
            ShopifyFulfillmentService fulfillmentService,
            ILogger<AddOrderNoteAttributesCommandHandler> logger)
        {
            _db = db;
            _fulfillmentService = fulfillmentService;
            _logger = logger;
        }

        public async Task<AddOrderNoteAttributesResult> Handle(
            AddOrderNoteAttributesCommand command,
            CancellationToken cancellationToken)
        {
            // ── Step 1: Parse orderId ─────────────────────────────────────────
            var shopifyOrderId = GetOrderNoteQueryHandler.ParseOrderId(command.OrderId);

            _logger.LogInformation(
                "AddOrderNoteAttributes — ShopifyOrderId: {ShopifyOrderId}, " +
                "IncomingAttributes: {Count}, HasNote: {HasNote}",
                shopifyOrderId, command.NoteAttributes.Count,
                !string.IsNullOrEmpty(command.Note));

            // ── Step 2: Upsert Order in local DB ──────────────────────────────
            // Load existing order with its attributes, or create a new one.
            var order = await _db.Orders
                .Include(o => o.NoteAttributes)
                .FirstOrDefaultAsync(o => o.ShopifyOrderId == shopifyOrderId, cancellationToken);

            if (order == null)
            {
                // First time this order is being tracked locally
                order = new Order
                {
                    ShopifyOrderId = shopifyOrderId,
                    OrderName = string.Empty,   // will be populated if Shopify returns it
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.Orders.Add(order);

                _logger.LogInformation(
                    "Order not found locally — creating new record for ShopifyOrderId: {ShopifyOrderId}",
                    shopifyOrderId);
            }
            else
            {
                order.UpdatedAt = DateTime.UtcNow;
            }

            // ── Step 3: Update note text if provided ──────────────────────────
            if (!string.IsNullOrEmpty(command.Note))
                order.Note = command.Note;

            // ── Step 4: Merge note_attributes additively ──────────────────────
            // For each incoming attribute:
            //   - If a row with the same Name already exists → update its Value
            //   - Otherwise → insert a new row
            foreach (var incoming in command.NoteAttributes)
            {
                var existing = order.NoteAttributes
                    .FirstOrDefault(a => a.Name.Equals(incoming.Name, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    // Update existing attribute value
                    existing.Value = incoming.Value;

                    _logger.LogInformation(
                        "Updated existing NoteAttribute — Name: {Name}, Value: {Value}",
                        incoming.Name, incoming.Value);
                }
                else
                {
                    // Insert new attribute
                    var newAttr = new OrderNoteAttribute
                    {
                        Name = incoming.Name,
                        Value = incoming.Value,
                        CreatedAt = DateTime.UtcNow
                    };
                    order.NoteAttributes.Add(newAttr);

                    _logger.LogInformation(
                        "Added new NoteAttribute — Name: {Name}, Value: {Value}",
                        incoming.Name, incoming.Value);
                }
            }

            // ── Step 5: Save to local DB ──────────────────────────────────────
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Local DB updated — ShopifyOrderId: {ShopifyOrderId}, " +
                "TotalAttributes: {Total}",
                shopifyOrderId, order.NoteAttributes.Count);

            // ── Step 6: Sync to Shopify via GraphQL orderUpdate mutation ──────
            // Build the complete merged list to send to Shopify.
            // Shopify replaces all note_attributes on update, so we must send the full list.
            var allAttributes = order.NoteAttributes
                .Select(a => (a.Name, a.Value))
                .ToList();

            await _fulfillmentService.UpdateOrderNoteAsync(
                shopifyOrderId,
                order.Note,
                allAttributes);

            _logger.LogInformation(
                "Shopify synced — ShopifyOrderId: {ShopifyOrderId}", shopifyOrderId);

            // ── Step 7: Return result ─────────────────────────────────────────
            return new AddOrderNoteAttributesResult
            {
                Message = "Order note attributes updated and synced to Shopify successfully.",
                Order = new OrderNoteResponseDto
                {
                    ShopifyOrderId = order.ShopifyOrderId,
                    OrderName = order.OrderName,
                    Note = order.Note,
                    NoteAttributes = order.NoteAttributes
                        .Select(a => new NoteAttributeDto { Name = a.Name, Value = a.Value })
                        .ToList()
                }
            };
        }
    }
}

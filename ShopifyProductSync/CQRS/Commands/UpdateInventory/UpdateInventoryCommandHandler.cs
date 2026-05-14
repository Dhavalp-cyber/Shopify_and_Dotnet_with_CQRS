using MediatR;
using ShopifyProductSync.Data;
using ShopifyProductSync.Models;
using ShopifyProductSync.Services;
using Microsoft.EntityFrameworkCore;

namespace ShopifyProductSync.CQRS.Commands.UpdateInventory
{
    /// <summary>
    /// Handles UpdateInventoryCommand.
    ///
    /// Flow:
    ///   1. Look up the product in the local DB.
    ///      If not found → auto-fetch it from Shopify GraphQL and save it locally.
    ///   2. Auto-fetch the inventory item ID from Shopify using the product ID.
    ///   3. Call Shopify GraphQL mutation to update inventory (returns available qty after update).
    ///   4. Update the local DB product with the new inventory fields.
    ///   5. Find or create a Location record for the given ShopifyLocationId.
    ///   6. Insert one UpdatedInventory history record.
    ///   7. Return a success result.
    /// </summary>
    public class UpdateInventoryCommandHandler : IRequestHandler<UpdateInventoryCommand, UpdateInventoryResult>
    {
        private readonly AppDbContext _db;
        private readonly ShopifyInventoryService _inventoryService;
        private readonly ShopifyGraphQLService _graphQLService;
        private readonly ILogger<UpdateInventoryCommandHandler> _logger;

        public UpdateInventoryCommandHandler(
            AppDbContext db,
            ShopifyInventoryService inventoryService,
            ShopifyGraphQLService graphQLService,
            ILogger<UpdateInventoryCommandHandler> logger)
        {
            _db = db;
            _inventoryService = inventoryService;
            _graphQLService = graphQLService;
            _logger = logger;
        }

        public async Task<UpdateInventoryResult> Handle(
            UpdateInventoryCommand command,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Inventory update request received — ShopifyProductId: {ProductId}, " +
                "LocationId: {LocationId}, Quantity: {Qty}",
                command.ShopifyProductId,
                command.ShopifyLocationId,
                command.Quantity);

            // Find product in local DB 
            var product = await _db.Products
                .FirstOrDefaultAsync(p => p.ShopifyProductId == command.ShopifyProductId, cancellationToken);

            // If not in local DB → fetch from Shopify and save it first
            if (product == null)
            {
                _logger.LogInformation(
                    "Product {ProductId} not in local DB — fetching from Shopify and saving.",
                    command.ShopifyProductId);

                var shopifyProduct = await _graphQLService.GetProductByShopifyIdAsync(command.ShopifyProductId);

                if (shopifyProduct == null)
                    throw new KeyNotFoundException(
                        $"Product with Shopify ID {command.ShopifyProductId} not found in Shopify.");

                product = new Product
                {
                    ShopifyProductId = shopifyProduct.ShopifyProductId,
                    Title = shopifyProduct.Title,
                    Vendor = shopifyProduct.Vendor,
                    Status = shopifyProduct.Status,
                    ProductType = shopifyProduct.ProductType,
                    Price = shopifyProduct.Price,
                    CreatedAt = DateTime.SpecifyKind(shopifyProduct.CreatedAt, DateTimeKind.Utc),
                    UpdatedAt = DateTime.SpecifyKind(shopifyProduct.UpdatedAt, DateTimeKind.Utc),
                    Source = "Shopify"
                };

                _db.Products.Add(product);
                await _db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Product {ProductId} auto-saved to local DB with LocalId: {LocalId}.",
                    command.ShopifyProductId, product.Id);
            }

            // Get inventory item ID from Shopify 
            var inventoryItemId = await _inventoryService.GetInventoryItemIdAsync(command.ShopifyProductId);

            _logger.LogInformation(
                "Resolved InventoryItemId: {ItemId} for ShopifyProductId: {ProductId}",
                inventoryItemId, command.ShopifyProductId);

            // Update inventory in Shopify 
            // Returns the actual available quantity after the update
            var availableAfterUpdate = await _inventoryService.UpdateInventoryAsync(
                inventoryItemId,
                command.ShopifyLocationId,
                command.Quantity);

            //  Update product inventory fields in local DB 
            product.ShopifyInventoryItemId = inventoryItemId;
            product.ShopifyLocationId = command.ShopifyLocationId;
            product.InventoryQuantity = availableAfterUpdate;
            product.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Product inventory fields updated in local DB — ShopifyProductId: {ProductId}, " +
                "InventoryQuantity: {Qty}",
                command.ShopifyProductId, availableAfterUpdate);

            // Find or create Location record 
            var location = await _db.Locations
                .FirstOrDefaultAsync(l => l.ShopifyLocationId == command.ShopifyLocationId, cancellationToken);

            if (location == null)
            {
                // Location not in DB yet — fetch name from Shopify and insert
                var locationName = await _inventoryService.GetLocationNameAsync(command.ShopifyLocationId);

                location = new Location
                {
                    ShopifyLocationId = command.ShopifyLocationId,
                    LocationName = locationName,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Locations.Add(location);
                await _db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "New location saved — ShopifyLocationId: {LocationId}, Name: {Name}",
                    command.ShopifyLocationId, locationName);
            }
            else if (location.LocationName == "Unknown Location" || string.IsNullOrWhiteSpace(location.LocationName))
            {
                // Location exists but name was not saved properly before — try to fix it now
                var locationName = await _inventoryService.GetLocationNameAsync(command.ShopifyLocationId);

                if (locationName != "Unknown Location" && !string.IsNullOrWhiteSpace(locationName))
                {
                    location.LocationName = locationName;
                    await _db.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "Fixed location name — ShopifyLocationId: {LocationId}, Name: {Name}",
                        command.ShopifyLocationId, locationName);
                }
            }

            // ── Step 6: Save inventory update history record ─────────────────
            var historyRecord = new UpdatedInventory
            {
                ProductId = product.Id,
                LocationId = location.Id,
                // Snapshot values — saved at update time so history is preserved
                // even if product name or location name changes later in Shopify
                ShopifyProductId = product.ShopifyProductId,
                ProductName = product.Title,
                ShopifyLocationId = command.ShopifyLocationId,
                LocationName = location.LocationName,
                UpdatedQuantity = command.Quantity,
                AvailableQuantityAfterUpdate = availableAfterUpdate,
                UpdatedAt = DateTime.UtcNow
            };

            _db.UpdatedInventories.Add(historyRecord);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Inventory history saved — ProductId: {ProductId}, LocationId: {LocationId}, " +
                "UpdatedQty: {Qty}, AvailableAfter: {Available}",
                product.Id, location.Id, command.Quantity, availableAfterUpdate);

            return new UpdateInventoryResult
            {
                Message = "Inventory updated successfully in Shopify and local database.",
                ShopifyProductId = command.ShopifyProductId,
                ShopifyLocationId = command.ShopifyLocationId,
                UpdatedQuantity = command.Quantity,
                AvailableQuantityAfterUpdate = availableAfterUpdate
            };
        }
    }
}

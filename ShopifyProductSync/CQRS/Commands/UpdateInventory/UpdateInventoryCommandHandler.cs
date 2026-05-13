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
    ///   3. Call ShopifyInventoryService to run the GraphQL mutation (updates Shopify).
    ///   4. Update the local DB product with the new inventory fields.
    ///   5. Return a success result.
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

            // Step 1: Find the product in the local database
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

            // Step 2: Auto-fetch the inventory item ID from Shopify
            var inventoryItemId = await _inventoryService.GetInventoryItemIdAsync(command.ShopifyProductId);

            _logger.LogInformation(
                "Resolved InventoryItemId: {ItemId} for ShopifyProductId: {ProductId}",
                inventoryItemId, command.ShopifyProductId);

            // Step 3: Call Shopify GraphQL mutation to update inventory in Shopify
            await _inventoryService.UpdateInventoryAsync(
                inventoryItemId,
                command.ShopifyLocationId,
                command.Quantity);

            // Step 4: Update local DB record with new inventory data
            product.ShopifyInventoryItemId = inventoryItemId;
            product.ShopifyLocationId = command.ShopifyLocationId;
            product.InventoryQuantity = command.Quantity;
            product.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Local database updated — ShopifyProductId: {ProductId}, " +
                "InventoryQuantity: {Qty}, LocationId: {LocationId}",
                command.ShopifyProductId,
                command.Quantity,
                command.ShopifyLocationId);

            return new UpdateInventoryResult
            {
                Message = "Inventory updated successfully in Shopify and local database.",
                ShopifyProductId = command.ShopifyProductId,
                ShopifyLocationId = command.ShopifyLocationId,
                UpdatedQuantity = command.Quantity
            };
        }
    }
}

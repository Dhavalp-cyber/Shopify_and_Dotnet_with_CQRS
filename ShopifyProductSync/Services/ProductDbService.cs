using Microsoft.EntityFrameworkCore;
using ShopifyProductSync.Data;
using ShopifyProductSync.Models;

namespace ShopifyProductSync.Services
{
    /// <summary>
    /// Handles all database operations for products.
    /// This service saves, retrieves, and checks products in the local database.
    /// </summary>
    public class ProductDbService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProductDbService> _logger;

        public ProductDbService(AppDbContext context, ILogger<ProductDbService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Saves a product to the database.
        /// Returns the saved product with its local database ID.
        /// </summary>
        public async Task<Product> SaveProductAsync(Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Product saved to database. LocalId: {Id}, ShopifyId: {ShopifyId}",
                product.Id, product.ShopifyProductId);

            return product;
        }

        /// <summary>
        /// Returns all products from the database.
        /// </summary>
        public async Task<List<Product>> GetAllProductsAsync()
        {
            return await _context.Products
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Returns a single product by its local database ID.
        /// Returns null if not found.
        /// </summary>
        public async Task<Product?> GetProductByIdAsync(int id)
        {
            return await _context.Products.FindAsync(id);
        }

        /// <summary>
        /// Checks if a product with the given Shopify product ID already exists.
        /// Used to prevent duplicate inserts from webhooks.
        /// </summary>
        public async Task<bool> ProductExistsByShopifyIdAsync(long shopifyProductId)
        {
            return await _context.Products
                .AnyAsync(p => p.ShopifyProductId == shopifyProductId);
        }

        /// <summary>
        /// Finds a product by its Shopify inventory item ID.
        /// Used by the inventory_levels/update webhook to locate the right local record.
        /// Returns null if not found.
        /// </summary>
        public async Task<Product?> GetProductByInventoryItemIdAsync(long inventoryItemId)
        {
            return await _context.Products
                .FirstOrDefaultAsync(p => p.ShopifyInventoryItemId == inventoryItemId);
        }

        /// <summary>
        /// Saves changes to an existing product record.
        /// Used after modifying inventory fields from a webhook.
        /// </summary>
        public async Task UpdateProductAsync(Product product)
        {
            _context.Products.Update(product);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Product updated in database. LocalId: {Id}, ShopifyId: {ShopifyId}",
                product.Id, product.ShopifyProductId);
        }
    }
}

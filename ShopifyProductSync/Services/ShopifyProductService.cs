using ShopifySharp;
using ShopifyProductSync.DTOs;

namespace ShopifyProductSync.Services
{
    /// <summary>
    /// Handles all communication with Shopify using ShopifySharp.
    /// This service creates products in Shopify and returns the result.
    /// </summary>
    public class ShopifyProductService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ShopifyProductService> _logger;

        // Read Shopify settings from appsettings.json
        private readonly string _shopUrl;
        private readonly string _accessToken;

        public ShopifyProductService(IConfiguration configuration, ILogger<ShopifyProductService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            _shopUrl = _configuration["Shopify:ShopUrl"]
                ?? throw new InvalidOperationException("Shopify:ShopUrl is not configured in appsettings.json");

            _accessToken = _configuration["Shopify:AccessToken"]
                ?? throw new InvalidOperationException("Shopify:AccessToken is not configured in appsettings.json");
        }

        /// <summary>
        /// Creates a product in Shopify using ShopifySharp.
        /// Returns the created Shopify product (which includes the Shopify product ID).
        /// </summary>
        public async Task<ShopifySharp.Product> CreateProductInShopifyAsync(CreateProductDto dto)
        {
            _logger.LogInformation("Creating product in Shopify: {Title}", dto.Title);

            // Create the ShopifySharp ProductService
            var productService = new ProductService(_shopUrl, _accessToken);

            // Build the Shopify product object
            var shopifyProduct = new ShopifySharp.Product
            {
                Title = dto.Title,
                Vendor = dto.Vendor,
                ProductType = dto.ProductType,
                Status = dto.Status,

                // Shopify requires at least one variant with a price
                Variants = new List<ProductVariant>
                {
                    new ProductVariant
                    {
                        Price = dto.Price,
                        InventoryManagement = null // not tracking inventory for now
                    }
                }
            };

            // Call Shopify API to create the product
            var createdProduct = await productService.CreateAsync(shopifyProduct);

            _logger.LogInformation("Product created in Shopify with ID: {ShopifyId}", createdProduct.Id);

            return createdProduct;
        }
    }
}

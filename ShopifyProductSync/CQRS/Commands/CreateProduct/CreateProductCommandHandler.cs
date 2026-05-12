using MediatR;
using Microsoft.Extensions.Logging;
using ShopifyProductSync.DTOs;
using ShopifyProductSync.Models;
using ShopifyProductSync.Services;

namespace ShopifyProductSync.CQRS.Commands.CreateProduct
{
    /// <summary>
    /// Handles the CreateProductCommand.
    /// Creates a product in Shopify via ShopifySharp, then saves it to the local database.
    /// Zero business logic change from the original ProductsController.CreateProduct action.
    /// </summary>
    public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, CreateProductResult>
    {
        private readonly ShopifyProductService _shopifyProductService;
        private readonly ProductDbService _productDbService;
        private readonly ILogger<CreateProductCommandHandler> _logger;

        public CreateProductCommandHandler(
            ShopifyProductService shopifyProductService,
            ProductDbService productDbService,
            ILogger<CreateProductCommandHandler> logger)
        {
            _shopifyProductService = shopifyProductService;
            _productDbService = productDbService;
            _logger = logger;
        }

        public async Task<CreateProductResult> Handle(
            CreateProductCommand command,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Received create product request for: {Title}", command.Title);

            // Map command to DTO for the existing service
            var dto = new CreateProductDto
            {
                Title = command.Title,
                Vendor = command.Vendor,
                ProductType = command.ProductType,
                Status = command.Status,
                Price = command.Price
            };

            // Step 1: Create product in Shopify using ShopifySharp
            var shopifyProduct = await _shopifyProductService.CreateProductInShopifyAsync(dto);

            // Step 2: Get the price from the first variant Shopify returned
            var price = shopifyProduct.Variants?.FirstOrDefault()?.Price ?? dto.Price;

            // Step 3: Build the local Product entity
            // PostgreSQL requires DateTimeKind.Utc — we use SpecifyKind to ensure that
            var createdAt = shopifyProduct.CreatedAt?.UtcDateTime ?? DateTime.UtcNow;
            var updatedAt = shopifyProduct.UpdatedAt?.UtcDateTime ?? DateTime.UtcNow;

            var product = new Product
            {
                ShopifyProductId = shopifyProduct.Id ?? 0,
                Title = shopifyProduct.Title ?? dto.Title,
                Vendor = shopifyProduct.Vendor ?? dto.Vendor,
                Status = shopifyProduct.Status ?? dto.Status,
                ProductType = shopifyProduct.ProductType ?? dto.ProductType,
                Price = price,
                CreatedAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(updatedAt, DateTimeKind.Utc),
                Source = "Api"
            };

            // Step 4: Save to local database
            var savedProduct = await _productDbService.SaveProductAsync(product);

            // Step 5: Return result
            return new CreateProductResult
            {
                Message = "Product created successfully in Shopify and saved to database.",
                LocalDatabaseId = savedProduct.Id,
                ShopifyProductId = savedProduct.ShopifyProductId,
                Title = savedProduct.Title,
                Source = savedProduct.Source
            };
        }
    }
}

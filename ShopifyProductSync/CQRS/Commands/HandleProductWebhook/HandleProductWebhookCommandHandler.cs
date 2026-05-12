using MediatR;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using ShopifyProductSync.DTOs;
using ShopifyProductSync.Models;
using ShopifyProductSync.Services;

namespace ShopifyProductSync.CQRS.Commands.HandleProductWebhook
{
    /// <summary>
    /// Handles the HandleProductWebhookCommand.
    /// Verifies HMAC, parses the webhook payload, checks for duplicates, and saves to DB.
    /// Zero business logic change from the original ShopifyWebhookController.ProductCreated action.
    /// </summary>
    public class HandleProductWebhookCommandHandler : IRequestHandler<HandleProductWebhookCommand, HandleProductWebhookResult>
    {
        private readonly ShopifyWebhookService _webhookService;
        private readonly ProductDbService _productDbService;
        private readonly ILogger<HandleProductWebhookCommandHandler> _logger;

        public HandleProductWebhookCommandHandler(
            ShopifyWebhookService webhookService,
            ProductDbService productDbService,
            ILogger<HandleProductWebhookCommandHandler> logger)
        {
            _webhookService = webhookService;
            _productDbService = productDbService;
            _logger = logger;
        }

        public async Task<HandleProductWebhookResult> Handle(
            HandleProductWebhookCommand command,
            CancellationToken cancellationToken)
        {
            // Step 1: Verify HMAC signature
            var isValid = _webhookService.IsValidWebhook(command.RawBodyBytes, command.HmacHeader);
            if (!isValid)
            {
                _logger.LogWarning("Invalid HMAC signature. Rejecting webhook.");
                return new HandleProductWebhookResult
                {
                    HmacValid = false,
                    Message = "Invalid HMAC signature."
                };
            }

            _logger.LogInformation("HMAC verification passed.");

            // Step 2: Parse the raw body JSON into our DTO
            var rawBodyString = Encoding.UTF8.GetString(command.RawBodyBytes);
            var webhookDto = JsonSerializer.Deserialize<ShopifyProductWebhookDto>(rawBodyString);

            if (webhookDto == null || webhookDto.Id == 0)
            {
                _logger.LogWarning("Could not parse webhook body or product ID is missing.");
                return new HandleProductWebhookResult
                {
                    HmacValid = true,
                    Message = "Invalid webhook payload."
                };
            }

            _logger.LogInformation("Webhook product ID: {ShopifyId}, Title: {Title}",
                webhookDto.Id, webhookDto.Title);

            // Step 3: Check for duplicate — avoid saving the same product twice
            var alreadyExists = await _productDbService.ProductExistsByShopifyIdAsync(webhookDto.Id);
            if (alreadyExists)
            {
                _logger.LogInformation("Product with ShopifyId {ShopifyId} already exists. Skipping.", webhookDto.Id);
                return new HandleProductWebhookResult
                {
                    HmacValid = true,
                    AlreadyExists = true,
                    Message = "Product already exists. Skipped duplicate."
                };
            }

            // Step 4: Extract price from first variant (Shopify sends variants array)
            decimal price = 0;
            if (webhookDto.Variants != null && webhookDto.Variants.Count > 0)
            {
                decimal.TryParse(webhookDto.Variants[0].Price, out price);
            }

            // Step 5: Build the Product entity
            // PostgreSQL requires DateTimeKind.Utc — force it with SpecifyKind
            var createdAt = webhookDto.CreatedAt == default ? DateTime.UtcNow : webhookDto.CreatedAt;
            var updatedAt = webhookDto.UpdatedAt == default ? DateTime.UtcNow : webhookDto.UpdatedAt;

            var product = new Product
            {
                ShopifyProductId = webhookDto.Id,
                Title = webhookDto.Title,
                Vendor = webhookDto.Vendor,
                Status = webhookDto.Status,
                ProductType = webhookDto.ProductType,
                Price = price,
                CreatedAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(updatedAt, DateTimeKind.Utc),
                Source = "ShopifyWebhook"
            };

            // Step 6: Save to database
            var savedProduct = await _productDbService.SaveProductAsync(product);

            _logger.LogInformation("Webhook product saved. LocalId: {Id}", savedProduct.Id);

            return new HandleProductWebhookResult
            {
                HmacValid = true,
                AlreadyExists = false,
                Message = "Webhook received and product saved.",
                LocalId = savedProduct.Id
            };
        }
    }
}

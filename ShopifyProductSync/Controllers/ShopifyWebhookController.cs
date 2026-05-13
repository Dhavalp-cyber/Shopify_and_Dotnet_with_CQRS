using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopifyProductSync.CQRS.Commands.HandleProductWebhook;
using ShopifyProductSync.DTOs;
using ShopifyProductSync.Services;
using System.Text.Json;

namespace ShopifyProductSync.Controllers
{
    /// <summary>
    /// Handles incoming Shopify webhooks.
    /// 
    /// Endpoint:
    ///   POST /api/shopify/webhooks/products-create
    /// 
    /// Flow:
    ///   1. Shopify sends product JSON to this endpoint
    ///   2. We read the raw request body (needed for HMAC verification)
    ///   3. We verify the HMAC signature using X-Shopify-Hmac-Sha256 header
    ///   4. If valid, we parse the product data
    ///   5. We check if product already exists (avoid duplicates)
    ///   6. We save the product to the database
    ///   7. We return 200 OK quickly (Shopify expects fast response)
    /// </summary>
    [ApiController]
    [Route("api/shopify/webhooks")]
    public class ShopifyWebhookController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ShopifyWebhookController> _logger;
        private readonly ShopifyWebhookService _webhookService;
        private readonly ProductDbService _productDbService;

        public ShopifyWebhookController(
            IMediator mediator,
            ILogger<ShopifyWebhookController> logger,
            ShopifyWebhookService webhookService,
            ProductDbService productDbService)
        {
            _mediator = mediator;
            _logger = logger;
            _webhookService = webhookService;
            _productDbService = productDbService;
        }

        /// <summary>
        /// Receives Shopify product/create webhook.
        /// Shopify sends this when a new product is created in Shopify Admin.
        /// </summary>
        [HttpPost("products-create")]
        public async Task<IActionResult> ProductCreated()
        {
            _logger.LogInformation("Received Shopify product/create webhook.");

            // Step 1: Read the raw request body as bytes
            // IMPORTANT: We must read raw bytes BEFORE any JSON parsing
            // because HMAC verification needs the exact original bytes
            byte[] rawBodyBytes;
            using (var memoryStream = new MemoryStream())
            {
                await Request.Body.CopyToAsync(memoryStream);
                rawBodyBytes = memoryStream.ToArray();
            }

            // Step 2: Get the HMAC header Shopify sent
            var hmacHeader = Request.Headers["X-Shopify-Hmac-Sha256"].FirstOrDefault() ?? string.Empty;

            try
            {
                var command = new HandleProductWebhookCommand
                {
                    RawBodyBytes = rawBodyBytes,
                    HmacHeader = hmacHeader
                };

                var result = await _mediator.Send(command);

                // HMAC failed — reject
                if (!result.HmacValid)
                    return Unauthorized(new { message = result.Message });

                // Payload was invalid
                if (!result.AlreadyExists && result.LocalId == null && result.HmacValid)
                    return BadRequest(new { message = result.Message });

                // Duplicate — still return 200 OK (Shopify will retry on non-200)
                if (result.AlreadyExists)
                    return Ok(new { message = result.Message });

                // Success
                return Ok(new { message = result.Message, localId = result.LocalId });
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse webhook JSON body.");
                return BadRequest(new { message = "Invalid JSON in webhook body." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing webhook.");
                // Return 200 anyway to prevent Shopify from retrying endlessly
                return Ok(new { message = "Webhook received but processing failed internally." });
            }
        }

        /// <summary>
        /// Receives Shopify inventory_levels/update webhook.
        /// Shopify sends this automatically whenever inventory is updated at any location.
        /// This keeps our local database in sync with Shopify inventory.
        ///
        /// Register this webhook in Shopify Admin:
        ///   Topic: inventory_levels/update
        ///   URL:   https://your-domain/api/shopify/webhooks/inventory-update
        /// </summary>
        [HttpPost("inventory-update")]
        public async Task<IActionResult> InventoryUpdated()
        {
            _logger.LogInformation("Received Shopify inventory_levels/update webhook.");

            // Read raw body bytes first (needed for HMAC verification)
            byte[] rawBodyBytes;
            using (var memoryStream = new MemoryStream())
            {
                await Request.Body.CopyToAsync(memoryStream);
                rawBodyBytes = memoryStream.ToArray();
            }

            var hmacHeader = Request.Headers["X-Shopify-Hmac-Sha256"].FirstOrDefault() ?? string.Empty;

            // Verify HMAC signature
            if (!_webhookService.IsValidWebhook(rawBodyBytes, hmacHeader))
            {
                _logger.LogWarning("Invalid HMAC on inventory webhook. Rejecting.");
                return Unauthorized(new { message = "Invalid HMAC signature." });
            }

            try
            {
                var rawBodyString = System.Text.Encoding.UTF8.GetString(rawBodyBytes);

                var payload = JsonSerializer.Deserialize<ShopifyInventoryWebhookDto>(rawBodyString);

                if (payload == null || payload.InventoryItemId == 0)
                {
                    _logger.LogWarning("Could not parse inventory webhook body.");
                    return Ok(new { message = "Webhook received but payload was invalid." });
                }

                _logger.LogInformation(
                    "Inventory webhook — InventoryItemId: {ItemId}, LocationId: {LocationId}, Available: {Qty}",
                    payload.InventoryItemId, payload.LocationId, payload.Available);

                // Find the product in local DB by inventory item ID
                var product = await _productDbService.GetProductByInventoryItemIdAsync(payload.InventoryItemId);

                if (product == null)
                {
                    // Product not yet tracked locally — nothing to update
                    _logger.LogInformation(
                        "No local product found for InventoryItemId: {ItemId}. Skipping DB update.",
                        payload.InventoryItemId);
                    return Ok(new { message = "Inventory webhook received. Product not tracked locally." });
                }

                // Update inventory fields in local DB
                product.InventoryQuantity = payload.Available;
                product.ShopifyInventoryItemId = payload.InventoryItemId;
                product.ShopifyLocationId = payload.LocationId;
                product.UpdatedAt = DateTime.UtcNow;

                await _productDbService.UpdateProductAsync(product);

                _logger.LogInformation(
                    "Local DB inventory updated via webhook — ShopifyProductId: {ProductId}, " +
                    "InventoryQuantity: {Qty}, LocationId: {LocationId}",
                    product.ShopifyProductId, payload.Available, payload.LocationId);

                return Ok(new { message = "Inventory webhook processed and local database updated." });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse inventory webhook JSON.");
                return Ok(new { message = "Webhook received but JSON parsing failed." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing inventory webhook.");
                return Ok(new { message = "Webhook received but processing failed internally." });
            }
        }
    }
}

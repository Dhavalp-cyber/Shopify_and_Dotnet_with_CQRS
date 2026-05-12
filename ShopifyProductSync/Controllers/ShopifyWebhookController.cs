using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopifyProductSync.CQRS.Commands.HandleProductWebhook;

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

        public ShopifyWebhookController(IMediator mediator, ILogger<ShopifyWebhookController> logger)
        {
            _mediator = mediator;
            _logger = logger;
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
    }
}

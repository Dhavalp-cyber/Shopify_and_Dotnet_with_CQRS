using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopifyProductSync.CQRS.Commands.UpdateInventory;
using ShopifyProductSync.DTOs;

namespace ShopifyProductSync.Controllers
{
    /// <summary>
    /// Handles inventory update operations.
    ///
    /// Endpoint:
    ///   POST /api/inventory/update
    ///
    /// Flow:
    ///   1. Validate request body.
    ///   2. Dispatch UpdateInventoryCommand via MediatR.
    ///   3. Handler calls Shopify GraphQL mutation, then updates local DB.
    ///   4. Return success or appropriate error response.
    /// </summary>
    [ApiController]
    [Route("api/inventory")]
    public class InventoryController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(IMediator mediator, ILogger<InventoryController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        /// <summary>
        /// Updates inventory for a specific Shopify location.
        /// The inventory item ID is resolved automatically from Shopify.
        ///
        /// Example request body:
        /// {
        ///   "shopifyProductId": 123456789,
        ///   "shopifyLocationId": 555555555,
        ///   "quantity": 25
        /// }
        /// </summary>
        [HttpPost("update")]
        public async Task<IActionResult> UpdateInventory([FromBody] UpdateInventoryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _logger.LogInformation(
                "POST /api/inventory/update — ShopifyProductId: {ProductId}, " +
                "LocationId: {LocationId}, Quantity: {Qty}",
                dto.ShopifyProductId,
                dto.ShopifyLocationId,
                dto.Quantity);

            try
            {
                var command = new UpdateInventoryCommand
                {
                    ShopifyProductId = dto.ShopifyProductId,
                    ShopifyLocationId = dto.ShopifyLocationId,
                    Quantity = dto.Quantity
                };

                var result = await _mediator.Send(command);

                return Ok(new
                {
                    message = result.Message,
                    shopifyProductId = result.ShopifyProductId,
                    shopifyLocationId = result.ShopifyLocationId,
                    updatedQuantity = result.UpdatedQuantity,
                    availableQuantityAfterUpdate = result.AvailableQuantityAfterUpdate
                });
            }
            catch (KeyNotFoundException ex)
            {
                // Product not found in local database
                _logger.LogWarning(ex, "Product not found during inventory update.");
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // Shopify API failure or unexpected error
                _logger.LogError(ex, "Error updating inventory for ShopifyProductId: {ProductId}",
                    dto.ShopifyProductId);
                return StatusCode(500, new { message = "Inventory update failed: " + ex.Message });
            }
        }
    }
}

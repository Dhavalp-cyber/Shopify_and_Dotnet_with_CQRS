using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopifyProductSync.CQRS.Commands.CreateProduct;
using ShopifyProductSync.CQRS.Queries.GetAllProductsFromShopify;
using ShopifyProductSync.CQRS.Queries.GetProductByShopifyId;
using ShopifyProductSync.DTOs;

namespace ShopifyProductSync.Controllers
{

    [ApiController]
    [Route("api/products")]
    public class ProductsController : ControllerBase
    {
        private readonly IMediator _mediator;
        //ILogger ASP.NET Core ka logging system 
        //what happan in code all things record
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(IMediator mediator, ILogger<ProductsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllProductsFromShopify()
        {
            try
            {
                // for tracking
                _logger.LogInformation("GET /api/products — fetching all products from Shopify via GraphQL.");

                var result = await _mediator.Send(new GetAllProductsFromShopifyQuery());

                return Ok(new
                {
                    totalCount = result.TotalCount,
                    source = result.Source,
                    products = result.Products
                });
            }
            catch (Exception ex)
            {
                //save error in logger
                _logger.LogError(ex, "Error fetching products from Shopify GraphQL.");
                //if server side erroe happan
                return StatusCode(500, new { message = "Failed to fetch products from Shopify: " + ex.Message });
            }
        }

        
        [HttpGet("shopify/{shopifyId:long}")]
        public async Task<IActionResult> GetProductByShopifyId(long shopifyId)
        {
            try
            {
                _logger.LogInformation("GET /api/products/shopify/{ShopifyId} — fetching from Shopify.", shopifyId);

                var product = await _mediator.Send(new GetProductByShopifyIdQuery { ShopifyId = shopifyId });

                if (product == null)
                    return NotFound(new { message = $"Product with Shopify ID {shopifyId} not found in Shopify." });

                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching product {ShopifyId} from Shopify.", shopifyId);
                return StatusCode(500, new { message = "Failed to fetch product from Shopify: " + ex.Message });
            }
        }

        /// <summary>
        /// Creates a product in Shopify and saves it to the local database.
        /// Source will be set to "Api".
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                _logger.LogInformation("Received create product request for: {Title}", dto.Title);

                var command = new CreateProductCommand
                {
                    Title = dto.Title,
                    Vendor = dto.Vendor,
                    ProductType = dto.ProductType,
                    Status = dto.Status,
                    Price = dto.Price
                };

                var result = await _mediator.Send(command);

                return Ok(new
                {
                    message = result.Message,
                    localDatabaseId = result.LocalDatabaseId,
                    shopifyProductId = result.ShopifyProductId,
                    title = result.Title,
                    source = result.Source
                });
            }
            catch (ShopifySharp.ShopifyException ex)
            {
                _logger.LogError(ex, "Shopify API error while creating product.");
                return StatusCode(500, new { message = "Shopify API error: " + ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating product.");
                return StatusCode(500, new { message = "Internal server error: " + ex.Message });
            }
        }
    }
}

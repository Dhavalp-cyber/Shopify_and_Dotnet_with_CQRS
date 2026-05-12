using MediatR;
using Microsoft.Extensions.Logging;
using ShopifyProductSync.DTOs;
using ShopifyProductSync.Services;

namespace ShopifyProductSync.CQRS.Queries.GetProductByShopifyId
{
    /// <summary>
    /// Handles the GetProductByShopifyIdQuery.
    /// Fetches a single product live from Shopify by its numeric Shopify Product ID via GraphQL.
    /// Zero business logic change from the original ProductsController.GetProductByShopifyId action.
    /// </summary>
    public class GetProductByShopifyIdQueryHandler
        : IRequestHandler<GetProductByShopifyIdQuery, ShopifyProductResponseDto?>
    {
        private readonly ShopifyGraphQLService _shopifyGraphQLService;
        private readonly ILogger<GetProductByShopifyIdQueryHandler> _logger;

        public GetProductByShopifyIdQueryHandler(
            ShopifyGraphQLService shopifyGraphQLService,
            ILogger<GetProductByShopifyIdQueryHandler> logger)
        {
            _shopifyGraphQLService = shopifyGraphQLService;
            _logger = logger;
        }

        public async Task<ShopifyProductResponseDto?> Handle(
            GetProductByShopifyIdQuery request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "GET /api/products/shopify/{ShopifyId} — fetching from Shopify.",
                request.ShopifyId);

            var product = await _shopifyGraphQLService.GetProductByShopifyIdAsync(request.ShopifyId);

            if (product == null)
                _logger.LogWarning("Product with Shopify ID {ShopifyId} not found.", request.ShopifyId);

            return product;
        }
    }
}

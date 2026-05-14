using MediatR;
using Microsoft.Extensions.Logging;
using ShopifyProductSync.Services;

namespace ShopifyProductSync.CQRS.Queries.GetAllProductsFromShopify
{

    /// Handles the GetAllProductsFromShopifyQuery
    /// Fetches all products live from Shopify via GraphQL with automatic pagination
    /// and return GetAllProductsFromShopifyResult

    public class GetAllProductsFromShopifyQueryHandler
        : IRequestHandler<GetAllProductsFromShopifyQuery, GetAllProductsFromShopifyResult>
    {
        private readonly ShopifyGraphQLService _shopifyGraphQLService;
        private readonly ILogger<GetAllProductsFromShopifyQueryHandler> _logger;

        public GetAllProductsFromShopifyQueryHandler(
            ShopifyGraphQLService shopifyGraphQLService,
            ILogger<GetAllProductsFromShopifyQueryHandler> logger)
        {
            _shopifyGraphQLService = shopifyGraphQLService;
            _logger = logger;
        }

        public async Task<GetAllProductsFromShopifyResult> Handle(
            GetAllProductsFromShopifyQuery request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("GET /api/products — fetching all products from Shopify via GraphQL.");

            var products = await _shopifyGraphQLService.GetAllProductsAsync();

            //data send to midiator send() method
            return new GetAllProductsFromShopifyResult
            {
                TotalCount = products.Count,
                Source = "Shopify GraphQL (live)",
                Products = products
            };
        }
    }
}

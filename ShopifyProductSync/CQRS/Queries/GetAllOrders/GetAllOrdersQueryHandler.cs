using MediatR;
using Microsoft.Extensions.Logging;
using ShopifyProductSync.Services;

namespace ShopifyProductSync.CQRS.Queries.GetAllOrders
{
    /// <summary>
    /// Handles the GetAllOrdersQuery.
    /// Fetches ALL orders live from Shopify via GraphQL with automatic pagination.
    /// Delegates all Shopify communication to ShopifyFulfillmentService.
    /// </summary>
    public class GetAllOrdersQueryHandler
        : IRequestHandler<GetAllOrdersQuery, GetAllOrdersResult>
    {
        private readonly ShopifyFulfillmentService _fulfillmentService;
        private readonly ILogger<GetAllOrdersQueryHandler> _logger;

        public GetAllOrdersQueryHandler(
            ShopifyFulfillmentService fulfillmentService,
            ILogger<GetAllOrdersQueryHandler> logger)
        {
            _fulfillmentService = fulfillmentService;
            _logger = logger;
        }

        public async Task<GetAllOrdersResult> Handle(
            GetAllOrdersQuery request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "GET /api/orders — fetching all orders from Shopify via GraphQL.");

            var orders = await _fulfillmentService.GetAllOrdersAsync();

            _logger.LogInformation(
                "All orders fetch complete — TotalOrders: {Total}", orders.Count);

            return new GetAllOrdersResult
            {
                TotalOrders = orders.Count,
                Source = "Shopify GraphQL (live)",
                Orders = orders
            };
        }
    }
}

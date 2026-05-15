using MediatR;
using Microsoft.Extensions.Logging;
using ShopifyProductSync.DTOs;
using ShopifyProductSync.Services;

namespace ShopifyProductSync.CQRS.Queries.GetUnfulfilledOrders
{
    /// <summary>
    /// Handles the GetUnfulfilledOrdersQuery.
    /// Fetches all UNFULFILLED and PARTIALLY_FULFILLED orders live from Shopify via GraphQL.
    /// Delegates all Shopify communication to ShopifyFulfillmentService.
    /// </summary>
    public class GetUnfulfilledOrdersQueryHandler
        : IRequestHandler<GetUnfulfilledOrdersQuery, UnfulfilledOrdersResponseDto>
    {
        private readonly ShopifyFulfillmentService _fulfillmentService;
        private readonly ILogger<GetUnfulfilledOrdersQueryHandler> _logger;

        public GetUnfulfilledOrdersQueryHandler(
            ShopifyFulfillmentService fulfillmentService,
            ILogger<GetUnfulfilledOrdersQueryHandler> logger)
        {
            _fulfillmentService = fulfillmentService;
            _logger = logger;
        }

        public async Task<UnfulfilledOrdersResponseDto> Handle(
            GetUnfulfilledOrdersQuery request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "GET /api/orders/unfulfilled-with-items — fetching unfulfilled orders from Shopify via GraphQL.");

            var result = await _fulfillmentService.GetUnfulfilledOrdersAsync();

            _logger.LogInformation(
                "Unfulfilled orders fetch complete — TotalOrders: {Total}", result.TotalOrders);

            return result;
        }
    }
}

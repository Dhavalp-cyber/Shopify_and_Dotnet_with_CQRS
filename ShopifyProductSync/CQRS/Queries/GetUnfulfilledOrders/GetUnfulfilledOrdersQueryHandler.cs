using MediatR;
using ShopifyProductSync.Services;

namespace ShopifyProductSync.CQRS.Queries.GetUnfulfilledOrders
{
    /// <summary>
    /// Handles GetUnfulfilledOrdersQuery.
    /// Delegates to ShopifyOrderService to fetch unfulfilled/partially fulfilled orders.
    /// </summary>
    public class GetUnfulfilledOrdersQueryHandler
        : IRequestHandler<GetUnfulfilledOrdersQuery, GetUnfulfilledOrdersResult>
    {
        private readonly ShopifyOrderService _shopifyOrderService;
        private readonly ILogger<GetUnfulfilledOrdersQueryHandler> _logger;

        public GetUnfulfilledOrdersQueryHandler(
            ShopifyOrderService shopifyOrderService,
            ILogger<GetUnfulfilledOrdersQueryHandler> logger)
        {
            _shopifyOrderService = shopifyOrderService;
            _logger = logger;
        }

        public async Task<GetUnfulfilledOrdersResult> Handle(
            GetUnfulfilledOrdersQuery request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Handling GetUnfulfilledOrdersQuery — fetching unfulfilled orders from Shopify.");

            var orders = await _shopifyOrderService.GetUnfulfilledOrdersAsync();

            _logger.LogInformation(
                "GetUnfulfilledOrdersQuery complete — Total orders: {Count}", orders.Count);

            return new GetUnfulfilledOrdersResult
            {
                TotalOrders = orders.Count,
                Orders = orders
            };
        }
    }
}

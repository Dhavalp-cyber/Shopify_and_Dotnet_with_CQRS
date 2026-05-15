using MediatR;
using ShopifyProductSync.DTOs;

namespace ShopifyProductSync.CQRS.Queries.GetUnfulfilledOrders
{
    /// <summary>
    /// Query to fetch all Shopify orders that are unfulfilled or partially fulfilled.
    /// No parameters needed — fetches all such orders from Shopify GraphQL.
    /// </summary>
    public class GetUnfulfilledOrdersQuery : IRequest<GetUnfulfilledOrdersResult>
    {
    }

    /// <summary>
    /// Result containing all unfulfilled/partially fulfilled orders with their line items.
    /// </summary>
    public class GetUnfulfilledOrdersResult
    {
        public int TotalOrders { get; set; }
        public List<UnfulfilledOrderDto> Orders { get; set; } = new();
    }
}

using MediatR;
using ShopifyProductSync.DTOs;

namespace ShopifyProductSync.CQRS.Queries.GetAllOrders
{
    /// <summary>
    /// Query to fetch ALL orders live from Shopify via GraphQL.
    /// Returns every order regardless of fulfillment or financial status.
    /// </summary>
    public class GetAllOrdersQuery : IRequest<GetAllOrdersResult>
    {
    }

    /// <summary>
    /// Result returned by GetAllOrdersQueryHandler.
    /// </summary>
    public class GetAllOrdersResult
    {
        public int TotalOrders { get; set; }
        public string Source { get; set; } = "Shopify GraphQL (live)";
        public List<OrderSummaryDto> Orders { get; set; } = new();
    }
}

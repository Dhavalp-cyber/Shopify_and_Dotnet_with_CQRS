using MediatR;
using ShopifyProductSync.DTOs;

namespace ShopifyProductSync.CQRS.Queries.GetUnfulfilledOrders
{
    /// <summary>
    /// Query to fetch all UNFULFILLED and PARTIALLY_FULFILLED orders live from Shopify via GraphQL.
    /// FULFILLED orders are excluded.
    /// For PARTIALLY_FULFILLED orders, ALL line items are returned (both fulfilled and pending).
    /// </summary>
    public class GetUnfulfilledOrdersQuery : IRequest<UnfulfilledOrdersResponseDto>
    {
    }
}

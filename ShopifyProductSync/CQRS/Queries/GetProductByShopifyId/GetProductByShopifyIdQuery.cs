using MediatR;
using ShopifyProductSync.DTOs;

namespace ShopifyProductSync.CQRS.Queries.GetProductByShopifyId
{
    /// <summary>
    /// Query to fetch a single product live from Shopify by its numeric Shopify Product ID.
    /// Returns null if the product is not found.
    /// </summary>
    public class GetProductByShopifyIdQuery : IRequest<ShopifyProductResponseDto?>
    {
        public long ShopifyId { get; set; }
    }
}

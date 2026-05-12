using MediatR;
using ShopifyProductSync.DTOs;

namespace ShopifyProductSync.CQRS.Queries.GetAllProductsFromShopify
{
    /// Query to fetch ALL products live from Shopify via GraphQL
    /// this request return GetAllProductsFromShopifyResult
    /// request object always laightwait
    public class GetAllProductsFromShopifyQuery : IRequest<GetAllProductsFromShopifyResult>
    {
    }


    /// Result containing all products fetched from Shopify
    /// store data and pack in object

    public class GetAllProductsFromShopifyResult
    {
        public int TotalCount { get; set; }
        public string Source { get; set; } = "Shopify GraphQL (live)";
        public List<ShopifyProductResponseDto> Products { get; set; } = new();
    }
}

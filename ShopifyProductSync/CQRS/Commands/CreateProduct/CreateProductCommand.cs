using MediatR;
using ShopifyProductSync.DTOs;

namespace ShopifyProductSync.CQRS.Commands.CreateProduct
{
    /// <summary>
    /// Command to create a product in Shopify and save it to the local database.
    /// Carries the CreateProductDto as the request payload.
    /// </summary>
    public class CreateProductCommand : IRequest<CreateProductResult>
    {
        public string Title { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
        public string Status { get; set; } = "active";
        public decimal Price { get; set; }
    }

    /// <summary>
    /// Result returned after a product is successfully created.
    /// </summary>
    public class CreateProductResult
    {
        public string Message { get; set; } = string.Empty;
        public int LocalDatabaseId { get; set; }
        public long ShopifyProductId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
    }
}

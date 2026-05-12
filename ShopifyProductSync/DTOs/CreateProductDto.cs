namespace ShopifyProductSync.DTOs
{
    /// <summary>
    /// DTO used when creating a product from our API (Swagger/Postman).
    /// This is the request body for POST /api/products/create
    /// </summary>
    public class CreateProductDto
    {
        public string Title { get; set; } = string.Empty;

        public string Vendor { get; set; } = string.Empty;

        // e.g. "Demo Product", "T-Shirt", etc.
        public string ProductType { get; set; } = string.Empty;

        // "active", "draft", or "archived"
        public string Status { get; set; } = "active";

        public decimal Price { get; set; }
    }
}

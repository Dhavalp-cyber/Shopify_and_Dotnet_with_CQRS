namespace ShopifyProductSync.DTOs
{

    public class UpdateInventoryDto
    {
        public long ShopifyProductId { get; set; }

        public long ShopifyLocationId { get; set; }

        public int Quantity { get; set; }
    }
}

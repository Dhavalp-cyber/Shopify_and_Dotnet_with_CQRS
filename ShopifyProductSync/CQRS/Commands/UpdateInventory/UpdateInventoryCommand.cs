using MediatR;

namespace ShopifyProductSync.CQRS.Commands.UpdateInventory
{

    public class UpdateInventoryCommand : IRequest<UpdateInventoryResult>
    {
        public long ShopifyProductId { get; set; }
        public long ShopifyLocationId { get; set; }
        public int Quantity { get; set; }
    }

    public class UpdateInventoryResult
    {
        public string Message { get; set; } = string.Empty;
        public long ShopifyProductId { get; set; }
        public long ShopifyLocationId { get; set; }
        public int UpdatedQuantity { get; set; }
        public int AvailableQuantityAfterUpdate { get; set; }
    }
}

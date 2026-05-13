using MediatR;

namespace ShopifyProductSync.CQRS.Commands.UpdateInventory
{
    /// <summary>
    /// Command to update inventory for a specific Shopify location.
    /// Triggers a Shopify GraphQL mutation and then updates the local DB record.
    /// The inventory item ID is resolved automatically inside the handler.
    /// </summary>
    public class UpdateInventoryCommand : IRequest<UpdateInventoryResult>
    {
        public long ShopifyProductId { get; set; }
        public long ShopifyLocationId { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>Result returned after inventory is successfully updated.</summary>
    public class UpdateInventoryResult
    {
        public string Message { get; set; } = string.Empty;
        public long ShopifyProductId { get; set; }
        public long ShopifyLocationId { get; set; }
        public int UpdatedQuantity { get; set; }
        public int AvailableQuantityAfterUpdate { get; set; }
    }
}

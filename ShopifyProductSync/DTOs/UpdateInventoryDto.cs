namespace ShopifyProductSync.DTOs
{
    /// <summary>
    /// Request body for POST /api/inventory/update.
    /// The inventory item ID is resolved automatically from Shopify using the product ID.
    /// </summary>
    public class UpdateInventoryDto
    {
        /// <summary>Numeric Shopify product ID (used to locate the local DB record and fetch inventory item ID).</summary>
        public long ShopifyProductId { get; set; }

        /// <summary>Shopify location ID where inventory should be updated.</summary>
        public long ShopifyLocationId { get; set; }

        /// <summary>New absolute inventory quantity to set at the given location.</summary>
        public int Quantity { get; set; }
    }
}

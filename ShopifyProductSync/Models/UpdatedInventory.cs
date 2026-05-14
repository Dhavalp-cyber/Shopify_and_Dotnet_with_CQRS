namespace ShopifyProductSync.Models
{
    /// <summary>
    /// Stores the history of every inventory update made through the API.
    /// Every time POST /api/inventory/update is called successfully,
    /// one record is inserted here.
    ///
    /// Relations:
    ///   - ProductId  → Product.Id   (FK — links to the product that was updated)
    ///   - LocationId → Location.Id  (FK — links to the location where inventory was updated)
    ///
    /// Snapshot fields (ProductName, ShopifyLocationId, LocationName):
    ///   These are saved as plain values at the time of update.
    ///   Even if the product name or location name changes later in Shopify,
    ///   the history record will still show what name was used at update time.
    /// </summary>
    public class UpdatedInventory
    {
        public int Id { get; set; }

        // ── Foreign keys ──────────────────────────────────────────────────────

        // Foreign key → Product table
        public int ProductId { get; set; }

        // Foreign key → Location table
        public int LocationId { get; set; }

        // ── Snapshot values (saved at update time) ────────────────────────────

        // Shopify product ID at the time of this update
        public long ShopifyProductId { get; set; }

        // Product name at the time of this update
        public string ProductName { get; set; } = string.Empty;

        // Shopify location ID at the time of this update
        public long ShopifyLocationId { get; set; }

        // Location name at the time of this update
        public string LocationName { get; set; } = string.Empty;

        // ── Inventory values ──────────────────────────────────────────────────

        // The quantity value that was sent in the update request
        public int UpdatedQuantity { get; set; }

        // The actual available quantity in Shopify after the update
        public int AvailableQuantityAfterUpdate { get; set; }

        // When this update happened
        public DateTime UpdatedAt { get; set; }

        // ── Navigation properties ─────────────────────────────────────────────

        // Navigation property → the product that was updated
        public Product Product { get; set; } = null!;

        // Navigation property → the location where inventory was updated
        public Location Location { get; set; } = null!;
    }
}

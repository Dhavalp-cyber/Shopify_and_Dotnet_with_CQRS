namespace ShopifyProductSync.Models
{
    /// <summary>
    /// Represents a Shopify location/store in the local database.
    /// A location is a physical place (warehouse, store) where inventory is tracked.
    /// One location can have many inventory update history records.
    /// </summary>
    public class Location
    {
        public int Id { get; set; }

        // The location ID assigned by Shopify
        public long ShopifyLocationId { get; set; }

        // The name of the location (e.g. "Main Warehouse", "Store Front")
        public string LocationName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        // Navigation property — one Location has many UpdatedInventory records
        public ICollection<UpdatedInventory> UpdatedInventories { get; set; } = new List<UpdatedInventory>();
    }
}

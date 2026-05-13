using Microsoft.EntityFrameworkCore;
using ShopifyProductSync.CQRS.Common.Interfaces;
using ShopifyProductSync.Models;

namespace ShopifyProductSync.Data
{
    /// <summary>
    /// Entity Framework Core database context.
    /// This class connects our application to the database.
    /// </summary>
    public class AppDbContext : DbContext, IAppDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Existing table — no changes
        public DbSet<Product> Products { get; set; }

        // New table — stores Shopify locations/stores
        public DbSet<Location> Locations { get; set; }

        // New table — stores inventory update history
        public DbSet<UpdatedInventory> UpdatedInventories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Product (existing — no changes) ──────────────────────────────
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(p => p.Id);

                // ShopifyProductId must be unique — prevents duplicate inserts
                entity.HasIndex(p => p.ShopifyProductId).IsUnique();

                entity.Property(p => p.Title).IsRequired().HasMaxLength(500);
                entity.Property(p => p.Vendor).HasMaxLength(255);
                entity.Property(p => p.Status).HasMaxLength(50);
                entity.Property(p => p.ProductType).HasMaxLength(255);
                entity.Property(p => p.Price).HasColumnType("decimal(18,2)");
                entity.Property(p => p.Source).IsRequired().HasMaxLength(50);
            });

            // ── Location (new) ────────────────────────────────────────────────
            modelBuilder.Entity<Location>(entity =>
            {
                entity.HasKey(l => l.Id);

                // ShopifyLocationId must be unique — one row per Shopify location
                entity.HasIndex(l => l.ShopifyLocationId).IsUnique();

                entity.Property(l => l.LocationName).IsRequired().HasMaxLength(255);
            });

            // ── UpdatedInventory (new) ────────────────────────────────────────
            modelBuilder.Entity<UpdatedInventory>(entity =>
            {
                entity.HasKey(u => u.Id);

                // Snapshot string columns
                entity.Property(u => u.ProductName).IsRequired().HasMaxLength(500);
                entity.Property(u => u.LocationName).IsRequired().HasMaxLength(255);

                // Relation: many UpdatedInventory → one Product
                entity.HasOne(u => u.Product)
                      .WithMany(p => p.UpdatedInventories)
                      .HasForeignKey(u => u.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Relation: many UpdatedInventory → one Location
                entity.HasOne(u => u.Location)
                      .WithMany(l => l.UpdatedInventories)
                      .HasForeignKey(u => u.LocationId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}

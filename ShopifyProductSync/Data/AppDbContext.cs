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

        // This creates a "Products" table in the database
        public DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure the Product entity
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
        }
    }
}

using Microsoft.EntityFrameworkCore;
using ShopifyProductSync.Models;

namespace ShopifyProductSync.CQRS.Common.Interfaces
{
    /// <summary>
    /// Abstraction over AppDbContext used by CQRS handlers.
    /// Allows handlers to depend on an interface rather than the concrete EF context.
    /// </summary>
    public interface IAppDbContext
    {
        DbSet<Product> Products { get; }
        DbSet<Order> Orders { get; }
        DbSet<OrderNoteAttribute> OrderNoteAttributes { get; }

        Task<int> SaveChangesAsync(CancellationToken ct);
    }
}

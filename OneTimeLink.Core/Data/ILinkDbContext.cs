using Microsoft.EntityFrameworkCore;
using OneTimeLink.Core.Models;

namespace OneTimeLink.Core.Data
{
    public interface ILinkDbContext
    {
        DbSet<Link> Links { get; set; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
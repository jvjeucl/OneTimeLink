using OneTimeLink.Core.Data;
using OneTimeLink.Core.Models;
using Microsoft.EntityFrameworkCore;
using OneTimeLink.Core.Models;

namespace OneTimeLink.EntityFrameworkCore;

public class LinkDbContext : DbContext, ILinkDbContext
{
    public LinkDbContext(DbContextOptions<LinkDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<Link> Links { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Link>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            // Other configurations...
        });
    }
}
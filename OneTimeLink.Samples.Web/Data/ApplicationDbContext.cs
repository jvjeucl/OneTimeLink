using Microsoft.EntityFrameworkCore;
using OneTimeLink.Core.Data;
using OneTimeLink.Core.Models;

namespace OneTimeLink.Samples.Web.Data;

public class ApplicationDbContext : DbContext, ILinkDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<Link> Links { get; set; }
    
    public DbSet<User> Users { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure Link entity
        modelBuilder.Entity<Link>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.Property(e => e.Purpose).IsRequired();
            entity.Property(e => e.Token).IsRequired();
        });
        
        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            
            // Seed some sample users
            entity.HasData(
                new User { Id = Guid.Parse("d8566de3-b1a6-4a9b-b842-8e3887a82e41"), Name = "John Doe", Email = "john@example.com", IsEmailVerified = false },
                new User { Id = Guid.Parse("2c2afde0-2f32-4979-b1df-294e433d9b3e"), Name = "Jane Smith", Email = "jane@example.com", IsEmailVerified = true }
            );
        });
    }
}
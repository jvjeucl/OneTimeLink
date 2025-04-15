using OneTimeLink.Core.Models;

namespace OneTimeLink.Core.Data;

using Microsoft.EntityFrameworkCore;
using System;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<Link> OneTimeLinks { get; set; }
    
    // Add other DbSets for your application entities here
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure the OneTimeLink entity
        modelBuilder.Entity<Link>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // MySQL specific: specify the character set for string columns
            entity.Property(e => e.Token)
                .IsRequired()
                .HasMaxLength(64)
                .UseCollation("utf8mb4_general_ci");
                
            entity.HasIndex(e => e.Token).IsUnique();
            
            entity.Property(e => e.Purpose)
                .HasMaxLength(100)
                .UseCollation("utf8mb4_general_ci");
                
            entity.Property(e => e.UserId)
                .HasMaxLength(128)
                .UseCollation("utf8mb4_general_ci");
                
            // MySQL stores these as DATETIME
            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasColumnType("datetime");
                
            entity.Property(e => e.ExpiresAt)
                .IsRequired()
                .HasColumnType("datetime");
                
            // MySQL boolean is represented as TINYINT(1)
            entity.Property(e => e.IsUsed)
                .IsRequired()
                .HasColumnType("tinyint(1)");
        });
        
        // Configure other entities...
    }
    
    // Optional: MySQL-specific configuration
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // This is only used if you don't inject the DbContext with configured options
            // In production, you should always configure through DI
            // This is just a safeguard
            throw new InvalidOperationException("DbContext not configured. Use the constructor that takes options.");
        }
        
        // You can add MySQL-specific options here if needed
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneTimeLink.Core.Data;
using OneTimeLink.Core.Models;
using OneTimeLink.Core.Configurations;
using OneTimeLink.Core.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OneTimeLink.Tests
{
    public class LinkIntegrationTests
    {
        [Fact]
        public async Task GenerateAndUseLink_ShouldWork()
        {
            // Set up a service collection
            var services = new ServiceCollection();
            
            // Add DbContext using in-memory database
            var dbName = "TestDb_" + Guid.NewGuid().ToString();
            services.AddDbContext<TestLinkDbContext>(options =>
                options.UseInMemoryDatabase(dbName));
            
            // Register the context as ILinkDbContext
            services.AddScoped<ILinkDbContext>(provider => 
                provider.GetRequiredService<TestLinkDbContext>());
            
            // Configure options
            services.Configure<LinkOptions>(options => {
                options.DefaultExpiration = TimeSpan.FromMinutes(5);
                options.TokenLength = 32;
            });
            
            // Register service
            services.AddScoped<ILinkService, LinkService>();
            
            // Build the service provider
            using var serviceProvider = services.BuildServiceProvider();
            
            // Get service and context for testing
            var linkService = serviceProvider.GetRequiredService<ILinkService>();
            var context = serviceProvider.GetRequiredService<TestLinkDbContext>();
            
            // Test the full flow
            var link = await linkService.GenerateLinkAsync("https://example.com", "testuser", "test", TimeSpan.FromHours(1));
            Assert.NotNull(link);
            
            var token = link.Substring(link.LastIndexOf('/') + 1);
            
            // Verify the link exists in the database
            var dbLink = await context.Links.FirstOrDefaultAsync(l => l.Token == token);
            Assert.NotNull(dbLink);
            Assert.Equal("testuser", dbLink.UserId);
            Assert.Equal("test", dbLink.Purpose);
            Assert.False(dbLink.IsUsed);
            
            // Use the link
            var usedLink = await linkService.ValidateAndUseLinkAsync(token);
            Assert.NotNull(usedLink);
            Assert.Equal("testuser", usedLink.UserId);
            Assert.Equal("test", usedLink.Purpose);
            Assert.True(usedLink.IsUsed);
            
            // Try to use it again
            var reusedLink = await linkService.ValidateAndUseLinkAsync(token);
            Assert.Null(reusedLink);
        }
        
        [Fact]
        public async Task ExpiredLinks_ShouldBeInvalidated()
        {
            // Set up services
            var services = new ServiceCollection();
            var dbName = "TestDb_" + Guid.NewGuid().ToString();
            
            services.AddDbContext<TestLinkDbContext>(options =>
                options.UseInMemoryDatabase(dbName));
            
            services.AddScoped<ILinkDbContext>(provider => 
                provider.GetRequiredService<TestLinkDbContext>());
            
            services.Configure<LinkOptions>(options => {
                options.DefaultExpiration = TimeSpan.FromMilliseconds(50); // Very short expiration
            });
            
            services.AddScoped<ILinkService, LinkService>();
            
            using var serviceProvider = services.BuildServiceProvider();
            var linkService = serviceProvider.GetRequiredService<ILinkService>();
            
            // Generate a link with very short expiration
            var link = await linkService.GenerateLinkAsync("https://example.com", "testuser", "test", TimeSpan.FromMilliseconds(50));
            var token = link.Substring(link.LastIndexOf('/') + 1);
            
            // Wait for expiration
            await Task.Delay(100);
            
            // Try to use the expired link
            var usedLink = await linkService.ValidateAndUseLinkAsync(token);
            Assert.Null(usedLink);
        }
        
        [Fact]
        public async Task CleanupExpiredLinks_ShouldRemoveOnlyExpiredLinks()
        {
            // Set up services
            var services = new ServiceCollection();
            var dbName = "TestDb_" + Guid.NewGuid().ToString();
            
            services.AddDbContext<TestLinkDbContext>(options =>
                options.UseInMemoryDatabase(dbName));
            
            services.AddScoped<ILinkDbContext>(provider => 
                provider.GetRequiredService<TestLinkDbContext>());
            
            services.Configure<LinkOptions>(options => {
                options.DefaultExpiration = TimeSpan.FromHours(1);
            });
            
            services.AddScoped<ILinkService, LinkService>();
            
            using var serviceProvider = services.BuildServiceProvider();
            var linkService = serviceProvider.GetRequiredService<ILinkService>();
            var context = serviceProvider.GetRequiredService<TestLinkDbContext>();
            
            // Create an active link
            await linkService.GenerateLinkAsync("https://example.com", "user1", "active", TimeSpan.FromHours(1));
            
            // Create an expired link by directly manipulating the database
            var expiredLink = new Link
            {
                Id = Guid.NewGuid(),
                Token = "expired-token",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                ExpiresAt = DateTime.UtcNow.AddHours(-1),
                IsUsed = false,
                Purpose = "expired",
                UserId = "user2"
            };
            
            context.Links.Add(expiredLink);
            await context.SaveChangesAsync();
            
            // Verify we have 2 links before cleanup
            Assert.Equal(2, await context.Links.CountAsync());
            
            // Run cleanup
            await linkService.CleanupExpiredLinksAsync();
            
            // Verify only the expired link was removed
            Assert.Equal(1, await context.Links.CountAsync());
            Assert.False(await context.Links.AnyAsync(l => l.Token == "expired-token"));
            Assert.True(await context.Links.AnyAsync(l => l.Purpose == "active"));
        }
    }

    // Test implementation of ILinkDbContext
    /*public class TestLinkDbContext : DbContext, ILinkDbContext
    {
        public TestLinkDbContext(DbContextOptions<TestLinkDbContext> options)
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
            });
        }
    }*/
}
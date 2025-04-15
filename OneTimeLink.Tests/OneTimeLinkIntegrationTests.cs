using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneTimeLink.Core.Configurations;
using OneTimeLink.Core.Data;
using OneTimeLink.Core.Models;
using OneTimeLink.Core.Services;
using Xunit;

namespace OneTimeLink.Tests
{
    public class OneTimeLinkIntegrationTests
    {
        [Fact]
        public async Task GenerateAndUseLink_ShouldWork()
        {
            // Set up a service collection
            var services = new ServiceCollection();
            
            // Add your services
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid().ToString()));
            
            services.Configure<LinkOptions>(options => {
                options.DefaultExpiration = TimeSpan.FromMinutes(5);
            });
            
            services.AddScoped<LinkService>();
            
            // Build the service provider
            using var serviceProvider = services.BuildServiceProvider();
            
            // Get your service
            var linkService = serviceProvider.GetRequiredService<LinkService>();
            
            // Test the functionality
            var link = await linkService.GenerateLinkAsync("https://example.com", "testuser", "test", TimeSpan.FromHours(1));
            Assert.NotNull(link);
            
            var token = link.Substring(link.LastIndexOf('/') + 1);
            
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
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid().ToString()));
            
            services.Configure<LinkOptions>(options => {
                options.DefaultExpiration = TimeSpan.FromMilliseconds(50); // Very short expiration
            });
            
            services.AddScoped<LinkService>();
            
            using var serviceProvider = services.BuildServiceProvider();
            var linkService = serviceProvider.GetRequiredService<LinkService>();
            
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
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid().ToString()));
            
            services.Configure<LinkOptions>(options => {
                options.DefaultExpiration = TimeSpan.FromHours(1);
            });
            
            services.AddScoped<LinkService>();
            
            using var serviceProvider = services.BuildServiceProvider();
            var linkService = serviceProvider.GetRequiredService<LinkService>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            
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
}
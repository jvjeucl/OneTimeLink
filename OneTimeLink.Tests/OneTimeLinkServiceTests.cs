using Microsoft.EntityFrameworkCore;
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
    public class LinkServiceTests
    {
        private readonly DbContextOptions<TestLinkDbContext> _dbContextOptions;
        private readonly LinkOptions _options;
        private readonly IOptions<LinkOptions> _optionsWrapper;

        public LinkServiceTests()
        {
            // Create a fresh in-memory database for each test
            _dbContextOptions = new DbContextOptionsBuilder<TestLinkDbContext>()
                .UseInMemoryDatabase(databaseName: "LinkServiceTests_" + Guid.NewGuid().ToString())
                .Options;

            // Set up options
            _options = new LinkOptions
            {
                DefaultExpiration = TimeSpan.FromHours(1),
                TokenLength = 32
            };

            _optionsWrapper = Options.Create(_options);
        }

        [Fact]
        public async Task GenerateLinkAsync_ShouldCreateToken()
        {
            // Arrange
            using var context = new TestLinkDbContext(_dbContextOptions);
            var service = new LinkService(context, _optionsWrapper);
            
            var baseUrl = "https://example.com";
            var userId = "user123";
            var purpose = "password-reset";
            var expiration = TimeSpan.FromHours(2);

            // Act
            var link = await service.GenerateLinkAsync(baseUrl, userId, purpose, expiration);

            // Assert
            Assert.NotNull(link);
            Assert.StartsWith(baseUrl, link);

            var token = link.Substring(link.LastIndexOf('/') + 1);
            var linkInDb = await context.Links.FirstOrDefaultAsync(l => l.Token == token);
            
            Assert.NotNull(linkInDb);
            Assert.Equal(userId, linkInDb.UserId);
            Assert.Equal(purpose, linkInDb.Purpose);
            Assert.False(linkInDb.IsUsed);
            Assert.True(linkInDb.ExpiresAt > DateTime.UtcNow);
        }

        [Fact]
        public async Task ValidateAndUseLinkAsync_WithValidToken_ShouldMarkAsUsed()
        {
            // Arrange
            using var context = new TestLinkDbContext(_dbContextOptions);
            
            // Add a valid link directly to the database
            var validLink = new Link
            {
                Id = Guid.NewGuid(),
                Token = "valid-token",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsUsed = false,
                Purpose = "test-purpose",
                UserId = "user123"
            };
            
            context.Links.Add(validLink);
            await context.SaveChangesAsync();
            
            var service = new LinkService(context, _optionsWrapper);

            // Act
            var result = await service.ValidateAndUseLinkAsync("valid-token");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("user123", result.UserId);
            Assert.Equal("test-purpose", result.Purpose);
            Assert.True(result.IsUsed);
            
            // Verify the link is marked as used in the database
            var linkInDb = await context.Links.FirstOrDefaultAsync(l => l.Token == "valid-token");
            Assert.NotNull(linkInDb);
            Assert.True(linkInDb.IsUsed);
        }

        [Fact]
        public async Task ValidateAndUseLinkAsync_WithInvalidToken_ShouldReturnNull()
        {
            // Arrange
            using var context = new TestLinkDbContext(_dbContextOptions);
            var service = new LinkService(context, _optionsWrapper);

            // Act
            var result = await service.ValidateAndUseLinkAsync("invalid-token");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task TokenExistsAsync_ShouldReturnCorrectResult()
        {
            // Arrange
            using var context = new TestLinkDbContext(_dbContextOptions);
            
            // Add a valid link directly to the database
            context.Links.Add(new Link
            {
                Id = Guid.NewGuid(),
                Token = "existing-token",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsUsed = false,
                Purpose = "test-purpose",
                UserId = "user123"
            });
            await context.SaveChangesAsync();
            
            var service = new LinkService(context, _optionsWrapper);

            // Act & Assert
            Assert.True(await service.TokenExistsAsync("existing-token"));
            Assert.False(await service.TokenExistsAsync("non-existent-token"));
        }

        [Fact]
        public async Task CleanupExpiredLinksAsync_ShouldRemoveOnlyExpiredLinks()
        {
            // Arrange
            using var context = new TestLinkDbContext(_dbContextOptions);
            
            // Add expired and valid links directly to the database
            var expiredLink = new Link
            {
                Id = Guid.NewGuid(),
                Token = "expired-token",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                ExpiresAt = DateTime.UtcNow.AddHours(-1),
                IsUsed = false,
                Purpose = "expired",
                UserId = "user1"
            };

            var validLink = new Link
            {
                Id = Guid.NewGuid(),
                Token = "valid-token",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsUsed = false,
                Purpose = "valid",
                UserId = "user2"
            };
            
            context.Links.AddRange(expiredLink, validLink);
            await context.SaveChangesAsync();
            
            var service = new LinkService(context, _optionsWrapper);

            // Act
            await service.CleanupExpiredLinksAsync();

            // Assert
            var remainingLinks = await context.Links.ToListAsync();
            Assert.Single(remainingLinks);
            Assert.Equal("valid-token", remainingLinks[0].Token);
        }
    }

    // Test implementation of ILinkDbContext that uses a real DbContext
    public class TestLinkDbContext : DbContext, ILinkDbContext
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
    }
}
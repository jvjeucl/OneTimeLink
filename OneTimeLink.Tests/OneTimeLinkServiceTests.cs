using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using OneTimeLink.Core.Configurations;
using OneTimeLink.Core.Data;
using OneTimeLink.Core.Models;
using OneTimeLink.Core.Services;
using OneTimeLink.Core.Configurations;
using OneTimeLink.Core.Models;

namespace OneTimeLink.Tests;

public class OneTimeLinkServiceTests
{
    private readonly DbContextOptions<ApplicationDbContext> _dbContextOptions;
        private readonly LinkOptions _options;
        private readonly IOptions<LinkOptions> _optionsWrapper;

        public OneTimeLinkServiceTests()
        {
            // Set up in-memory database for testing
            _dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "OneTimeLinkTestDb_" + Guid.NewGuid())
                .Options;

            // Set up options
            _options = new LinkOptions
            {
                DefaultExpiration = TimeSpan.FromHours(1),
                TokenLength = 32
            };

            var mock = new Mock<IOptions<LinkOptions>>();
            mock.Setup(m => m.Value).Returns(_options);
            _optionsWrapper = mock.Object;
        }

        [Fact]
        public async Task GenerateLinkAsync_ShouldCreateToken()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var service = new LinkService(context, _optionsWrapper);
            var baseUrl = "https://example.com";
            var userId = "user123";
            var purpose = "password-reset";
            var expiration = TimeSpan.FromHours(24);
            // Act
            var link = await service.GenerateLinkAsync(baseUrl, userId, purpose, expiration);

            // Assert
            Assert.NotNull(link);
            Assert.StartsWith(baseUrl, link);

            var token = link.Substring(link.LastIndexOf('/') + 1);
            var dbEntry = await context.Links.FirstOrDefaultAsync(l => l.Token == token);
            
            Assert.NotNull(dbEntry);
            Assert.Equal(userId, dbEntry.UserId);
            Assert.Equal(purpose, dbEntry.Purpose);
            Assert.False(dbEntry.IsUsed);
            Assert.True(dbEntry.ExpiresAt > DateTime.UtcNow);
        }

        [Fact]
        public async Task ValidateAndUseLinkAsync_WithValidToken_ShouldMarkAsUsed()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var service = new LinkService(context, _optionsWrapper);
            
            // First create a token
            var link = await service.GenerateLinkAsync("https://example.com", "user123", "test-purpose", TimeSpan.FromHours(24));
            var token = link.Substring(link.LastIndexOf('/') + 1);

            // Act
            var result = await service.ValidateAndUseLinkAsync(token);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("user123", result.UserId);
            Assert.Equal("test-purpose", result.Purpose);
            Assert.True(result.IsUsed);

            // Verify the change was saved to the database
            var dbEntry = await context.Links.FirstOrDefaultAsync(l => l.Token == token);
            Assert.NotNull(dbEntry);
            Assert.True(dbEntry.IsUsed);
        }

        [Fact]
        public async Task ValidateAndUseLinkAsync_WithInvalidToken_ShouldReturnNull()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var service = new LinkService(context, _optionsWrapper);
            
            // Act
            var result = await service.ValidateAndUseLinkAsync("invalid-token");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ValidateAndUseLinkAsync_WithExpiredToken_ShouldReturnNull()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            
            // Create an expired token directly in the database
            var expiredLink = new Link
            {
                Id = Guid.NewGuid(),
                Token = "expired-token",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                ExpiresAt = DateTime.UtcNow.AddDays(-1),
                IsUsed = false,
                Purpose = "test",
                UserId = "user123"
            };
            
            context.Links.Add(expiredLink);
            await context.SaveChangesAsync();
            
            var service = new LinkService(context, _optionsWrapper);
            
            // Act
            var result = await service.ValidateAndUseLinkAsync("expired-token");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ValidateAndUseLinkAsync_WithAlreadyUsedToken_ShouldReturnNull()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var service = new LinkService(context, _optionsWrapper);
            
            // First create and use a token
            var link = await service.GenerateLinkAsync("https://example.com", "user123", "test-purpose", TimeSpan.FromHours(24));
            var token = link.Substring(link.LastIndexOf('/') + 1);
            await service.ValidateAndUseLinkAsync(token);
            
            // Act - try to use it again
            var result = await service.ValidateAndUseLinkAsync(token);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task CleanupExpiredLinksAsync_ShouldRemoveExpiredLinks()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            
            // Create some expired and valid links
            context.Links.AddRange(
                new Link
                {
                    Id = Guid.NewGuid(),
                    Token = "expired-1",
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    ExpiresAt = DateTime.UtcNow.AddHours(-1),
                    IsUsed = false,
                    Purpose = "test",
                    UserId = "user1"
                },
                new Link
                {
                    Id = Guid.NewGuid(),
                    Token = "expired-2",
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    ExpiresAt = DateTime.UtcNow.AddHours(-2),
                    IsUsed = false,
                    Purpose = "test",
                    UserId = "user2"
                },
                new Link
                {
                    Id = Guid.NewGuid(),
                    Token = "valid-1",
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    IsUsed = false,
                    Purpose = "test",
                    UserId = "user3"
                }
            );
            await context.SaveChangesAsync();
            
            var service = new LinkService(context, _optionsWrapper);
            
            // Act
            await service.CleanupExpiredLinksAsync();

            // Assert
            Assert.Equal(1, await context.Links.CountAsync());
            Assert.True(await context.Links.AnyAsync(l => l.Token == "valid-1"));
            Assert.False(await context.Links.AnyAsync(l => l.Token == "expired-1"));
            Assert.False(await context.Links.AnyAsync(l => l.Token == "expired-2"));
        }

        [Fact]
        public async Task TokenExistsAsync_ShouldReturnCorrectResult()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var service = new LinkService(context, _optionsWrapper);
            
            // Create a valid token
            var link = await service.GenerateLinkAsync("https://example.com", "user123", "test-purpose", TimeSpan.FromHours(24));
            var token = link.Substring(link.LastIndexOf('/') + 1);
            
            // Act & Assert
            Assert.True(await service.TokenExistsAsync(token));
            Assert.False(await service.TokenExistsAsync("non-existent-token"));
            
            // Use the token and verify it no longer exists
            await service.ValidateAndUseLinkAsync(token);
            Assert.False(await service.TokenExistsAsync(token));
        }
}
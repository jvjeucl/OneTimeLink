using OneTimeLink.Core.Models;

namespace OneTimeLink.Core.Services;

public interface ILinkService
{
    Task<string> GenerateLinkAsync(string baseUrl, string userId, string purpose, TimeSpan? expiration = null);
    Task<Link> ValidateAndUseLinkAsync(string token);
    Task<bool> TokenExistsAsync(string token);
    Task CleanupExpiredLinksAsync();
}
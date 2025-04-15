using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneTimeLink.Core.Configurations;
using OneTimeLink.Core.Data;
using OneTimeLink.Core.Models;

namespace OneTimeLink.Core.Services;

public class LinkService
{
    private readonly ApplicationDbContext _context;
    private readonly LinkOptions _options;
    public LinkService(ApplicationDbContext context, IOptions<LinkOptions> options)
    {
        _context = context;
        _options = options.Value;
    }
    
    public async Task<string> GenerateLinkAsync(string baseUrl, string userId, string purpose, TimeSpan expiration)
    {
        // Generate secure random token
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
        
        var link = new Link
        {
            Id = Guid.NewGuid(),
            Token = token,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(expiration),
            IsUsed = false,
            Purpose = purpose,
            UserId = userId
        };
    
        _context.Links.Add(link);
        await _context.SaveChangesAsync();
    
        return $"{baseUrl}/use-link/{token}";
    }

    public async Task<Link> ValidateAndUseLinkAsync(string token)
    {
        var link = await _context.Links
            .Where(l => l.Token == token && !l.IsUsed && l.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync();
        
        if (link == null)
        {
            return null; // Invalid or already used
        }
    
        // Mark as used
        link.IsUsed = true;
        await _context.SaveChangesAsync();
    
        return link;
    }
    
    // Cleanup expired tokens
    public async Task CleanupExpiredLinksAsync()
    {
        var expiredLinks = await _context.Links
            .Where(l => l.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();
        
        _context.Links.RemoveRange(expiredLinks);
        await _context.SaveChangesAsync();
    }
    
    // Method to check if a token exists without using it
    public async Task<bool> TokenExistsAsync(string token)
    {
        return await _context.Links
            .AnyAsync(l => l.Token == token && !l.IsUsed && l.ExpiresAt > DateTime.UtcNow);
    }
}
using System.Security.Cryptography;
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
    
    public string GenerateLink(string baseUrl, string userId, string purpose, TimeSpan expiration)
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
        
        _context.OneTimeLinks.Add(link);
        _context.SaveChanges();
        
        return $"{baseUrl}/use-link/{token}";
    }
    
    public Link ValidateAndUseLink(string token)
    {
        // Use transaction to prevent race conditions
        using var transaction = _context.Database.BeginTransaction();
        
        var link = _context.OneTimeLinks
            .Where(l => l.Token == token && !l.IsUsed && l.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefault();
            
        if (link == null)
        {
            return null; // Invalid or already used
        }
        
        // Mark as used
        link.IsUsed = true;
        _context.SaveChanges();
        transaction.Commit();
        
        return link;
    }
    
    // Cleanup expired tokens
    public void CleanupExpiredLinks()
    {
        var expiredLinks = _context.OneTimeLinks
            .Where(l => l.ExpiresAt < DateTime.UtcNow);
            
        _context.OneTimeLinks.RemoveRange(expiredLinks);
        _context.SaveChanges();
    }
}
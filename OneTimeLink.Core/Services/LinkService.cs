using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneTimeLink.Core.Data;
using OneTimeLink.Core.Models;
using OneTimeLink.Core.Configurations;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace OneTimeLink.Core.Services
{
    public class LinkService : ILinkService
    {
        private readonly ILinkDbContext _context;
        private readonly LinkOptions _options;

        public LinkService(ILinkDbContext context, IOptions<LinkOptions> options)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<string> GenerateLinkAsync(string baseUrl, string userId, string purpose, TimeSpan? expiration = null)
        {
            // Use provided expiration or default from options
            var linkExpiration = expiration ?? _options.DefaultExpiration;
            
            // Generate secure random token
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(_options.TokenLength))
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
                
            var link = new Link
            {
                Id = Guid.NewGuid(),
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(linkExpiration),
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
            // Use database provider-agnostic approach
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

        public async Task<bool> TokenExistsAsync(string token)
        {
            return await _context.Links
                .AnyAsync(l => l.Token == token && !l.IsUsed && l.ExpiresAt > DateTime.UtcNow);
        }

        public async Task CleanupExpiredLinksAsync()
        {
            var now = DateTime.UtcNow;
            var expiredLinks = await _context.Links
                .Where(l => l.ExpiresAt < now)
                .ToListAsync();
        
            if (expiredLinks.Any())
            {
                _context.Links.RemoveRange(expiredLinks);
                await _context.SaveChangesAsync();
            }
        }
    }
}
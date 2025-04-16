# OneTimeLink

![Build Status](https://github.com/yourusername/OneTimeLink/workflows/build/badge.svg)
![Tests Status](https://github.com/yourusername/OneTimeLink/workflows/tests/badge.svg)
[![NuGet](https://img.shields.io/nuget/v/OneTimeLink.svg)](https://www.nuget.org/packages/OneTimeLink/)

A robust, lightweight .NET library for creating and validating one-time use links. Perfect for password reset emails, email verification, secure invitations, and more.

## Features

- 🔒 Secure token generation
- ⏱️ Configurable expiration times
- 🚫 Automatic protection against reuse
- 🧹 Easy cleanup of expired tokens
- 🔌 Database agnostic with Entity Framework Core support
- 🧩 Simple integration with dependency injection

## Installation

### Core Package

```bash
dotnet add package OneTimeLink.Core
```

### Entity Framework Core Integration

```bash
dotnet add package OneTimeLink.EntityFrameworkCore
```

## Quick Start

### Setup with Entity Framework Core

```csharp
// In Program.cs or Startup.cs
services.AddLinkWithEfCore<LinkDbContext>(
    options => {
        options.DefaultExpiration = TimeSpan.FromHours(24);
    },
    dbOptions => dbOptions.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString)
    )
);
```

### Using the Link Service

```csharp
public class PasswordResetController : ControllerBase
{
    private readonly ILinkService _linkService;
    
    public PasswordResetController(ILinkService linkService)
    {
        _linkService = linkService;
    }
    
    [HttpPost]
    public async Task<IActionResult> RequestReset(string email)
    {
        var user = await _userService.FindByEmailAsync(email);
        if (user == null) return Ok(); // Don't reveal if email exists
        
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var resetLink = await _linkService.GenerateLinkAsync(baseUrl, user.Id, "password-reset");
        
        await _emailService.SendPasswordResetEmailAsync(email, resetLink);
        
        return Ok();
    }
    
    [HttpGet("reset/{token}")]
    public async Task<IActionResult> ValidateResetToken(string token)
    {
        var link = await _linkService.ValidateAndUseLinkAsync(token);
        if (link == null)
        {
            return BadRequest("Invalid or expired reset link");
        }
        
        // Token is valid and has been marked as used
        return View(new ResetPasswordViewModel { UserId = link.UserId, Token = token });
    }
}
```

## Database Setup

### Using your own DbContext

```csharp
public class ApplicationDbContext : DbContext, ILinkDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
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
            // Other configurations...
        });
    }
}
```

### Running Database Migrations

```bash
dotnet ef migrations add AddLinkTable
dotnet ef database update
```

## Advanced Configuration

### Custom Token Length

```csharp
services.AddLinkWithEfCore<LinkDbContext>(
    options => {
        options.DefaultExpiration = TimeSpan.FromDays(7);
        options.TokenLength = 64; // Longer tokens for higher security
    },
    dbOptions => dbOptions.UseSqlServer(connectionString)
);
```

### Custom Link URL Format

```csharp
var resetLink = await _linkService.GenerateLinkAsync(
    "https://myapp.com/custom-reset-page", 
    user.Id, 
    "password-reset",
    TimeSpan.FromHours(2) // Custom expiration
);
```

### Scheduled Cleanup

```csharp
// Use a background service or scheduled job to clean up expired links
public class LinkCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    
    public LinkCleanupService(IServiceProvider services)
    {
        _services = services;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _services.CreateScope())
            {
                var linkService = scope.ServiceProvider.GetRequiredService<ILinkService>();
                await linkService.CleanupExpiredLinksAsync();
            }
            
            // Run once per day
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
```

## License

This project is licensed under the MIT License - see the LICENSE file for details.
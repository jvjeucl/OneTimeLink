namespace OneTimeLink.Core.Models;

public class Link
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string? UserId { get; set; }
}
namespace OneTimeLink.Core.Configurations;

public class LinkOptions : ILinkOptions
{
    public string ConnectionString { get; set; }
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromHours(24);
    public int TokenLength { get; set; } = 32;
    // Other settings...
}
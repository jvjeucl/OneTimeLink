using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneTimeLink.Core.Configurations;
using OneTimeLink.Core.Data;
using OneTimeLink.Core.Services;

namespace OneTimeLink.Core.Extensions;

public static class LinkServiceExtensions
{
    public static IServiceCollection AddOneTimeLinkServices(
        this IServiceCollection services, 
        Action<LinkOptions> configureOptions)
    {
        services.Configure(configureOptions);
        
        services.AddDbContext<ApplicationDbContext>((provider, options) => 
        {
            var linkOptions = provider.GetRequiredService<IOptions<LinkOptions>>().Value;
            options.UseMySql(
                linkOptions.ConnectionString,
                ServerVersion.AutoDetect(linkOptions.ConnectionString),
                mysqlOptions => mysqlOptions.EnableRetryOnFailure()
            );
        });
        
        services.AddScoped<LinkService>();
        
        return services;
    }
}
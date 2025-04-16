using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneTimeLink.Core.Configurations;
using OneTimeLink.Core.Services;
using OneTimeLink.Core.Data;

namespace OneTimeLink.EntityFrameworkCore.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLink(
        this IServiceCollection services, 
        Action<LinkOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddScoped<ILinkService, LinkService>();
        return services;
    }

    public static IServiceCollection AddLinkWithEfCore<TContext>(
        this IServiceCollection services,
        Action<LinkOptions> configureOptions,
        Action<DbContextOptionsBuilder> dbOptionsAction) 
        where TContext : DbContext, ILinkDbContext
    {
        services.Configure(configureOptions);
        services.AddDbContext<TContext>(dbOptionsAction);
        services.AddScoped<ILinkDbContext>(sp => sp.GetRequiredService<TContext>());
        services.AddScoped<ILinkService, LinkService>();
        return services;
    }
}
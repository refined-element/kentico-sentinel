using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using RefinedElement.Kentico.Sentinel.XbyK.Configuration;
using RefinedElement.Kentico.Sentinel.XbyK.Notifications;
using RefinedElement.Kentico.Sentinel.XbyK.Services;

namespace RefinedElement.Kentico.Sentinel.XbyK.DependencyInjection;

/// <summary>
/// DI entry point. Call once in <c>Program.cs</c>:
/// <code>
/// builder.Services.AddKenticoSentinel(builder.Configuration);
/// </code>
/// Does NOT modify middleware — the consumer still controls <c>Program.cs</c> and must preserve
/// the Kentico trio ordering (<c>InitKentico → UseStaticFiles → UseKentico</c>).
/// </summary>
public static class SentinelServiceCollectionExtensions
{
    public static IServiceCollection AddKenticoSentinel(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = SentinelOptions.SectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<SentinelOptions>(configuration.GetSection(sectionName));
        RegisterSharedServices(services);
        return services;
    }

    /// <summary>
    /// Overload for callers who'd rather configure via a delegate than bind from configuration.
    /// </summary>
    public static IServiceCollection AddKenticoSentinel(
        this IServiceCollection services,
        Action<SentinelOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        RegisterSharedServices(services);
        return services;
    }

    private static void RegisterSharedServices(IServiceCollection services)
    {
        services.AddHttpClient();

        // Module installer is resolved in the RegisterModule callback, not by DI-constructor injection.
        services.AddScoped<SentinelModuleInstaller>();
        services.AddScoped<SentinelScanService>();
        services.AddScoped<ISentinelEventLogWriter, SentinelEventLogWriter>();
        services.AddScoped<ISentinelEmailDigestSender, SentinelEmailDigestSender>();

        services.AddOptions<SentinelOptions>();
    }
}

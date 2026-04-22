using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using RefinedElement.Kentico.Sentinel.XbyK.Acknowledgment;
using RefinedElement.Kentico.Sentinel.XbyK.Configuration;
using RefinedElement.Kentico.Sentinel.XbyK.Contact;
using RefinedElement.Kentico.Sentinel.XbyK.Notifications;
using RefinedElement.Kentico.Sentinel.XbyK.Retention;
using RefinedElement.Kentico.Sentinel.XbyK.Services;
using RefinedElement.Kentico.Sentinel.XbyK.SettingsOverride;

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

        // Installer lifetime: Singleton. Kentico resolves it once during startup from the root
        // provider (inside SentinelModule.OnInit) and keeps the reference for the full app
        // lifetime, so the service registration must not be scoped — otherwise scope validation
        // throws, or (without validation) we quietly leak a scoped instance.
        services.AddSingleton<SentinelModuleInstaller>();

        services.AddScoped<SentinelScanService>();
        services.AddScoped<ISentinelEventLogWriter, SentinelEventLogWriter>();
        services.AddScoped<ISentinelEmailDigestSender, SentinelEmailDigestSender>();
        services.AddScoped<ISentinelFindingAckService, SentinelFindingAckService>();

        // Retention is stateless: resolve-per-use is fine, and keeping it Transient avoids
        // coupling the scan-completion pipeline's scope lifetime to the trim pass. The scan
        // service is Scoped and resolves this inline when it fires a trim after each run.
        services.AddTransient<ISentinelRetentionService, SentinelRetentionService>();

        // Settings-override store — reads the single-row admin-UI override and layers it on top
        // of SentinelOptions via PostConfigure. Scoped because it holds an IInfoProvider<T>
        // dependency that Kentico resolves per-request; the applier is Scoped for the same reason.
        services.AddScoped<ISentinelSettingsOverrideStore, SentinelSettingsOverrideStore>();
        services.AddScoped<IPostConfigureOptions<SentinelOptions>, SentinelOptionsOverrideApplier>();

        // Typed HttpClient for the Refined Element quote intake. 30s timeout leaves headroom for
        // KDaaS cold-start while still bounding a hung dependency — the admin UI surface is
        // synchronous-feeling from the operator's perspective, so we'd rather fail fast than
        // leave them staring at a spinner.
        services.AddHttpClient<ISentinelContactService, SentinelContactService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            // Valid RFC 7231 User-Agent product token: `<name>/<version>` pair using the assembly
            // InformationalVersion (matches what SentinelScanService persists on each scan run).
            // A bare token like "KenticoSentinel-XbyK" (no slash + version) makes ParseAdd throw
            // FormatException at typed-client construction time, which would tank the whole DI
            // graph.
            client.DefaultRequestHeaders.UserAgent.Add(
                new System.Net.Http.Headers.ProductInfoHeaderValue(
                    productName: "KenticoSentinel-XbyK",
                    productVersion: SentinelVersion.Current));
        });
    }
}

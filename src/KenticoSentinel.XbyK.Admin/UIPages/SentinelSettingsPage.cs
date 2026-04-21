using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;

using Microsoft.Extensions.Options;

using RefinedElement.Kentico.Sentinel.Quoting;
using RefinedElement.Kentico.Sentinel.XbyK.Admin;
using RefinedElement.Kentico.Sentinel.XbyK.Admin.UIPages;
using RefinedElement.Kentico.Sentinel.XbyK.Configuration;

[assembly: UIPage(
    parentType: typeof(SentinelApplicationPage),
    slug: "settings",
    uiPageType: typeof(SentinelSettingsPage),
    name: "Settings",
    templateName: "@refinedelement/sentinel-admin/Settings",
    order: 400)]

namespace RefinedElement.Kentico.Sentinel.XbyK.Admin.UIPages;

/// <summary>
/// Read-only display of the effective <c>Sentinel</c> configuration. Values come from whatever
/// source won the <see cref="IOptions{T}"/> binding — appsettings.json, environment variables,
/// Azure App Service config, the delegate overload of <c>AddKenticoSentinel</c>. Deliberately
/// read-only: editing config here would require writing back to the source chain (appsettings
/// vs. env var vs. Key Vault reference) and there's no safe way to guess the right target.
/// Admins edit the source and redeploy; this page just surfaces what's currently loaded.
/// </summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public class SentinelSettingsPage(IOptions<SentinelOptions> options) : Page<SettingsClientProperties>
{
    public override Task<SettingsClientProperties> ConfigureTemplateProperties(SettingsClientProperties properties)
    {
        var opts = options.Value;
        properties.Enabled = opts.Enabled;
        properties.ExcludedChecks = opts.Checks.Excluded.ToArray();
        properties.RuntimeConnectionString = string.IsNullOrWhiteSpace(opts.RuntimeChecks.ConnectionString)
            ? "(empty — falling back to CMSConnectionString)"
            : "(configured — value redacted)";
        properties.StaleDays = opts.RuntimeChecks.StaleDays;
        properties.EventLogDays = opts.RuntimeChecks.EventLogDays;

        properties.EmailDigestEnabled = opts.EmailDigest.Enabled;
        properties.EmailDigestRecipients = opts.EmailDigest.Recipients.ToArray();
        properties.EmailDigestSeverityThreshold = opts.EmailDigest.SeverityThreshold;
        properties.EmailDigestOnlyWhenThresholdFindings = opts.EmailDigest.OnlyWhenThresholdFindings;

        properties.EventLogEnabled = opts.EventLogIntegration.Enabled;
        properties.EventLogSeverityThreshold = opts.EventLogIntegration.SeverityThreshold;
        properties.EventLogMaxEntriesPerScan = opts.EventLogIntegration.MaxEntriesPerScan;

        properties.ContactEndpoint = !string.IsNullOrWhiteSpace(opts.Contact.Endpoint)
            ? opts.Contact.Endpoint
            : QuoteClient.DefaultEndpoint;
        properties.ContactIncludeContextByDefault = opts.Contact.IncludeContextByDefault;

        // See SentinelDashboardPage.PopulateDashboard for the rationale — Kentico admin's
        // Scheduled Tasks deep-link changes across refreshes; linking to the admin root keeps
        // the instructional copy accurate across versions.
        properties.ScheduledTasksUrl = "/admin";

        return Task.FromResult(properties);
    }
}

public sealed class SettingsClientProperties : TemplateClientProperties
{
    public bool Enabled { get; set; }
    public string[] ExcludedChecks { get; set; } = Array.Empty<string>();

    public string RuntimeConnectionString { get; set; } = string.Empty;
    public int StaleDays { get; set; }
    public int EventLogDays { get; set; }

    public bool EmailDigestEnabled { get; set; }
    public string[] EmailDigestRecipients { get; set; } = Array.Empty<string>();
    public string EmailDigestSeverityThreshold { get; set; } = string.Empty;
    public bool EmailDigestOnlyWhenThresholdFindings { get; set; }

    public bool EventLogEnabled { get; set; }
    public string EventLogSeverityThreshold { get; set; } = string.Empty;
    public int EventLogMaxEntriesPerScan { get; set; }

    public string ContactEndpoint { get; set; } = string.Empty;
    public bool ContactIncludeContextByDefault { get; set; }

    public string ScheduledTasksUrl { get; set; } = string.Empty;
}

using CMS.DataEngine;
using CMS.Membership;
using CMS.Scheduler;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;

using Microsoft.Extensions.Options;

using RefinedElement.Kentico.Sentinel.Quoting;
using RefinedElement.Kentico.Sentinel.XbyK.Admin;
using RefinedElement.Kentico.Sentinel.XbyK.Admin.UIPages;
using RefinedElement.Kentico.Sentinel.XbyK.Configuration;
using RefinedElement.Kentico.Sentinel.XbyK.Scheduling;

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
public class SentinelSettingsPage(
    IOptions<SentinelOptions> options,
    IInfoProvider<ScheduledTaskConfigurationInfo> scheduledTaskProvider)
    : Page<SettingsClientProperties>
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

        // Cadence + enabled state read directly from CMS_ScheduledTaskConfiguration. We surface
        // the raw pipe-delimited Interval plus a human hint so the admin doesn't have to leave
        // the Sentinel app to answer "is this actually running, and how often?". True editing
        // still happens in the Scheduled Tasks admin app — this block is the status readout.
        var task = scheduledTaskProvider.Get()
            .WhereEquals(nameof(ScheduledTaskConfigurationInfo.ScheduledTaskConfigurationScheduledTaskIdentifier), SentinelScanTask.TaskName)
            .TopN(1)
            .FirstOrDefault();
        if (task is null)
        {
            properties.ScheduleState = "missing";
            properties.ScheduleIntervalRaw = string.Empty;
            properties.ScheduleIntervalHint = "No scheduled task row found — automated scans are not running. Create one in Scheduled tasks.";
            properties.ScheduleLastRunUtc = null;
            properties.ScheduleNextRunUtc = null;
        }
        else
        {
            properties.ScheduleState = task.ScheduledTaskConfigurationEnabled ? "enabled" : "disabled";
            properties.ScheduleIntervalRaw = task.ScheduledTaskConfigurationInterval ?? string.Empty;
            properties.ScheduleIntervalHint = HumanizeInterval(task.ScheduledTaskConfigurationInterval);
            properties.ScheduleLastRunUtc = task.ScheduledTaskConfigurationLastRunTime == default
                ? null
                : DateTime.SpecifyKind(task.ScheduledTaskConfigurationLastRunTime, DateTimeKind.Utc).ToString("O");
            properties.ScheduleNextRunUtc = task.ScheduledTaskConfigurationNextRunTime == default
                ? null
                : DateTime.SpecifyKind(task.ScheduledTaskConfigurationNextRunTime, DateTimeKind.Utc).ToString("O");
        }

        return Task.FromResult(properties);
    }

    /// <summary>
    /// Best-effort humanization of Kentico's pipe-delimited interval format. We only parse the
    /// first two fields (period + every) because those carry 90% of the useful information, and
    /// Kentico's internal parser is the ground truth for the rest. Admin sees something like
    /// "every day" (for every=1) or "every 3 days" (for every=3) instead of the raw
    /// "day;1;00:00:00" — good enough to confirm the cadence without making them decode the
    /// format. One-time schedules get the literal "runs once" since "every one-time" is nonsense.
    /// </summary>
    private static string HumanizeInterval(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "(no interval configured)";
        }
        var parts = raw.Split(';');
        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]))
        {
            return $"raw: {raw}";
        }
        var period = parts[0].ToLowerInvariant();
        var every = int.TryParse(parts[1], out var n) ? n : 0;
        if (every <= 0)
        {
            return $"raw: {raw}";
        }
        // One-time schedules don't take a period multiplier ("every 2 one-times" is nonsense),
        // so short-circuit to the literal phrase Kentico admins would recognize.
        if (period == "once")
        {
            return "runs once";
        }
        var unit = period switch
        {
            "second" => "second",
            "minute" => "minute",
            "hour" => "hour",
            "day" => "day",
            "week" => "week",
            "month" => "month",
            "year" => "year",
            _ => period,
        };
        return every == 1 ? $"every {unit}" : $"every {every} {unit}s";
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

    /// <summary>"enabled" | "disabled" | "missing" — readable state of the scheduled task row.</summary>
    public string ScheduleState { get; set; } = "missing";
    /// <summary>Raw pipe-delimited interval ("day;1;00:00:00") — shown in small print for transparency.</summary>
    public string ScheduleIntervalRaw { get; set; } = string.Empty;
    /// <summary>Human-readable cadence ("every day", "every 3 days", "runs once") — primary display.</summary>
    public string ScheduleIntervalHint { get; set; } = string.Empty;
    /// <summary>ISO-8601 UTC timestamp of the last successful run, or null if never run.</summary>
    public string? ScheduleLastRunUtc { get; set; }
    /// <summary>ISO-8601 UTC timestamp of the next scheduled run, or null if disabled/never scheduled.</summary>
    public string? ScheduleNextRunUtc { get; set; }
}

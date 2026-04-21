using CMS.DataEngine;
using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;

using RefinedElement.Kentico.Sentinel.XbyK.Admin;
using RefinedElement.Kentico.Sentinel.XbyK.Admin.UIPages;
using RefinedElement.Kentico.Sentinel.XbyK.InfoModels.SentinelFinding;
using RefinedElement.Kentico.Sentinel.XbyK.InfoModels.SentinelScanRun;

[assembly: UIPage(
    parentType: typeof(SentinelApplicationPage),
    slug: "dashboard",
    uiPageType: typeof(SentinelDashboardPage),
    name: "Dashboard",
    templateName: "@refinedelement/sentinel-admin/Dashboard",
    order: UIPageOrder.First - 1)]

namespace RefinedElement.Kentico.Sentinel.XbyK.Admin.UIPages;

/// <summary>
/// First screen an operator sees on opening Sentinel. KPI tiles for the latest scan, a mini
/// history of the last few runs, and the top rule offenders across recent scans. Backed by a
/// React template at <c>Client/src/dashboard/DashboardTemplate.tsx</c>; data is preloaded on
/// mount via <see cref="ConfigureTemplateProperties"/> and refreshable without page reload
/// via the <c>GetDashboardData</c> page command.
/// </summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public class SentinelDashboardPage(
    IInfoProvider<SentinelScanRunInfo> scanRunProvider,
    IInfoProvider<SentinelFindingInfo> findingProvider)
    : Page<DashboardClientProperties>
{
    // Tunables for the "recent" windows. Small enough that the data-pull stays fast at every
    // dashboard open, large enough that the ranking feels stable across a few scan cycles.
    private const int RecentScanWindow = 10;
    private const int TopRuleWindow = 50;
    private const int TopRulePageSize = 10;

    public override Task<DashboardClientProperties> ConfigureTemplateProperties(DashboardClientProperties properties)
    {
        PopulateDashboard(properties);
        return Task.FromResult(properties);
    }

    /// <summary>
    /// Re-pulls the same shape the initial page load built — the React template swaps its state
    /// without a hard page reload. Kept shallow so refreshing during a busy scan window doesn't
    /// hammer the CMS database.
    /// </summary>
    [PageCommand]
    public Task<ICommandResponse<DashboardRefreshResult>> GetDashboardData()
    {
        var data = new DashboardClientProperties();
        PopulateDashboard(data);
        return Task.FromResult(ResponseFrom(new DashboardRefreshResult { Data = data }));
    }

    private void PopulateDashboard(DashboardClientProperties properties)
    {
        properties.ScheduledTasksUrl = "/admin/scheduledtasks";
        properties.ScanHistoryUrl = "/admin/sentinel/scans";
        properties.FindingsUrl = "/admin/sentinel/findings";

        var recent = scanRunProvider.Get()
            .OrderByDescending(nameof(SentinelScanRunInfo.SentinelScanRunID))
            .TopN(RecentScanWindow)
            .ToList();

        properties.HasScans = recent.Count > 0;
        properties.LatestScan = recent.Count > 0 ? ToSummary(recent[0]) : null;
        properties.RecentScans = recent.Select(ToSummary).ToArray();

        if (recent.Count == 0)
        {
            properties.TopRules = Array.Empty<RuleCountDto>();
            return;
        }

        // Top-rule ranking sweeps findings from the most recent N scans. Ordering is count-desc;
        // ties break by category so the list is stable across refreshes.
        var recentRunIds = recent.Take(Math.Min(recent.Count, TopRuleWindow)).Select(r => r.SentinelScanRunID).ToArray();
        var findings = findingProvider.Get()
            .WhereIn(nameof(SentinelFindingInfo.SentinelFindingScanRunID), recentRunIds)
            .Columns(
                nameof(SentinelFindingInfo.SentinelFindingRuleID),
                nameof(SentinelFindingInfo.SentinelFindingCategory))
            .ToList();

        properties.TopRules = findings
            .GroupBy(f => (f.SentinelFindingRuleID, f.SentinelFindingCategory))
            .Select(g => new RuleCountDto
            {
                RuleId = g.Key.SentinelFindingRuleID,
                Category = g.Key.SentinelFindingCategory,
                Count = g.Count(),
            })
            .OrderByDescending(r => r.Count)
            .ThenBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
            .Take(TopRulePageSize)
            .ToArray();
    }

    private static ScanSummaryDto ToSummary(SentinelScanRunInfo run) => new()
    {
        RunId = run.SentinelScanRunID,
        StartedAt = run.SentinelScanRunStartedAt.ToString("O"),
        Status = run.SentinelScanRunStatus,
        Trigger = run.SentinelScanRunTrigger,
        TotalFindings = run.SentinelScanRunTotalFindings,
        ErrorCount = run.SentinelScanRunErrorCount,
        WarningCount = run.SentinelScanRunWarningCount,
        InfoCount = run.SentinelScanRunInfoCount,
        DurationSeconds = (double)run.SentinelScanRunDurationSeconds,
        SentinelVersion = run.SentinelScanRunSentinelVersion,
    };
}

// Client-property shapes serialize to JSON and are consumed by the React template — field names
// and types must line up with the matching TypeScript interfaces in DashboardTemplate.tsx.

public sealed class DashboardClientProperties : TemplateClientProperties
{
    public bool HasScans { get; set; }
    public ScanSummaryDto? LatestScan { get; set; }
    public ScanSummaryDto[] RecentScans { get; set; } = Array.Empty<ScanSummaryDto>();
    public RuleCountDto[] TopRules { get; set; } = Array.Empty<RuleCountDto>();
    public string ScheduledTasksUrl { get; set; } = string.Empty;
    public string ScanHistoryUrl { get; set; } = string.Empty;
    public string FindingsUrl { get; set; } = string.Empty;
}

public sealed class DashboardRefreshResult
{
    public DashboardClientProperties Data { get; set; } = new();
}

public sealed class ScanSummaryDto
{
    public int RunId { get; set; }
    public string StartedAt { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Trigger { get; set; } = string.Empty;
    public int TotalFindings { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public double DurationSeconds { get; set; }
    public string SentinelVersion { get; set; } = string.Empty;
}

public sealed class RuleCountDto
{
    public string RuleId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
}

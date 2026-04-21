using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;

using RefinedElement.Kentico.Sentinel.XbyK.Admin;
using RefinedElement.Kentico.Sentinel.XbyK.Admin.UIPages;
using RefinedElement.Kentico.Sentinel.XbyK.InfoModels.SentinelScanRun;

[assembly: UIPage(
    parentType: typeof(SentinelApplicationPage),
    slug: "scans",
    uiPageType: typeof(ScanHistoryListingPage),
    name: "Scan history",
    templateName: TemplateNames.LISTING,
    order: UIPageOrder.First)]

namespace RefinedElement.Kentico.Sentinel.XbyK.Admin.UIPages;

/// <summary>
/// Lists every <see cref="SentinelScanRunInfo"/> row — including in-progress, failed, and
/// cancelled runs — newest first. The <c>Status</c> column is exposed so admins can tell at a
/// glance which executions actually completed vs. which bailed. Reuses Kentico's built-in
/// LISTING template so this page needs no client-side React bundle; the admin shell renders
/// columns + filter + sort out of the box, and we only configure which columns show.
/// </summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public class ScanHistoryListingPage : ListingPage
{
    protected override string ObjectType => SentinelScanRunInfo.OBJECT_TYPE;

    public override Task ConfigurePage()
    {
        // Started-at descending is the natural operator mental model: "what ran most recently?"
        // Kentico's listing sorts via query param; we set the default so the first page load
        // already has the most useful order without forcing a click.
        PageConfiguration.ColumnConfigurations
            .AddColumn(nameof(SentinelScanRunInfo.SentinelScanRunID), "#", sortable: true, defaultSortDirection: SortTypeEnum.Desc)
            .AddColumn(nameof(SentinelScanRunInfo.SentinelScanRunStartedAt), "Started", sortable: true, defaultSortDirection: SortTypeEnum.Desc)
            .AddColumn(nameof(SentinelScanRunInfo.SentinelScanRunStatus), "Status", sortable: true, searchable: true)
            .AddColumn(nameof(SentinelScanRunInfo.SentinelScanRunTrigger), "Trigger", sortable: true, searchable: true)
            .AddColumn(nameof(SentinelScanRunInfo.SentinelScanRunTotalFindings), "Total", sortable: true)
            .AddColumn(nameof(SentinelScanRunInfo.SentinelScanRunErrorCount), "Errors", sortable: true)
            .AddColumn(nameof(SentinelScanRunInfo.SentinelScanRunWarningCount), "Warnings", sortable: true)
            .AddColumn(nameof(SentinelScanRunInfo.SentinelScanRunInfoCount), "Info", sortable: true)
            .AddColumn(nameof(SentinelScanRunInfo.SentinelScanRunDurationSeconds), "Duration (s)", sortable: true)
            .AddColumn(nameof(SentinelScanRunInfo.SentinelScanRunSentinelVersion), "Version", sortable: true);

        // No row-click edit action — scan-run rows are read-only history. Admins drill into
        // findings via the sibling Findings listing page (filtered by Scan run on demand).
        return base.ConfigurePage();
    }
}

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
    slug: "diff",
    uiPageType: typeof(SentinelDiffPage),
    name: "Compare scans",
    templateName: "@refinedelement/sentinel-admin/Diff",
    order: 250)]

namespace RefinedElement.Kentico.Sentinel.XbyK.Admin.UIPages;

/// <summary>
/// Side-by-side diff between two scan runs. Uses <see cref="SentinelFindingInfo.SentinelFindingFingerprintHash"/>
/// as the identity key so a finding that drifts in exact wording between scans (digits / timestamps
/// mutated by the fingerprint normalizer) still matches — a resolution is one that GENUINELY
/// disappeared, not one that happens to read slightly differently this week.
///
/// <para>Output is three buckets: <b>Resolved</b> (in "before" but not "after"),
/// <b>New</b> (in "after" but not "before"), and <b>Still open</b> (in both).</para>
/// </summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public class SentinelDiffPage(
    IInfoProvider<SentinelScanRunInfo> scanRunProvider,
    IInfoProvider<SentinelFindingInfo> findingProvider)
    : Page<DiffClientProperties>
{
    private const int ScanDropdownSize = 50;

    public override Task<DiffClientProperties> ConfigureTemplateProperties(DiffClientProperties properties)
    {
        var scans = scanRunProvider.Get()
            .OrderByDescending(nameof(SentinelScanRunInfo.SentinelScanRunID))
            .TopN(ScanDropdownSize)
            .ToList();
        properties.AvailableScans = scans.Select(r => new ScanOptionDto
        {
            RunId = r.SentinelScanRunID,
            Label = $"#{r.SentinelScanRunID} — {r.SentinelScanRunStartedAt:yyyy-MM-dd HH:mm} ({r.SentinelScanRunTotalFindings} findings)",
            FindingsCount = r.SentinelScanRunTotalFindings,
        }).ToArray();

        // Sensible defaults: after = most recent, before = next-most-recent. Admins reviewing
        // "what changed since last scan" get a useful result on page load without picking.
        if (scans.Count >= 2)
        {
            properties.DefaultBeforeRunId = scans[1].SentinelScanRunID;
            properties.DefaultAfterRunId = scans[0].SentinelScanRunID;
        }
        else if (scans.Count == 1)
        {
            properties.DefaultAfterRunId = scans[0].SentinelScanRunID;
            properties.DefaultBeforeRunId = scans[0].SentinelScanRunID;
        }

        return Task.FromResult(properties);
    }

    [PageCommand]
    public Task<ICommandResponse<DiffResponse>> ComputeDiff(DiffRequestData data)
    {
        if (data.BeforeRunId <= 0 || data.AfterRunId <= 0)
        {
            return Task.FromResult(ResponseFrom(new DiffResponse { Message = "Both scan selections are required." }));
        }

        var before = LoadFindings(data.BeforeRunId);
        var after = LoadFindings(data.AfterRunId);

        var beforeByFingerprint = before.ToDictionary(f => f.SentinelFindingFingerprintHash, StringComparer.OrdinalIgnoreCase);
        var afterByFingerprint = after.ToDictionary(f => f.SentinelFindingFingerprintHash, StringComparer.OrdinalIgnoreCase);

        var resolved = before
            .Where(f => !afterByFingerprint.ContainsKey(f.SentinelFindingFingerprintHash))
            .Select(ToDiff)
            .ToArray();
        var introduced = after
            .Where(f => !beforeByFingerprint.ContainsKey(f.SentinelFindingFingerprintHash))
            .Select(ToDiff)
            .ToArray();
        var stillOpen = after
            .Where(f => beforeByFingerprint.ContainsKey(f.SentinelFindingFingerprintHash))
            .Select(ToDiff)
            .ToArray();

        return Task.FromResult(ResponseFrom(new DiffResponse
        {
            Resolved = resolved,
            Introduced = introduced,
            StillOpen = stillOpen,
            Message = string.Empty,
        }));
    }

    private List<SentinelFindingInfo> LoadFindings(int runId) =>
        findingProvider.Get()
            .WhereEquals(nameof(SentinelFindingInfo.SentinelFindingScanRunID), runId)
            .ToList();

    private static DiffFindingDto ToDiff(SentinelFindingInfo f) => new()
    {
        Fingerprint = f.SentinelFindingFingerprintHash,
        RuleId = f.SentinelFindingRuleID,
        RuleTitle = f.SentinelFindingRuleTitle,
        Category = f.SentinelFindingCategory,
        Severity = f.SentinelFindingSeverity,
        Message = f.SentinelFindingMessage,
        Location = string.IsNullOrWhiteSpace(f.SentinelFindingLocation) ? null : f.SentinelFindingLocation,
    };
}

public sealed class DiffClientProperties : TemplateClientProperties
{
    public ScanOptionDto[] AvailableScans { get; set; } = Array.Empty<ScanOptionDto>();
    public int DefaultBeforeRunId { get; set; }
    public int DefaultAfterRunId { get; set; }
}

public sealed class DiffRequestData
{
    public int BeforeRunId { get; set; }
    public int AfterRunId { get; set; }
}

public sealed class DiffResponse
{
    public DiffFindingDto[] Resolved { get; set; } = Array.Empty<DiffFindingDto>();
    public DiffFindingDto[] Introduced { get; set; } = Array.Empty<DiffFindingDto>();
    public DiffFindingDto[] StillOpen { get; set; } = Array.Empty<DiffFindingDto>();
    public string Message { get; set; } = string.Empty;
}

public sealed class DiffFindingDto
{
    public string Fingerprint { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string RuleTitle { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Location { get; set; }
}

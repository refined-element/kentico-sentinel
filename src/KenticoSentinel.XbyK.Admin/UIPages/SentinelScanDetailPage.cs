using CMS.DataEngine;
using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.Authentication;
using Kentico.Xperience.Admin.Base.UIPages;

using RefinedElement.Kentico.Sentinel.Core.Remediation;
using RefinedElement.Kentico.Sentinel.XbyK.Acknowledgment;
using RefinedElement.Kentico.Sentinel.XbyK.Admin;
using RefinedElement.Kentico.Sentinel.XbyK.Admin.UIPages;
using RefinedElement.Kentico.Sentinel.XbyK.InfoModels.SentinelFinding;
using RefinedElement.Kentico.Sentinel.XbyK.InfoModels.SentinelScanRun;

[assembly: UIPage(
    parentType: typeof(SentinelApplicationPage),
    slug: "scan-detail",
    uiPageType: typeof(SentinelScanDetailPage),
    name: "Scan detail",
    templateName: "@refinedelement/sentinel-admin/ScanDetail",
    order: 150)]

namespace RefinedElement.Kentico.Sentinel.XbyK.Admin.UIPages;

/// <summary>
/// Drill-in view for a single scan run. Loads the most recent scan by default; a dropdown lets
/// the admin pivot to any other run. For each finding exposes the ack state + a one-click
/// acknowledge/snooze/revoke action via the <c>SetAckState</c> page command. Rule IDs with a
/// <see cref="RemediationGuide"/> entry render an expandable "how to fix" panel.
/// </summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public class SentinelScanDetailPage(
    IInfoProvider<SentinelScanRunInfo> scanRunProvider,
    IInfoProvider<SentinelFindingInfo> findingProvider,
    ISentinelFindingAckService ackService,
    IAuthenticatedUserAccessor userAccessor)
    : Page<ScanDetailClientProperties>
{
    private const int ScanDropdownSize = 50;

    public override async Task<ScanDetailClientProperties> ConfigureTemplateProperties(ScanDetailClientProperties properties)
    {
        // "Initial scan" is whichever run the URL points to via ?runId=, falling back to the
        // most recent completed run. Kentico admin doesn't expose query params as typed page
        // properties without additional plumbing; clients that deep-link set initialRunId via
        // a separate page command if they need to jump. For now: latest wins on first load.
        var recentScans = scanRunProvider.Get()
            .OrderByDescending(nameof(SentinelScanRunInfo.SentinelScanRunID))
            .TopN(ScanDropdownSize)
            .ToList();
        properties.AvailableScans = recentScans.Select(r => new ScanOptionDto
        {
            RunId = r.SentinelScanRunID,
            Label = $"#{r.SentinelScanRunID} — {r.SentinelScanRunStartedAt:yyyy-MM-dd HH:mm} ({r.SentinelScanRunTrigger})",
            FindingsCount = r.SentinelScanRunTotalFindings,
        }).ToArray();

        var initial = recentScans.FirstOrDefault();
        properties.Detail = initial is null ? BuildEmptyDetail() : await BuildDetail(initial.SentinelScanRunID);

        var user = await userAccessor.Get();
        properties.CurrentUserId = user?.UserID ?? 0;

        return properties;
    }

    [PageCommand]
    public async Task<ICommandResponse<ScanDetailResponse>> LoadScanDetail(LoadScanDetailData data)
    {
        var detail = await BuildDetail(data.RunId);
        return ResponseFrom(new ScanDetailResponse { Detail = detail });
    }

    [PageCommand(Permission = SystemPermissions.UPDATE)]
    public async Task<ICommandResponse<AckMutationResponse>> SetAckState(AckMutationData data)
    {
        var user = await userAccessor.Get();
        if (user is null)
        {
            // Don't persist an ack attributed to user 0 — kills auditability and could conflict
            // with future assumptions about who dismissed what. UIEvaluatePermission should
            // block unauthenticated access already; defence in depth in case something slips
            // through.
            return ResponseFrom(new AckMutationResponse
            {
                Success = false,
                Message = "Cannot record an acknowledgment without an authenticated admin user.",
            });
        }
        var userId = user.UserID;

        switch (data.Action)
        {
            case "acknowledge":
                ackService.Acknowledge(data.Fingerprint, userId, data.Note);
                break;
            case "snooze":
                // Client sends an ISO-8601 round-trip string (Date.toISOString() on the JS side).
                // Parse STRICTLY with the "O" format + RoundtripKind so a server culture that
                // uses dd/MM/yyyy can't swap month and day on us.
                if (!DateTime.TryParseExact(
                        data.SnoozeUntilUtc,
                        "O",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind,
                        out var until))
                {
                    return ResponseFrom(new AckMutationResponse
                    {
                        Success = false,
                        Message = "Invalid snooze date — expected ISO-8601 round-trip format.",
                    });
                }
                // Reject past / effectively-immediate snooze times. The finding would persist as
                // Snoozed and then read back as Active on the very next refresh (ToAck treats an
                // expired snooze as Active), giving the admin misleading "Snoozed" feedback for
                // something that's already un-snoozed. One-minute grace covers clock drift.
                if (until <= DateTime.UtcNow.AddMinutes(1))
                {
                    return ResponseFrom(new AckMutationResponse
                    {
                        Success = false,
                        Message = "Snooze date must be at least one minute in the future.",
                    });
                }
                ackService.Snooze(data.Fingerprint, until, userId, data.Note);
                break;
            case "revoke":
                ackService.Revoke(data.Fingerprint);
                break;
            default:
                return ResponseFrom(new AckMutationResponse { Success = false, Message = $"Unknown action '{data.Action}'." });
        }

        var state = ackService.Get(data.Fingerprint);
        return ResponseFrom(new AckMutationResponse
        {
            Success = true,
            Fingerprint = data.Fingerprint,
            NewState = state?.State.ToString() ?? "Active",
            SnoozeUntilUtc = state?.SnoozeUntil?.ToString("O"),
            Note = state?.Note,
        });
    }

    private async Task<ScanDetailDto> BuildDetail(int runId)
    {
        var run = scanRunProvider.Get()
            .WhereEquals(nameof(SentinelScanRunInfo.SentinelScanRunID), runId)
            .TopN(1)
            .FirstOrDefault();
        if (run is null)
        {
            return BuildEmptyDetail();
        }

        var findings = findingProvider.Get()
            .WhereEquals(nameof(SentinelFindingInfo.SentinelFindingScanRunID), runId)
            .ToList();

        var acks = ackService.GetAll(findings.Select(f => f.SentinelFindingFingerprintHash));

        var findingDtos = findings.Select(f =>
        {
            var ackState = acks.TryGetValue(f.SentinelFindingFingerprintHash, out var a) ? a : null;
            var remediation = RemediationGuide.TryFor(f.SentinelFindingRuleID);
            return new FindingDetailDto
            {
                FindingId = f.SentinelFindingID,
                Fingerprint = f.SentinelFindingFingerprintHash,
                RuleId = f.SentinelFindingRuleID,
                RuleTitle = f.SentinelFindingRuleTitle,
                Category = f.SentinelFindingCategory,
                Severity = f.SentinelFindingSeverity,
                Message = f.SentinelFindingMessage,
                Location = string.IsNullOrWhiteSpace(f.SentinelFindingLocation) ? null : f.SentinelFindingLocation,
                Remediation = string.IsNullOrWhiteSpace(f.SentinelFindingRemediation) ? null : f.SentinelFindingRemediation,
                QuoteEligible = f.SentinelFindingQuoteEligible,
                AckState = ackState?.State.ToString() ?? "Active",
                SnoozeUntilUtc = ackState?.SnoozeUntil?.ToString("O"),
                AckNote = ackState?.Note,
                RemediationTitle = remediation?.Title,
                RemediationSummary = remediation?.Summary,
                RemediationSteps = remediation?.Steps,
            };
        }).ToArray();

        return await Task.FromResult(new ScanDetailDto
        {
            Run = new ScanSummaryDto
            {
                RunId = run.SentinelScanRunID,
                // SQL datetime round-trips as DateTimeKind.Unspecified via Kentico's Info framework;
                // SpecifyKind(UTC) before formatting ensures the "Z" designator is present so the
                // React client doesn't re-interpret the value in local time.
                StartedAt = DateTime.SpecifyKind(run.SentinelScanRunStartedAt, DateTimeKind.Utc).ToString("O"),
                Status = run.SentinelScanRunStatus,
                Trigger = run.SentinelScanRunTrigger,
                TotalFindings = run.SentinelScanRunTotalFindings,
                ErrorCount = run.SentinelScanRunErrorCount,
                WarningCount = run.SentinelScanRunWarningCount,
                InfoCount = run.SentinelScanRunInfoCount,
                DurationSeconds = (double)run.SentinelScanRunDurationSeconds,
                SentinelVersion = run.SentinelScanRunSentinelVersion,
            },
            Findings = findingDtos,
        });
    }

    private static ScanDetailDto BuildEmptyDetail() => new()
    {
        Run = null,
        Findings = Array.Empty<FindingDetailDto>(),
    };
}

public sealed class ScanDetailClientProperties : TemplateClientProperties
{
    public ScanOptionDto[] AvailableScans { get; set; } = Array.Empty<ScanOptionDto>();
    public ScanDetailDto Detail { get; set; } = new();
    public int CurrentUserId { get; set; }
}

public sealed class ScanDetailResponse
{
    public ScanDetailDto Detail { get; set; } = new();
}

public sealed class ScanDetailDto
{
    public ScanSummaryDto? Run { get; set; }
    public FindingDetailDto[] Findings { get; set; } = Array.Empty<FindingDetailDto>();
}

public sealed class FindingDetailDto
{
    public int FindingId { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string RuleTitle { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Remediation { get; set; }
    public bool QuoteEligible { get; set; }
    public string AckState { get; set; } = "Active";
    public string? SnoozeUntilUtc { get; set; }
    public string? AckNote { get; set; }
    public string? RemediationTitle { get; set; }
    public string? RemediationSummary { get; set; }
    public string? RemediationSteps { get; set; }
}

public sealed class LoadScanDetailData
{
    public int RunId { get; set; }
}

public sealed class AckMutationData
{
    public string Fingerprint { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // "acknowledge" | "snooze" | "revoke"
    public string? SnoozeUntilUtc { get; set; }
    public string? Note { get; set; }
}

public sealed class AckMutationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string NewState { get; set; } = "Active";
    public string? SnoozeUntilUtc { get; set; }
    public string? Note { get; set; }
}

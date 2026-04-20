using CMS.Core;

using Microsoft.Extensions.Options;

using RefinedElement.Kentico.Sentinel.Core;
using RefinedElement.Kentico.Sentinel.XbyK.Configuration;
using RefinedElement.Kentico.Sentinel.XbyK.InfoModels.SentinelScanRun;

namespace RefinedElement.Kentico.Sentinel.XbyK.Notifications;

internal sealed class SentinelEventLogWriter(IEventLogService eventLog, IOptions<SentinelOptions> options) : ISentinelEventLogWriter
{
    private const string EventSource = "Sentinel";

    private readonly SentinelOptions options = options.Value;

    public void Write(SentinelScanRunInfo run, IReadOnlyList<Finding> findings)
    {
        // One summary entry per scan so admins see a pulse even when everything is clean.
        eventLog.LogInformation(
            source: EventSource,
            eventCode: "SCAN_COMPLETED",
            eventDescription:
                $"Sentinel scan #{run.SentinelScanRunID} completed — " +
                $"{run.SentinelScanRunTotalFindings} findings " +
                $"({run.SentinelScanRunErrorCount}E/{run.SentinelScanRunWarningCount}W/{run.SentinelScanRunInfoCount}I), " +
                $"trigger={run.SentinelScanRunTrigger}, version={run.SentinelScanRunSentinelVersion}.");

        if (!Enum.TryParse<Severity>(options.EventLogIntegration.SeverityThreshold, ignoreCase: true, out var threshold))
        {
            threshold = Severity.Warning;
        }

        var qualifying = findings.Where(f => f.Severity >= threshold).ToArray();
        var cap = Math.Max(0, options.EventLogIntegration.MaxEntriesPerScan);
        var toWrite = qualifying.Take(cap);
        var suppressed = Math.Max(0, qualifying.Length - cap);

        // One event per severity-qualifying finding, capped by MaxEntriesPerScan so a single
        // thousand-finding scan can't balloon the event log / slow the admin's Event log pager.
        foreach (var f in toWrite)
        {
            var desc = string.IsNullOrWhiteSpace(f.Location)
                ? $"{f.RuleId}: {f.Message}"
                : $"{f.RuleId}: {f.Message} ({f.Location})";
            switch (f.Severity)
            {
                case Severity.Error:
                    eventLog.LogError(EventSource, f.RuleId, desc);
                    break;
                case Severity.Warning:
                    eventLog.LogWarning(EventSource, f.RuleId, desc);
                    break;
                default:
                    eventLog.LogInformation(EventSource, f.RuleId, desc);
                    break;
            }
        }

        if (suppressed > 0)
        {
            // Single summary line so the admin sees the scale of what was dropped without the
            // noise of writing every remaining row.
            eventLog.LogInformation(
                source: EventSource,
                eventCode: "FINDINGS_TRUNCATED",
                eventDescription:
                    $"Sentinel scan #{run.SentinelScanRunID} had {qualifying.Length} findings at or above " +
                    $"{threshold} severity; {suppressed} not written to the event log (MaxEntriesPerScan={cap}). " +
                    $"Full list is available in the RefinedElement_SentinelFinding table.");
        }
    }
}

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

        // One event per severity-qualifying finding.
        foreach (var f in findings.Where(f => f.Severity >= threshold))
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
    }
}

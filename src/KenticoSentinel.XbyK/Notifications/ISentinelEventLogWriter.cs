using RefinedElement.Kentico.Sentinel.Core;
using RefinedElement.Kentico.Sentinel.XbyK.InfoModels.SentinelScanRun;

namespace RefinedElement.Kentico.Sentinel.XbyK.Notifications;

/// <summary>
/// Writes a summary entry per scan to <c>CMS_EventLog</c> plus one entry per finding that meets
/// the configured severity threshold. Exposed as an interface so tests can drop in a no-op.
/// </summary>
public interface ISentinelEventLogWriter
{
    void Write(SentinelScanRunInfo run, IReadOnlyList<Finding> findings);
}

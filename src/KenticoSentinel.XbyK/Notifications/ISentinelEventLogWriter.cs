using RefinedElement.Kentico.Sentinel.Core;

namespace RefinedElement.Kentico.Sentinel.XbyK.Notifications;

/// <summary>
/// Writes a summary entry per scan to <c>CMS_EventLog</c> plus one entry per finding that meets
/// the configured severity threshold. Exposed as an interface so tests can drop in a no-op and
/// future embeddings (admin UI command, webhook, etc.) can replace the implementation.
/// </summary>
public interface ISentinelEventLogWriter
{
    void Write(ScanRunSummary run, IReadOnlyList<Finding> findings);
}

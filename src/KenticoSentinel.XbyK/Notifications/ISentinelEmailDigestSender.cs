using RefinedElement.Kentico.Sentinel.Core;

namespace RefinedElement.Kentico.Sentinel.XbyK.Notifications;

public interface ISentinelEmailDigestSender
{
    Task SendAsync(ScanRunSummary run, IReadOnlyList<Finding> findings, CancellationToken cancellationToken);
}

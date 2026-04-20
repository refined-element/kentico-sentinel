using RefinedElement.Kentico.Sentinel.Core;
using RefinedElement.Kentico.Sentinel.XbyK.InfoModels.SentinelScanRun;

namespace RefinedElement.Kentico.Sentinel.XbyK.Notifications;

public interface ISentinelEmailDigestSender
{
    Task SendAsync(SentinelScanRunInfo run, IReadOnlyList<Finding> findings, CancellationToken cancellationToken);
}

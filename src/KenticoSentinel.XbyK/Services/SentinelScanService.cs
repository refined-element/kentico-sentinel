using CMS.DataEngine;
using CMS.Helpers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RefinedElement.Kentico.Sentinel.Core;
using RefinedElement.Kentico.Sentinel.XbyK.Configuration;
using RefinedElement.Kentico.Sentinel.XbyK.InfoModels.SentinelFinding;
using RefinedElement.Kentico.Sentinel.XbyK.InfoModels.SentinelScanRun;
using RefinedElement.Kentico.Sentinel.XbyK.Notifications;

namespace RefinedElement.Kentico.Sentinel.XbyK.Services;

/// <summary>
/// Orchestrates a single scan: resolves the check set, runs the engine, persists the scan run +
/// findings via Kentico's Info providers, and fires downstream notifiers (EventLog + digest).
/// </summary>
public sealed class SentinelScanService(
    IOptions<SentinelOptions> options,
    IInfoProvider<SentinelScanRunInfo> scanRunProvider,
    IInfoProvider<SentinelFindingInfo> findingProvider,
    IHttpClientFactory httpClientFactory,
    ISentinelEventLogWriter eventLogWriter,
    ISentinelEmailDigestSender digestSender,
    ILogger<SentinelScanService> logger)
{
    private readonly SentinelOptions options = options.Value;

    public async Task<SentinelScanRunInfo?> RunAsync(string trigger, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Sentinel scan skipped: Sentinel:Enabled is false. Trigger={Trigger}.", trigger);
            return null;
        }

        var runRow = new SentinelScanRunInfo
        {
            SentinelScanRunGuid = Guid.NewGuid(),
            SentinelScanRunStartedAt = DateTime.UtcNow,
            SentinelScanRunTrigger = trigger,
            SentinelScanRunSentinelVersion = SentinelVersion.Current,
            SentinelScanRunStatus = "Running",
        };
        scanRunProvider.Set(runRow);

        try
        {
            var checks = CheckRegistry.BuiltIn()
                .Where(c => !options.Checks.Excluded.Contains(c.RuleId, StringComparer.OrdinalIgnoreCase))
                .ToArray();

            var context = BuildContext(httpClientFactory);
            var runner = new CheckRunner(checks);

            logger.LogInformation("Sentinel scan starting: trigger={Trigger}, checks={CheckCount}, runtime={Runtime}.",
                trigger, checks.Length, context.RuntimeEnabled);

            var result = await runner.RunAsync(context, progress: null, cancellationToken).ConfigureAwait(false);

            // Keep the findings inserts + scan-run update atomic. If one finding row fails mid-loop
            // we don't want the scan-run row to say "Completed: 14 findings" while only 9 actually
            // landed. CMSTransactionScope wraps Kentico's Info provider writes in a single DB
            // transaction and commits only on success.
            using (var tx = new CMSTransactionScope())
            {
                foreach (var f in result.Findings)
                {
                    findingProvider.Set(new SentinelFindingInfo
                    {
                        SentinelFindingGuid = Guid.NewGuid(),
                        SentinelFindingScanRunID = runRow.SentinelScanRunID,
                        SentinelFindingRuleID = f.RuleId,
                        SentinelFindingRuleTitle = f.RuleTitle,
                        SentinelFindingCategory = f.Category,
                        SentinelFindingSeverity = f.Severity.ToString(),
                        SentinelFindingMessage = f.Message,
                        SentinelFindingLocation = f.Location ?? string.Empty,
                        SentinelFindingRemediation = f.Remediation ?? string.Empty,
                        SentinelFindingQuoteEligible = f.QuoteEligible,
                        SentinelFindingFingerprintHash = FindingFingerprint.Compute(f),
                    });
                }

                runRow.SentinelScanRunCompletedAt = DateTime.UtcNow;
                runRow.SentinelScanRunTotalFindings = result.Findings.Count;
                runRow.SentinelScanRunErrorCount = result.ErrorCount;
                runRow.SentinelScanRunWarningCount = result.WarningCount;
                runRow.SentinelScanRunInfoCount = result.InfoCount;
                runRow.SentinelScanRunDurationSeconds = (decimal)result.Duration.TotalSeconds;
                runRow.SentinelScanRunStatus = "Completed";
                scanRunProvider.Set(runRow);

                tx.Commit();
            }

            logger.LogInformation("Sentinel scan completed: run={RunId}, findings={Total} ({Errors}E/{Warnings}W/{Info}I).",
                runRow.SentinelScanRunID, result.Findings.Count, result.ErrorCount, result.WarningCount, result.InfoCount);

            if (options.EventLogIntegration.Enabled)
            {
                eventLogWriter.Write(runRow, result.Findings);
            }

            if (options.EmailDigest.Enabled && options.EmailDigest.Recipients.Count > 0)
            {
                await digestSender.SendAsync(runRow, result.Findings, cancellationToken).ConfigureAwait(false);
            }

            return runRow;
        }
        catch (Exception ex)
        {
            runRow.SentinelScanRunCompletedAt = DateTime.UtcNow;
            runRow.SentinelScanRunStatus = "Failed";
            runRow.SentinelScanRunErrorMessage = ex.Message;
            scanRunProvider.Set(runRow);
            logger.LogError(ex, "Sentinel scan failed for run {RunId}.", runRow.SentinelScanRunID);
            throw;
        }
    }

    private ScanContext BuildContext(IHttpClientFactory factory)
    {
        var connectionString = !string.IsNullOrWhiteSpace(options.RuntimeChecks.ConnectionString)
            ? options.RuntimeChecks.ConnectionString
            : ConnectionHelper.ConnectionString;

        return new ScanContext
        {
            RepoPath = AppContext.BaseDirectory, // static checks read files relative to the running app
            ConnectionString = string.IsNullOrWhiteSpace(connectionString) ? null : connectionString,
            StaleDays = options.RuntimeChecks.StaleDays,
            EventLogDays = options.RuntimeChecks.EventLogDays,
            HttpClientFactory = factory,
        };
    }
}

internal static class SentinelVersion
{
    public static string Current { get; } =
        typeof(SentinelScanService).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()
            ?.InformationalVersion
            .Split('+', 2)[0]
        ?? "unknown";
}

using CMS.DataEngine;
using CMS.Helpers;

using Microsoft.Extensions.Hosting;
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
    IHostEnvironment hostEnvironment,
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
            // Installer-defined column sizes: Trigger=32, Version=64. Current values fit with
            // room to spare, but clamp as belt-and-suspenders against future trigger strings
            // or build metadata that sneaks through.
            SentinelScanRunTrigger = TruncateTo(trigger, 32),
            SentinelScanRunSentinelVersion = TruncateTo(SentinelVersion.Current, 64),
            SentinelScanRunStatus = "Running",
            // Count/duration columns are NOT NULL in the installer schema; without an explicit
            // 0 here Kentico's DataContainer persists them as NULL and SQL rejects the INSERT
            // ("Cannot insert the value NULL into column 'SentinelScanRunTotalFindings'").
            // They're re-assigned to the real values inside the transaction below, but we need
            // valid placeholders for the initial Running row to land.
            SentinelScanRunTotalFindings = 0,
            SentinelScanRunErrorCount = 0,
            SentinelScanRunWarningCount = 0,
            SentinelScanRunInfoCount = 0,
            SentinelScanRunDurationSeconds = 0m,
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

            // Downstream notifiers run OUTSIDE the main try/catch for scan status. The scan +
            // persistence already succeeded at this point (DB transaction committed above). A
            // transient SMTP failure or event-log hiccup must NOT retroactively flip the run's
            // status to "Failed" — the findings are already correct and visible in the DB.
            if (options.EventLogIntegration.Enabled)
            {
                try
                {
                    eventLogWriter.Write(runRow, result.Findings);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Sentinel scan {RunId} completed, but CMS_EventLog mirroring failed. Findings are still in the DB.",
                        runRow.SentinelScanRunID);
                }
            }

            if (options.EmailDigest.Enabled && options.EmailDigest.Recipients.Count > 0)
            {
                try
                {
                    await digestSender.SendAsync(runRow, result.Findings, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Sentinel scan {RunId} completed, but email digest delivery failed.",
                        runRow.SentinelScanRunID);
                }
            }

            return runRow;
        }
        catch (OperationCanceledException)
        {
            // Cancellations are an expected outcome — someone hit "cancel" on the scheduled task,
            // an app shutdown is propagating the token, etc. Don't pollute the error log with
            // stack traces or mark the run "Failed"; record a first-class "Cancelled" status
            // and rethrow so the scheduled task returns its own cancelled result.
            runRow.SentinelScanRunCompletedAt = DateTime.UtcNow;
            runRow.SentinelScanRunStatus = "Cancelled";
            runRow.SentinelScanRunErrorMessage = "Scan cancelled before completion.";
            scanRunProvider.Set(runRow);
            logger.LogWarning("Sentinel scan cancelled for run {RunId} ({RunGuid}).",
                runRow.SentinelScanRunID, runRow.SentinelScanRunGuid);
            throw;
        }
        catch (Exception ex)
        {
            runRow.SentinelScanRunCompletedAt = DateTime.UtcNow;
            runRow.SentinelScanRunStatus = "Failed";
            // Don't persist raw ex.Message — exception text often contains server names, file paths,
            // connection-string fragments, and other internals. Store a correlation id (the run's
            // own GUID) so operators can join the DB row to the logged exception; logger.LogError
            // below captures the full stack.
            runRow.SentinelScanRunErrorMessage =
                $"Scan failed. See application logs with correlation id {runRow.SentinelScanRunGuid:D}.";
            scanRunProvider.Set(runRow);
            logger.LogError(ex, "Sentinel scan failed for run {RunId} ({RunGuid}).",
                runRow.SentinelScanRunID, runRow.SentinelScanRunGuid);
            throw;
        }
    }

    private static string TruncateTo(string value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength
            ? value ?? string.Empty
            : value[..maxLength];

    private ScanContext BuildContext(IHttpClientFactory factory)
    {
        var connectionString = !string.IsNullOrWhiteSpace(options.RuntimeChecks.ConnectionString)
            ? options.RuntimeChecks.ConnectionString
            : ConnectionHelper.ConnectionString;

        return new ScanContext
        {
            // ContentRootPath points at the XbyK project root (the folder containing Program.cs,
            // the *.csproj, appsettings.json, etc.) which is what the static checks read.
            // AppContext.BaseDirectory would incorrectly point at bin/<config>/<tfm>/, making
            // every file-based check report "not found" even in valid installations.
            RepoPath = hostEnvironment.ContentRootPath,
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
            // Null-propagate through BOTH the attribute and InformationalVersion — an assembly
            // built without the attribute OR with an empty InformationalVersion would otherwise
            // NRE on .Split, skipping the "unknown" fallback below.
            ?.InformationalVersion
            ?.Split('+', 2)[0]
        ?? "unknown";
}

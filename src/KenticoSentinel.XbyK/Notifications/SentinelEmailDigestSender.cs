using System.Text;

using CMS.EmailEngine;
using CMS.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RefinedElement.Kentico.Sentinel.Core;
using RefinedElement.Kentico.Sentinel.XbyK.Configuration;
using RefinedElement.Kentico.Sentinel.XbyK.InfoModels.SentinelScanRun;

namespace RefinedElement.Kentico.Sentinel.XbyK.Notifications;

internal sealed class SentinelEmailDigestSender(
    IEmailService emailService,
    IOptions<SentinelOptions> options,
    ILogger<SentinelEmailDigestSender> logger) : ISentinelEmailDigestSender
{
    private readonly SentinelOptions options = options.Value;

    public async Task SendAsync(SentinelScanRunInfo run, IReadOnlyList<Finding> findings, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Severity>(options.EmailDigest.SeverityThreshold, ignoreCase: true, out var threshold))
        {
            threshold = Severity.Warning;
        }

        var digestWorthy = findings.Where(f => f.Severity >= threshold).ToArray();
        if (options.EmailDigest.OnlyWhenNewFindings && digestWorthy.Length == 0)
        {
            logger.LogInformation("Sentinel digest suppressed — no findings at or above {Threshold} severity.", threshold);
            return;
        }

        var html = RenderHtml(run, findings, digestWorthy);
        var subject = $"Sentinel scan #{run.SentinelScanRunID}: " +
                      $"{run.SentinelScanRunTotalFindings} findings " +
                      $"({run.SentinelScanRunErrorCount}E/{run.SentinelScanRunWarningCount}W/{run.SentinelScanRunInfoCount}I)";

        foreach (var recipient in options.EmailDigest.Recipients.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            var message = new EmailMessage
            {
                Recipients = recipient,
                Subject = subject,
                Body = html,
                Priority = EmailPriorityEnum.Normal,
                MailoutGuid = Guid.NewGuid(),
            };

            try
            {
                await emailService.SendEmail(message).ConfigureAwait(false);
                logger.LogInformation("Sentinel digest sent to {Recipient} for run {RunId}.", recipient, run.SentinelScanRunID);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Sentinel digest send to {Recipient} failed for run {RunId}.", recipient, run.SentinelScanRunID);
            }
        }
    }

    internal static string RenderHtml(SentinelScanRunInfo run, IReadOnlyList<Finding> findings, IReadOnlyList<Finding> highlighted)
    {
        var sb = new StringBuilder(8 * 1024);

        sb.Append("<!DOCTYPE html><html><body style=\"font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:#1a1a2e;background:#f5f5f7;margin:0;padding:32px 0;\">");
        sb.Append("<div style=\"max-width:600px;margin:0 auto;background:#ffffff;border-radius:12px;padding:32px;\">");

        sb.Append("<h2 style=\"color:#1a1a2e;margin:0 0 12px;\">Kentico Sentinel weekly digest</h2>");
        sb.Append($"<p style=\"color:#555;margin:0 0 24px;font-size:14px;\">Scan #{run.SentinelScanRunID} completed {run.SentinelScanRunCompletedAt:yyyy-MM-dd HH:mm} UTC.</p>");

        sb.Append("<table cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;border-collapse:collapse;margin:0 0 24px;\">");
        AppendRow(sb, "Total findings", run.SentinelScanRunTotalFindings.ToString());
        AppendRow(sb, "Errors", run.SentinelScanRunErrorCount.ToString(), color: "#c0392b");
        AppendRow(sb, "Warnings", run.SentinelScanRunWarningCount.ToString(), color: "#b98422");
        AppendRow(sb, "Info", run.SentinelScanRunInfoCount.ToString(), color: "#555");
        AppendRow(sb, "Trigger", run.SentinelScanRunTrigger);
        sb.Append("</table>");

        if (highlighted.Count > 0)
        {
            sb.Append("<h3 style=\"font-size:15px;margin:24px 0 12px;\">Sample findings</h3>");
            foreach (var f in highlighted.Take(10))
            {
                var color = f.Severity switch { Severity.Error => "#c0392b", Severity.Warning => "#b98422", _ => "#555" };
                sb.Append($"<div style=\"padding:10px 12px;margin:6px 0;background:#f9f9fb;border-left:3px solid {color};border-radius:4px;\">");
                sb.Append($"<strong style=\"color:{color};\">[{f.Severity.ToString().ToUpperInvariant()}]</strong> ");
                sb.Append($"<code style=\"color:#555;\">{HtmlEncode(f.RuleId)}</code> — {HtmlEncode(f.Message)}");
                sb.Append("</div>");
            }
            if (highlighted.Count > 10)
            {
                sb.Append($"<p style=\"color:#555;font-size:13px;margin:10px 0 0;\">… and {highlighted.Count - 10} more at or above threshold. See the Sentinel module in the admin for the full list.</p>");
            }
        }

        sb.Append("<p style=\"color:#555;font-size:13px;margin:24px 0 0;\">Open the <strong>Sentinel</strong> app in Kentico admin to acknowledge findings, adjust the schedule, or contact Refined Element for remediation.</p>");
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string label, string value, string? color = null)
    {
        sb.Append("<tr>");
        sb.Append($"<td style=\"padding:6px 0;color:#8b8b95;width:140px;font-size:13px;\">{label}</td>");
        sb.Append($"<td style=\"padding:6px 0;color:{color ?? "#1a1a2e"};font-weight:600;\">{value}</td>");
        sb.Append("</tr>");
    }

    private static string HtmlEncode(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}

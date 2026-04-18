using RefinedElement.Kentico.Sentinel.Reporting;

namespace RefinedElement.Kentico.Sentinel.Quoting;

/// <summary>
/// Converts a <see cref="ReportDocument"/> into a <see cref="QuoteSubmission"/> suitable for sending
/// to Refined Element. By default it drops finding <c>Location</c> and <c>Remediation</c> — those are
/// the fields most likely to leak file paths, connection-string paths, or configuration values.
/// The <c>includeContext</c> opt-in preserves them for users who want a more accurate quote.
/// </summary>
public static class QuoteSanitizer
{
    public static QuoteSubmission Sanitize(ReportDocument report, string contactEmail, bool includeContext)
    {
        var findings = report.Findings
            .Select(f => new QuoteFinding(
                RuleId: f.RuleId,
                RuleTitle: f.RuleTitle,
                Category: f.Category,
                Severity: f.Severity,
                Message: f.Message,
                Location: includeContext ? f.Location : null,
                Remediation: includeContext ? f.Remediation : null))
            .ToArray();

        return new QuoteSubmission(
            ContactEmail: contactEmail,
            SentinelVersion: report.SentinelVersion,
            Scan: report.Scan with { RepoPath = includeContext ? report.Scan.RepoPath : "(redacted)" },
            Summary: report.Summary,
            Findings: findings,
            IncludesContext: includeContext);
    }
}

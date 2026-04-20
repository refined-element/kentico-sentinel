using RefinedElement.Kentico.Sentinel.Reporting;

namespace RefinedElement.Kentico.Sentinel.Quoting;

/// <summary>
/// Payload POSTed to the Refined Element quote endpoint. Always derived from a <see cref="ReportDocument"/>
/// via <see cref="QuoteSanitizer"/> so sensitive fields are stripped by default.
/// </summary>
public sealed record QuoteSubmission(
    string ContactEmail,
    string SentinelVersion,
    ReportScan Scan,
    ReportSummary Summary,
    IReadOnlyList<QuoteFinding> Findings,
    bool IncludesContext);

public sealed record QuoteFinding(
    string RuleId,
    string RuleTitle,
    string Category,
    string Severity,
    string Message,
    string? Location,
    string? Remediation);

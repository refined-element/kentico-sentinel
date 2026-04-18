using System.Net;
using System.Text;

namespace RefinedElement.Kentico.Sentinel.Reporting;

/// <summary>
/// Emits a self-contained HTML report (inline CSS, no external assets) styled with Refined Element brand
/// accents. Groups findings by category, ordered by severity.
/// </summary>
public static class HtmlReportWriter
{
    public static async Task WriteAsync(ReportDocument doc, string outputPath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var html = Render(doc);
        await File.WriteAllTextAsync(outputPath, html, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    private static string Render(ReportDocument doc)
    {
        var sb = new StringBuilder(16 * 1024);
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<title>Kentico Sentinel Report</title>");
        sb.Append("<style>").Append(Css).Append("</style>");
        sb.Append("</head><body>");

        RenderHeader(sb, doc);
        RenderSummary(sb, doc);
        RenderExecutions(sb, doc);
        RenderFindings(sb, doc);
        RenderFooter(sb);

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static void RenderHeader(StringBuilder sb, ReportDocument doc)
    {
        sb.Append("<header class=\"hero\">");
        sb.Append("<div class=\"brand\"><span class=\"mark\">⚡</span> Kentico Sentinel</div>");
        sb.Append("<div class=\"tagline\">Static + runtime health scan for Xperience by Kentico</div>");
        sb.Append("<div class=\"meta\">");
        sb.Append("<div><span>Scanned</span> ").Append(Html(doc.Scan.RepoPath)).Append("</div>");
        sb.Append("<div><span>Completed</span> ").Append(doc.Scan.CompletedAt.ToString("yyyy-MM-dd HH:mm 'UTC'")).Append("</div>");
        sb.Append("<div><span>Duration</span> ").Append(doc.Scan.DurationSeconds.ToString("N2")).Append(" s</div>");
        sb.Append("<div><span>Runtime checks</span> ").Append(doc.Scan.RuntimeEnabled ? "enabled" : "skipped").Append("</div>");
        sb.Append("</div></header>");
    }

    private static void RenderSummary(StringBuilder sb, ReportDocument doc)
    {
        sb.Append("<section class=\"summary\">");
        sb.Append(Card("total", "Total findings", doc.Summary.Total.ToString()));
        sb.Append(Card("errors", "Errors", doc.Summary.Errors.ToString()));
        sb.Append(Card("warnings", "Warnings", doc.Summary.Warnings.ToString()));
        sb.Append(Card("info", "Info", doc.Summary.Info.ToString()));
        sb.Append("</section>");
    }

    private static string Card(string cls, string label, string value) =>
        $"<div class=\"card {cls}\"><div class=\"value\">{WebUtility.HtmlEncode(value)}</div><div class=\"label\">{WebUtility.HtmlEncode(label)}</div></div>";

    private static void RenderExecutions(StringBuilder sb, ReportDocument doc)
    {
        sb.Append("<section class=\"executions\"><h2>Checks</h2><table><thead><tr>");
        sb.Append("<th>Rule</th><th>Title</th><th>Kind</th><th>Status</th><th>Duration</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var e in doc.Executions)
        {
            var statusClass = e.Status.ToLowerInvariant();
            sb.Append("<tr>");
            sb.Append("<td class=\"mono\">").Append(Html(e.RuleId)).Append("</td>");
            sb.Append("<td>").Append(Html(e.Title)).Append("</td>");
            sb.Append("<td>").Append(Html(e.Kind)).Append("</td>");
            sb.Append("<td><span class=\"pill ").Append(statusClass).Append("\">").Append(Html(e.Status)).Append("</span></td>");
            sb.Append("<td class=\"mono\">").Append(e.DurationMs.ToString("N1")).Append(" ms</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table></section>");
    }

    private static void RenderFindings(StringBuilder sb, ReportDocument doc)
    {
        sb.Append("<section class=\"findings\"><h2>Findings</h2>");

        if (doc.Findings.Count == 0)
        {
            sb.Append("<p class=\"empty\">No issues found. Nicely done.</p></section>");
            return;
        }

        var grouped = doc.Findings
            .GroupBy(f => f.Category)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            sb.Append("<h3>").Append(Html(group.Key)).Append("</h3>");
            var ordered = group
                .OrderByDescending(f => SeverityRank(f.Severity))
                .ThenBy(f => f.RuleId);

            foreach (var f in ordered)
            {
                var sev = f.Severity.ToLowerInvariant();
                sb.Append("<article class=\"finding ").Append(sev).Append("\">");
                sb.Append("<div class=\"row\">");
                sb.Append("<span class=\"pill ").Append(sev).Append("\">").Append(f.Severity.ToUpperInvariant()).Append("</span>");
                sb.Append("<span class=\"mono rule\">").Append(Html(f.RuleId)).Append("</span>");
                sb.Append("<span class=\"title\">").Append(Html(f.RuleTitle)).Append("</span>");
                sb.Append("</div>");
                sb.Append("<p class=\"message\">").Append(Html(f.Message)).Append("</p>");
                if (!string.IsNullOrEmpty(f.Location))
                {
                    sb.Append("<p class=\"location\"><strong>Location:</strong> <span class=\"mono\">").Append(Html(f.Location)).Append("</span></p>");
                }
                if (!string.IsNullOrEmpty(f.Remediation))
                {
                    sb.Append("<p class=\"remediation\"><strong>Remediation:</strong> ").Append(Html(f.Remediation)).Append("</p>");
                }
                sb.Append("</article>");
            }
        }

        sb.Append("</section>");
    }

    private static void RenderFooter(StringBuilder sb)
    {
        sb.Append("<footer>");
        sb.Append("<p class=\"cta\">Want these fixed? Run <code>sentinel quote</code> to request a ");
        sb.Append("fixed-price remediation estimate from <a href=\"https://refinedelement.com\">Refined Element</a> ");
        sb.Append("— Kentico Community Leaders 2025 &amp; 2026.</p>");
        sb.Append("<p class=\"fine\">Report generated by Kentico Sentinel · MIT licensed · ");
        sb.Append("<a href=\"https://github.com/refined-element/kentico-sentinel\">github.com/refined-element/kentico-sentinel</a></p>");
        sb.Append("</footer>");
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "Error" => 2,
        "Warning" => 1,
        _ => 0,
    };

    private static string Html(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private const string Css = """
        :root {
            --bg: #0d1117;
            --panel: #161b22;
            --panel-border: #30363d;
            --text: #e6edf3;
            --muted: #8b949e;
            --accent: #4f46e5;
            --accent-2: #22d3ee;
            --error: #f85149;
            --warning: #d29922;
            --info: #8b949e;
            --ok: #3fb950;
        }
        * { box-sizing: border-box; }
        html, body { margin: 0; padding: 0; background: var(--bg); color: var(--text); font: 15px/1.55 -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
        body { max-width: 1100px; margin: 0 auto; padding: 32px 24px 64px; }
        a { color: var(--accent-2); text-decoration: none; }
        a:hover { text-decoration: underline; }
        code, .mono { font-family: ui-monospace, SFMono-Regular, Consolas, monospace; font-size: 13px; }

        .hero { border-bottom: 1px solid var(--panel-border); padding-bottom: 24px; margin-bottom: 28px; }
        .hero .brand { font-size: 22px; font-weight: 700; letter-spacing: -0.01em; }
        .hero .mark { color: var(--accent-2); margin-right: 4px; }
        .hero .tagline { color: var(--muted); margin-top: 4px; }
        .hero .meta { margin-top: 18px; display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 10px 24px; color: var(--muted); font-size: 13px; }
        .hero .meta span { color: var(--text); margin-right: 6px; font-weight: 600; }

        .summary { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; margin-bottom: 32px; }
        @media (max-width: 700px) { .summary { grid-template-columns: repeat(2, 1fr); } }
        .card { background: var(--panel); border: 1px solid var(--panel-border); border-radius: 10px; padding: 18px 20px; }
        .card .value { font-size: 28px; font-weight: 700; letter-spacing: -0.02em; }
        .card .label { color: var(--muted); font-size: 13px; margin-top: 4px; }
        .card.errors .value { color: var(--error); }
        .card.warnings .value { color: var(--warning); }
        .card.info .value { color: var(--info); }

        h2 { font-size: 18px; margin: 32px 0 12px; letter-spacing: -0.01em; }
        h3 { font-size: 15px; color: var(--muted); margin: 18px 0 10px; text-transform: uppercase; letter-spacing: 0.06em; font-weight: 600; }

        table { width: 100%; border-collapse: collapse; background: var(--panel); border: 1px solid var(--panel-border); border-radius: 10px; overflow: hidden; }
        th, td { padding: 10px 14px; text-align: left; font-size: 13px; }
        thead { background: rgba(255,255,255,0.02); }
        th { font-weight: 600; color: var(--muted); }
        tbody tr { border-top: 1px solid var(--panel-border); }

        .pill { display: inline-block; padding: 2px 8px; border-radius: 999px; font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.04em; }
        .pill.ran { background: rgba(63,185,80,0.12); color: var(--ok); }
        .pill.skippedruntime { background: rgba(139,148,158,0.15); color: var(--muted); }
        .pill.failed { background: rgba(248,81,73,0.12); color: var(--error); }
        .pill.error { background: rgba(248,81,73,0.14); color: var(--error); }
        .pill.warning { background: rgba(210,153,34,0.15); color: var(--warning); }
        .pill.info { background: rgba(139,148,158,0.15); color: var(--muted); }

        .finding { background: var(--panel); border: 1px solid var(--panel-border); border-left: 3px solid var(--info); border-radius: 8px; padding: 14px 16px; margin: 10px 0; }
        .finding.warning { border-left-color: var(--warning); }
        .finding.error { border-left-color: var(--error); }
        .finding .row { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; }
        .finding .rule { color: var(--muted); }
        .finding .title { font-weight: 600; }
        .finding .message { margin: 8px 0 6px; color: var(--text); }
        .finding .location, .finding .remediation { margin: 4px 0; color: var(--muted); font-size: 13px; }
        .finding .location strong, .finding .remediation strong { color: var(--text); font-weight: 600; }

        footer { margin-top: 48px; padding-top: 24px; border-top: 1px solid var(--panel-border); color: var(--muted); }
        footer .cta { font-size: 14px; color: var(--text); }
        footer code { background: var(--panel); padding: 2px 6px; border-radius: 4px; border: 1px solid var(--panel-border); }
        footer .fine { margin-top: 10px; font-size: 12px; }
        .empty { color: var(--ok); background: rgba(63,185,80,0.08); border: 1px solid rgba(63,185,80,0.25); padding: 16px; border-radius: 8px; }
        """;
}

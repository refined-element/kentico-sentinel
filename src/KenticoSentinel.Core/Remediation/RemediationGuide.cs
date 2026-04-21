using System.Collections.Frozen;

namespace RefinedElement.Kentico.Sentinel.Core.Remediation;

/// <summary>
/// Static catalog of rule-ID → remediation guidance, displayed alongside findings in the admin
/// UI so operators get a next-step suggestion without leaving the page. Intentionally frozen at
/// assembly load — each entry ships with the package, consumers don't edit it.
///
/// When a new check lands in <c>CheckRegistry</c>, add a matching entry here. A check without a
/// matching guide falls back to the generic message below; the admin UI still renders, just
/// without the specific "how to fix" copy.
/// </summary>
public static class RemediationGuide
{
    private static readonly FrozenDictionary<string, RemediationEntry> Entries = new Dictionary<string, RemediationEntry>(StringComparer.OrdinalIgnoreCase)
    {
        ["CFG001"] = new(
            "Set CMSHashStringSalt",
            "The `CMSHashStringSalt` is empty — Kentico uses it to sign links, tokens, and state payloads, and an empty value weakens cryptographic guarantees.",
            "Run `dotnet user-secrets set CMSHashStringSalt \"<new-guid>\"` locally and set the same key as an App Service configuration value in production. Never commit the value to source control."),

        ["CFG002"] = new(
            "Fix Kentico middleware ordering",
            "`UseWebOptimizer()` must run AFTER the Kentico trio (`InitKentico → UseStaticFiles → UseKentico`). Kentico Page Builder preview rewrites resource paths with a `/cmsctx/.../-/` prefix; placing WebOptimizer first means it never sees the rewritten paths and bundled CSS returns with empty MIME types.",
            "Move `app.UseWebOptimizer()` to directly after `app.UseKentico()` in `Program.cs`. See the Kentico middleware pipeline section in the CLAUDE.md for the full correct ordering."),

        ["CFG003"] = new(
            "Move plaintext secrets to user-secrets / Key Vault",
            "A connection string, API key, or similar sensitive value was found committed to `appsettings.json`. Anyone with repo access can read it; anyone with log access can leak it.",
            "Move the value to user-secrets (`dotnet user-secrets set`) locally and to Azure Key Vault / App Service configuration in production. Use a `@Microsoft.KeyVault(...)` reference so secrets never touch the repo."),

        ["DEP001"] = new(
            "Upgrade outdated NuGet packages",
            "One or more `PackageReference` entries lag the latest stable release. Staying current avoids CVEs, framework incompatibilities, and a big-bang upgrade cliff.",
            "Run `dotnet list package --outdated`, review the diff in each package's changelog, and bump `<PackageReference Version=\"...\" />`. Kentico packages: match the major version across all of them (`31.x` across the board)."),

        ["VER001"] = new(
            "Upgrade Kentico Xperience to the latest refresh",
            "Kentico ships a refresh each quarter with bug fixes, security patches, and new admin UI features. Older refreshes accumulate drift; upgrading later is harder than upgrading often.",
            "Follow the Kentico refresh guide (search docs.kentico.com for \"refresh\"). Bump all `Kentico.Xperience.*` packages together, run `dotnet build`, apply any database migrations, and smoke-test admin + frontend."),

        ["CNT001"] = new(
            "Archive or remove unused content types",
            "A content type exists in the admin but no items use it. Either it was experimental and should be removed, or it's genuinely unused and accumulating config surface the team has to maintain.",
            "Review with the content team: is this type intentional? If yes, ignore the finding (or suppress the rule). If no, delete the type via the admin Content types app. Acknowledge the finding if you've decided to keep it."),

        ["CNT002"] = new(
            "Remove orphaned reusable content items",
            "A reusable content item exists but no page or linked content references it. Orphans bloat the Content Hub and slow the admin's content-selection pickers.",
            "Open the content item in the admin. If obsolete, delete it. If still useful, link it from at least one consuming page or mark it explicitly as a shared asset. Acknowledge the finding if you accept the orphan."),

        ["CNT003"] = new(
            "Review stale content",
            "A content item hasn't been edited in longer than the configured staleness threshold (default 180 days). Could be evergreen, could be forgotten — operator decides.",
            "Open the content item and check relevance. Refresh the publish-date, edit the body, or archive. Adjust `Sentinel:RuntimeChecks:StaleDays` to match your editorial cadence."),

        ["CNT004"] = new(
            "Fix broken asset references",
            "A content item references an asset (image, file) that no longer exists in the media library. Site visitors will see broken images or 404s on download links.",
            "Open the content item and either re-upload / repoint the asset, or remove the broken reference. Look for a pattern — if many items share a broken asset, the asset itself was likely deleted; restore it from backup if possible."),

        ["CNT005"] = new(
            "Clean up malformed Page Builder widgets",
            "A Page Builder widget instance has missing or invalid properties (e.g., required field blank, referenced content deleted). The page either renders broken or 500s when published.",
            "Edit the page in Page Builder, open the flagged widget, and refill required properties. If the widget is obsolete, remove it. Cascading widget bugs often share a root cause — if many pages trip the same widget, review the widget's property model."),

        ["CNT006"] = new(
            "Triage recent CMS_EventLog errors",
            "The event log has error-level entries from the last N days (default 7). Kentico writes exceptions here — some are transient, some are chronic.",
            "Open the Kentico Event log app and filter by the source reported in the finding. Group repeated entries by exception type. Deploy fixes for anything reproducible; open an issue for anything that looks environmental."),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Fallback used when a finding's rule ID has no specific guidance. Keeps the admin UI
    /// consistent — every finding renders a "how to fix" panel, even when we haven't written
    /// rule-specific copy yet.
    /// </summary>
    public static readonly RemediationEntry GenericFallback = new(
        "Review and remediate",
        "This finding doesn't have a rule-specific guidance entry yet. The finding message + location should give enough to investigate.",
        "Inspect the flagged item, decide whether it's a real issue, and either fix it or acknowledge the finding if you've judged it acceptable for your project.");

    /// <summary>Returns the guidance entry for <paramref name="ruleId"/>, or the generic fallback.</summary>
    public static RemediationEntry For(string ruleId) =>
        Entries.TryGetValue(ruleId ?? string.Empty, out var entry) ? entry : GenericFallback;

    /// <summary>Returns the guidance entry only if a rule-specific one exists — null otherwise.</summary>
    public static RemediationEntry? TryFor(string ruleId) =>
        Entries.TryGetValue(ruleId ?? string.Empty, out var entry) ? entry : null;

    /// <summary>All registered rule IDs — for docs generation, admin UI autocomplete, etc.</summary>
    public static IReadOnlyCollection<string> KnownRuleIds => Entries.Keys;
}

/// <summary>
/// Structured guidance for a single rule — short title, explanation, and actionable next steps.
/// </summary>
public sealed record RemediationEntry(string Title, string Summary, string Steps);

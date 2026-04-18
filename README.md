# Kentico Sentinel

<img src="docs/logo.svg" alt="Kentico Sentinel" width="48" align="left" style="margin-right:12px">

> **Health scanner for Xperience by Kentico projects.** ESLint for XbyK.

Free, open-source CLI that scans any Xperience by Kentico project and reports unused content types, orphaned content items, outdated packages, config smells, stale assets, recent `CMS_EventLog` errors, and a dozen other things the Kentico admin UI doesn't surface.

Built by [Refined Element](https://refinedelement.com) — Kentico Community Leaders 2025 & 2026.

### Supported Kentico versions

| Version | Supported | Notes |
|---------|-----------|-------|
| **Xperience by Kentico 29+** | ✅ Full support | The tool is built for XbyK. All 11 checks run. |
| Kentico Xperience 13 | ❌ Not supported | KX13 uses the legacy content tree (`CMS_Document` / `CMS_Tree`) and ASP.NET 4.x config patterns. A separate scanner would be needed; we have no plans to build one. |
| Kentico 12 and earlier | ❌ Not supported | End of mainstream support. |

If you're on KX13 or older, this tool won't help you. Please don't [open an issue](https://github.com/refined-element/kentico-sentinel/issues) asking us to backport.

## Quick Start

```bash
# Prerelease until v1.0 — use --prerelease or an explicit version.
dotnet tool install -g RefinedElement.Kentico.Sentinel --prerelease

# Static code checks only (works against XbyK 29+)
sentinel scan --path ./MyXperienceSite

# Full scan (code + runtime content checks — requires an XbyK database)
sentinel scan --path ./MyXperienceSite --connection-string "Server=...;Database=..."

# Scan a GitHub repo directly (shallow-cloned to a temp dir, cleaned up after)
sentinel scan --repo owner/your-xbyk-site

# Email the sanitized report to Refined Element for a one-time remediation quote
sentinel quote --report ./sentinel-report/report.json
```

## Convenience scripts

If your project uses `dotnet user-secrets` for its connection string (the Kentico-recommended default),
the wrapper script resolves it automatically:

```powershell
./scripts/scan.ps1 -Project F:\RefinedElement\re-xbk -StaleDays 365 -OpenReport
```

Iterating on the scanner itself? `scripts/dev-reinstall.ps1` packs the current source, reinstalls
the global tool, and leaves you ready to re-run `sentinel`.

## What you'll see

A real scan against a production XbyK 31.0.1 site takes about **3 seconds** end-to-end and yields
output like:

```
╭──────────────────────────┬──────────────────────────╮
│ Metric                   │ Value                    │
├──────────────────────────┼──────────────────────────┤
│ Repo                     │ F:\RefinedElement\re-xbk │
│ Runtime checks           │ enabled                  │
│ Duration                 │ 3.25s                    │
│ Checks executed          │ 10                       │
│ Errors                   │ 0                        │
│ Warnings                 │ 3                        │
│ Info                     │ 12                       │
╰──────────────────────────┴──────────────────────────╯
  WARNING  CFG003  '_comment_secrets' contains a plaintext secret…  (false-positive — now suppressed)
  WARNING  DEP001  Stripe.net: 50.1.0 → 51.0.0
  WARNING  DEP001  Microsoft.EntityFrameworkCore.SqlServer: 9.0.0 → 10.0.6
  INFO     CNT001  Content type 'LandingPage' has zero content items.
  INFO     CNT002  Reusable content item 'Content_TestBlogPost-…' has no inbound references.
  INFO     VER001  Kentico.Xperience.WebApp is on 31.0.1; latest is 31.4.0.
```

The HTML report is self-contained (no external CSS/JS) and Refined Element-branded.

## What It Checks (v1)

### Static — free, no database needed

| # | Check | Why it matters |
|---|-------|----------------|
| 1 | **XbyK version** vs. latest, with known CVE flags | Catch security-relevant version drift |
| 2 | **Outdated NuGet packages** (severity-ranked) | Stay ahead of patch-level risk |
| 3 | **Config smells** — empty `CMSHashStringSalt`, wrong middleware order, missing Key Vault refs | Prevent subtle prod breakage |
| 4 | **Duplicate / inconsistent content-type field definitions** | Keep the content model clean |
| 5 | **Page Builder widgets registered but never placed** | Remove dead code paths |

### Runtime — free, requires DB connection string or Management API credentials

| # | Check | Why it matters |
|---|-------|----------------|
| 6 | **Unused content types** (0 items) | Find candidates for cleanup |
| 7 | **Orphaned content items** (0 page or reusable references) | Reclaim the content tree |
| 8 | **Stale content** (no edits in N days, configurable) | Identify what needs refreshing |
| 9 | **Broken asset references** | Fix images before users hit them |
| 10 | **Widgets configured with invalid / missing properties** | Catch dead presentation data |

## Output

Every scan produces:

- An **HTML report** (`sentinel-report/report.html`) — human-readable, grouped by severity, with actionable guidance for each finding
- A **JSON report** (`sentinel-report/report.json`) — stable schema, CI-friendly, consumed by the `quote` command

## `sentinel quote` — one-click remediation quote

Every report ends with: *"Want Refined Element to fix these? Run `sentinel quote`."* The command POSTs a **sanitized summary** (counts + rule IDs — no source code excerpts by default) to Refined Element, which replies with an itemized, fixed-price quote based on the findings.

Opt in to richer context with `--include-context` for a more accurate quote.

## Roadmap

**v1 (this release)** — everything above. Free, MIT-licensed, local CLI.

**v2 (paid SaaS, coming later in 2026)** — connect your XbyK site, continuous monitoring, auto-remediation (unpublish stale items, open PRs for code fixes), team dashboard, CI integration.

**v3 (self-healing mode)** — analytics-driven content revisions, A/B testing via channel variants, SEO auto-remediation, broken-link repair.

## License

MIT © 2026 [Refined Element](https://refinedelement.com)

## Contributing

Issues and PRs welcome. New check ideas especially — the goal is to be **the** XbyK scanner.

### Dev loop

```bash
dotnet build              # quick compile
dotnet test               # 19 unit tests across checks, sanitizer, and runner
./scripts/dev-reinstall.ps1  # pack + reinstall the global tool
./scripts/scan.ps1 -Project F:\RefinedElement\re-xbk  # verify against a real site
```

Each check is a single class in `src/KenticoSentinel/Checks/` implementing `ICheck`. Register it in
`Core/CheckRegistry.cs` and it ships in the next run.

## Links

- [Refined Element](https://refinedelement.com) — the consultancy
- [KDaaS](https://kentico-developer.com) — AI-powered Kentico dev service
- [Xperience by Kentico](https://www.kentico.com/)

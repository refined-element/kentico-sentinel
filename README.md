# Kentico Sentinel

<img src="docs/logo.svg" alt="Kentico Sentinel" width="48" align="left" style="margin-right:12px">

> **Health scanner for Xperience by Kentico projects.** ESLint for XbyK.

Free, open-source. Ships in two forms:

- **Embedded NuGet** for your XbyK site — installs alongside the app, runs on Kentico's scheduler, persists findings to custom tables, mirrors summaries to `CMS_EventLog`, emails digests. Zero-config defaults.
- **CLI tool** for one-shot scans from a terminal or CI — same check suite, HTML + JSON reports, remote GitHub-repo mode.

Built by [Refined Element](https://refinedelement.com) — Kentico Community Leaders 2025 & 2026.

### Supported Kentico versions

| Version | Supported | Notes |
|---------|-----------|-------|
| **Xperience by Kentico 31.x** | ✅ Full support | The embedded NuGet targets 31.x explicitly. CLI also supports 29+. |
| **Xperience by Kentico 29–30** | ✅ CLI only | Use the CLI until we ship a 29/30-compatible embedded build. |
| Kentico Xperience 13 | ❌ Not supported | KX13 uses the legacy content tree (`CMS_Document` / `CMS_Tree`) and ASP.NET 4.x config patterns. A separate scanner would be needed; we have no plans to build one. |
| Kentico 12 and earlier | ❌ Not supported | End of mainstream support. |

If you're on KX13 or older, this tool won't help you. Please don't [open an issue](https://github.com/refined-element/kentico-sentinel/issues) asking us to backport.

## Install in an XbyK site (recommended)

Drop one NuGet reference into your XbyK project, wire one line in `Program.cs`, and Kentico takes care of scheduling, persistence, and the event-log mirror.

### 1. Reference the package

```xml
<PackageReference Include="RefinedElement.Kentico.Sentinel.XbyK" Version="0.2.3-alpha" />
```

### 2. Register the services

In `Program.cs`, after `builder.Services.AddKentico(...)`:

```csharp
using RefinedElement.Kentico.Sentinel.XbyK.DependencyInjection;

builder.Services.AddKenticoSentinel(builder.Configuration);
```

### 3. Configure (optional — every field has a sensible default)

In `appsettings.json`:

```jsonc
"Sentinel": {
  "Enabled": true,
  "Checks": { "Excluded": [] },
  "RuntimeChecks": {
    "ConnectionString": "",   // blank = reuse CMSConnectionString
    "StaleDays": 365,
    "EventLogDays": 7
  },
  "EventLogIntegration": {
    "Enabled": true,
    "SeverityThreshold": "Warning",   // Info | Warning | Error
    "MaxEntriesPerScan": 50
  },
  "EmailDigest": {
    "Enabled": false,
    "Recipients": [],
    "SeverityThreshold": "Warning",
    "OnlyWhenThresholdFindings": true
  }
}
```

### 4. First run

On the next app-start Sentinel's installer upserts three tables (`RefinedElement_SentinelScanRun`, `RefinedElement_SentinelFinding`, `RefinedElement_SentinelFindingAck`) in the CMS database. The scheduled task class registers automatically.

Open **Configuration → Scheduled tasks** in Kentico admin, create a new task with implementation `RefinedElement.SentinelScan` (the dropdown list), set a cadence, save, enable. Hit **Execute now** to run the first scan.

Cadence lives in Kentico's Scheduled Tasks UI — no cron config in code.

### 5. Where output lands

- **`RefinedElement_SentinelScanRun`** — one row per scan execution (trigger, duration, error/warning/info counts, status)
- **`RefinedElement_SentinelFinding`** — one row per finding with a stable fingerprint for cross-scan acknowledgments
- **`CMS_EventLog`** — summary entry per scan (source = `Sentinel`) + one entry per finding at or above `SeverityThreshold`, up to `EventLogIntegration.MaxEntriesPerScan`; if more findings qualify, Sentinel writes a single additional summary noting the suppressed event-log entries

### 6. Admin UI *(v0.3.0-alpha, in review)*

The companion package `RefinedElement.Kentico.Sentinel.XbyK.Admin` adds **Configuration → Sentinel** to the admin left-nav with listing pages for Scan history and Findings. Drop the reference in, no extra DI wiring needed.

## CLI (alternative / CI)

Same checks, one-shot mode, no install in your XbyK project.

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

**v0.2.x (current alpha)** — embedded-mode NuGet for XbyK 31.x with headless scheduled scanning, custom-table persistence, `CMS_EventLog` mirror, optional HTML email digest. CLI in parity.

**v0.3.x (next alpha)** — admin UI module: Scan history + Findings listing pages, then custom Dashboard + Contact Refined Element form. Separate `…XbyK.Admin` NuGet so headless deploys can skip it.

**v1.x (stable)** — API freeze. Same feature surface, no more breaking changes between minor versions. Free, MIT-licensed.

**v2.x (paid add-ons)** — automatic remediation via PR bot (small refactors, dep bumps, config fixes), multi-site dashboard, Slack / PagerDuty integration. Core checks remain free.

**v3.x (self-healing)** — content-side automation: unpublish stale items on a cadence, broken-link repair, SEO auto-remediation backed by your analytics.

## License

MIT © 2026 [Refined Element](https://refinedelement.com)

## Contributing

Issues and PRs welcome. New check ideas especially — the goal is to be **the** XbyK scanner.

### Dev loop

```bash
dotnet build KenticoSentinel.slnx   # full solution: Core + XbyK + CLI + tests
dotnet test KenticoSentinel.slnx    # 36+ unit tests — checks, sanitizer, runner, notifiers
./scripts/dev-reinstall.ps1         # CLI: pack + reinstall the global tool
./scripts/scan.ps1 -Project F:\RefinedElement\re-xbk  # verify against a real site
```

### Project layout

| Project | Purpose |
|---|---|
| `src/KenticoSentinel.Core` | Check engine, registry, sanitizer, reporting. Framework-agnostic. |
| `src/KenticoSentinel.XbyK` | Embedded XbyK integration — Info models, installer, scheduled task, notifiers. |
| `src/KenticoSentinel.XbyK.Admin` | *(planned / not yet in this repo)* Admin UI — listing pages. Optional. |
| `src/KenticoSentinel` | CLI tool (`sentinel`). |
| `tests/KenticoSentinel.Tests` | xUnit tests across all packages. |

Each check is a single class in `src/KenticoSentinel.Core/Checks/` implementing `ICheck`. Register it in `Core/CheckRegistry.cs` and it ships in the next run of both the CLI and the embedded scheduled task.

## Links

- [Refined Element](https://refinedelement.com) — the consultancy
- [KDaaS](https://kentico-developer.com) — AI-powered Kentico dev service
- [Xperience by Kentico](https://www.kentico.com/)

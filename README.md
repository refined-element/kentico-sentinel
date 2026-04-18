# Kentico Sentinel

> Static + runtime health scanner for Xperience by Kentico projects. **ESLint for Kentico.**

Free, open-source CLI that scans any XbyK project and reports unused content types, orphaned content items, outdated packages, config smells, stale assets, and a dozen other things the Kentico admin UI doesn't surface.

Built by [Refined Element](https://refinedelement.com) — AI-driven Xperience by Kentico consultancy, Kentico Community Leaders 2025 & 2026.

## Quick Start

```bash
dotnet tool install -g RefinedElement.Kentico.Sentinel

# Static code checks only
sentinel scan --path ./MyKenticoSite

# Full scan (code + runtime content checks)
sentinel scan --path ./MyKenticoSite --connection-string "Server=...;Database=..."

# Email the sanitized report to Refined Element for a one-time remediation quote
sentinel quote --report ./sentinel-report/report.json
```

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

## Links

- [Refined Element](https://refinedelement.com) — the consultancy
- [KDaaS](https://kentico-developer.com) — AI-powered Kentico dev service
- [Xperience by Kentico](https://www.kentico.com/)

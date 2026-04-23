# RefinedElement.Kentico.Sentinel.Core

> **Reusable check library powering Kentico Sentinel.** Embeddable in any .NET host — CLI, admin module, ASP.NET Core background worker, GitHub Action.

This is the framework-agnostic core of [Kentico Sentinel](https://github.com/refined-element/kentico-sentinel) — the health scanner for Xperience by Kentico projects. It ships the primitives everyone else builds on:

- `ICheck` — a single health check (config smell, outdated NuGet, orphan content item, etc.)
- `ScanContext` — the state a check reads / writes during a run
- `CheckRegistry` — the default suite of static and runtime checks
- `ScanRunner` — orchestrates a full scan and emits a sanitized report
- `Reporting` — HTML + JSON report writers, stable schema
- `Quoting` — sanitized payload builder for the `sentinel quote` flow

Use this package directly when you want to run Sentinel's checks inside your own host — for example, a custom dashboard, a webhook, or a bespoke CI script. For the turn-key experiences, reach for the integration packages instead:

- **`RefinedElement.Kentico.Sentinel.XbyK`** — headless XbyK integration (scheduled task, event log mirror, email digest)
- **`RefinedElement.Kentico.Sentinel.XbyK.Admin`** — admin UI module
- **`RefinedElement.Kentico.Sentinel`** — `sentinel` dotnet global tool for CI / local use

## Install

```xml
<PackageReference Include="RefinedElement.Kentico.Sentinel.Core" Version="0.4.3-alpha" />
```

## Minimal usage

```csharp
using RefinedElement.Kentico.Sentinel.Core;

var registry = new CheckRegistry();
var runner = new ScanRunner(registry);

var context = new ScanContext
{
    ProjectPath = @"C:\src\MyXperienceSite",
    // Optional — enables runtime checks (unused content types, stale content, etc.)
    ConnectionString = "Server=.;Database=XbyK;Integrated Security=true"
};

var result = await runner.RunAsync(context);

foreach (var finding in result.Findings)
{
    Console.WriteLine($"{finding.Severity} {finding.RuleId} {finding.Message}");
}
```

## What it checks

The same suite the CLI and embedded integration run — static config / dependency / content-model checks out of the box, plus runtime checks (unused types, orphans, stale content, broken assets) when a connection string is supplied. See the [main repo README](https://github.com/refined-element/kentico-sentinel#what-it-checks-v1) for the full list and configuration reference.

## Supported versions

Targets `.NET 9`. Check coverage targets **Xperience by Kentico 29+** for static checks and **XbyK 31.x** for runtime checks. KX13 is **not** supported.

## License

MIT © [Refined Element](https://refinedelement.com)

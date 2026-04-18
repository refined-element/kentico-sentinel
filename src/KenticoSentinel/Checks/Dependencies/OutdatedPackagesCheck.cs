using System.Diagnostics;
using System.Text.Json;
using RefinedElement.Kentico.Sentinel.Core;

namespace RefinedElement.Kentico.Sentinel.Checks.Dependencies;

/// <summary>
/// DEP001 — Shells out to `dotnet list package --outdated --format json` in the repo root and reports
/// every outdated package. Major-version drift is a Warning; minor/patch is Info.
/// </summary>
public sealed class OutdatedPackagesCheck : ICheck
{
    public string RuleId => "DEP001";
    public string Title => "Outdated NuGet packages";
    public string Category => "Dependencies";
    public CheckKind Kind => CheckKind.Static;

    public async Task<IReadOnlyList<Finding>> RunAsync(ScanContext context, CancellationToken cancellationToken)
    {
        var findings = new List<Finding>();
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { "list", "package", "--outdated", "--format", "json" },
            WorkingDirectory = context.RepoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            findings.Add(new Finding(
                RuleId, Title, Category, Severity.Info,
                "Could not start `dotnet list package`. Install the .NET SDK to enable this check.",
                Location: context.RepoPath));
            return findings;
        }

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
        {
            // Non-fatal: a fresh repo without restore, or no projects found. Surface as Info.
            findings.Add(new Finding(
                RuleId, Title, Category, Severity.Info,
                "`dotnet list package --outdated` produced no output. Run `dotnet restore` first, or verify a .sln/.csproj is at the repo root.",
                Location: context.RepoPath));
            return findings;
        }

        try
        {
            using var doc = JsonDocument.Parse(stdout);
            if (!doc.RootElement.TryGetProperty("projects", out var projects)) return findings;

            foreach (var project in projects.EnumerateArray())
            {
                var projectPath = project.TryGetProperty("path", out var p) ? p.GetString() : null;
                if (!project.TryGetProperty("frameworks", out var frameworks)) continue;

                foreach (var fw in frameworks.EnumerateArray())
                {
                    if (!fw.TryGetProperty("topLevelPackages", out var packages)) continue;

                    foreach (var pkg in packages.EnumerateArray())
                    {
                        var id = pkg.GetProperty("id").GetString() ?? "(unknown)";
                        var resolved = pkg.GetProperty("resolvedVersion").GetString() ?? "(unknown)";
                        var latest = pkg.GetProperty("latestVersion").GetString() ?? "(unknown)";
                        var severity = ClassifyDrift(resolved, latest);

                        findings.Add(new Finding(
                            RuleId, Title, Category,
                            severity,
                            $"{id}: {resolved} → {latest}",
                            Location: projectPath,
                            Remediation: $"Update with `dotnet add {projectPath} package {id} --version {latest}` after validating compatibility."));
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            findings.Add(new Finding(
                RuleId, Title, Category, Severity.Warning,
                $"Could not parse `dotnet list package` output: {ex.Message}",
                Location: context.RepoPath));
        }

        return findings;
    }

    private static Severity ClassifyDrift(string current, string latest)
    {
        // Extract leading major version from each. Bail out to Info on parse failure.
        if (!TryGetMajor(current, out int curMajor) || !TryGetMajor(latest, out int latMajor))
        {
            return Severity.Info;
        }

        return latMajor > curMajor ? Severity.Warning : Severity.Info;
    }

    private static bool TryGetMajor(string version, out int major)
    {
        major = 0;
        var dot = version.IndexOf('.');
        var leading = dot >= 0 ? version[..dot] : version;
        return int.TryParse(leading, out major);
    }
}

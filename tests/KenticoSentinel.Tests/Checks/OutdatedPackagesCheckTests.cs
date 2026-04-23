using RefinedElement.Kentico.Sentinel.Checks.Dependencies;
using RefinedElement.Kentico.Sentinel.Core;

namespace KenticoSentinel.Tests.Checks;

public class OutdatedPackagesCheckTests
{
    /// <summary>
    /// Regression for the DEP001 prerelease bug: when `dotnet list package --outdated --format json`
    /// emits <c>"Not found at the sources"</c> for a package whose resolved version is a prerelease
    /// (e.g. <c>0.4.3-alpha</c>), the check should fall back to an <see cref="INuGetVersionLookup"/>
    /// with <c>includePrerelease: true</c>. If the prerelease lookup returns the same version that's
    /// installed, the package is on the latest prerelease and no finding should be emitted.
    /// </summary>
    [Fact]
    public async Task Prerelease_installed_on_latest_prerelease_emits_no_finding()
    {
        const string json = """
        {
          "version": 1,
          "parameters": "--outdated",
          "sources": [ "https://api.nuget.org/v3/index.json" ],
          "projects": [
            {
              "path": "C:/repo/example.csproj",
              "frameworks": [
                {
                  "framework": "net9.0",
                  "topLevelPackages": [
                    {
                      "id": "RefinedElement.Kentico.Sentinel.XbyK",
                      "requestedVersion": "0.4.3-alpha",
                      "resolvedVersion": "0.4.3-alpha",
                      "latestVersion": "Not found at the sources"
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var lookup = new StubVersionLookup(latestPrerelease: "0.4.3-alpha");
        var check = new OutdatedPackagesCheck(lookup);

        var findings = await check.ParseAndBuildFindingsAsync(json, "C:/repo", CancellationToken.None);

        Assert.Empty(findings);
        Assert.True(lookup.WasCalledWithPrerelease,
            "When resolved version is a prerelease, the lookup must be called with includePrerelease: true.");
    }

    /// <summary>
    /// When the installed prerelease version is older than the latest prerelease on nuget.org,
    /// the check should report drift using the prerelease-aware latest — NOT the raw
    /// "Not found at the sources" sentinel.
    /// </summary>
    [Fact]
    public async Task Prerelease_installed_with_newer_prerelease_available_reports_drift()
    {
        const string json = """
        {
          "projects": [
            {
              "path": "C:/repo/example.csproj",
              "frameworks": [
                {
                  "framework": "net9.0",
                  "topLevelPackages": [
                    {
                      "id": "RefinedElement.Kentico.Sentinel.XbyK",
                      "requestedVersion": "0.4.3-alpha",
                      "resolvedVersion": "0.4.3-alpha",
                      "latestVersion": "Not found at the sources"
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var lookup = new StubVersionLookup(latestPrerelease: "0.5.0-alpha");
        var check = new OutdatedPackagesCheck(lookup);

        var findings = await check.ParseAndBuildFindingsAsync(json, "C:/repo", CancellationToken.None);

        Assert.Single(findings);
        Assert.Contains("0.5.0-alpha", findings[0].Message);
        Assert.DoesNotContain("Not found at the sources", findings[0].Message);
    }

    /// <summary>
    /// Guards against regression: when the installed version is stable, the prerelease fallback
    /// must NOT be triggered. Stable consumers should see the existing behavior (the raw latest
    /// from dotnet list, including the "Not found" sentinel when applicable — that scenario is
    /// a genuine "package gone" case for stable-installed packages).
    /// </summary>
    [Fact]
    public async Task Stable_installed_does_not_invoke_prerelease_fallback()
    {
        const string json = """
        {
          "projects": [
            {
              "path": "C:/repo/example.csproj",
              "frameworks": [
                {
                  "framework": "net9.0",
                  "topLevelPackages": [
                    {
                      "id": "Some.Stable.Package",
                      "requestedVersion": "1.0.0",
                      "resolvedVersion": "1.0.0",
                      "latestVersion": "2.0.0"
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var lookup = new StubVersionLookup(latestPrerelease: "3.0.0-preview");
        var check = new OutdatedPackagesCheck(lookup);

        var findings = await check.ParseAndBuildFindingsAsync(json, "C:/repo", CancellationToken.None);

        Assert.False(lookup.WasCalledWithPrerelease,
            "Prerelease fallback must only fire when the installed version itself is a prerelease.");
        Assert.Single(findings);
        Assert.Contains("1.0.0 → 2.0.0", findings[0].Message);
    }

    private sealed class StubVersionLookup : INuGetVersionLookup
    {
        private readonly string? _latestPrerelease;

        public StubVersionLookup(string? latestPrerelease)
        {
            _latestPrerelease = latestPrerelease;
        }

        public bool WasCalledWithPrerelease { get; private set; }

        public Task<string?> GetLatestVersionAsync(string packageId, bool includePrerelease, CancellationToken cancellationToken)
        {
            if (includePrerelease)
            {
                WasCalledWithPrerelease = true;
                return Task.FromResult<string?>(_latestPrerelease);
            }
            return Task.FromResult<string?>(null);
        }
    }
}

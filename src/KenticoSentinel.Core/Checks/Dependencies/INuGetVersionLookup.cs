namespace RefinedElement.Kentico.Sentinel.Checks.Dependencies;

/// <summary>
/// Abstraction over the NuGet "latest version for a package" lookup. Exists so <see cref="OutdatedPackagesCheck"/>
/// can ask for the latest *prerelease* when the installed version is itself a prerelease — which `dotnet list
/// package --outdated` silently refuses to do, reporting "Not found at the sources" instead. Injectable so
/// tests can swap in a stub that doesn't hit nuget.org.
/// </summary>
public interface INuGetVersionLookup
{
    /// <summary>
    /// Returns the latest version of <paramref name="packageId"/> published to nuget.org, or null when the
    /// package is not found or the lookup fails. When <paramref name="includePrerelease"/> is true, prerelease
    /// versions (those with a `-` suffix like `0.4.3-alpha`) are considered.
    /// </summary>
    Task<string?> GetLatestVersionAsync(string packageId, bool includePrerelease, CancellationToken cancellationToken);
}

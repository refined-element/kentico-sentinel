using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace RefinedElement.Kentico.Sentinel.Checks.Dependencies;

/// <summary>
/// Default <see cref="INuGetVersionLookup"/> — talks to nuget.org via the NuGet.Protocol metadata resource.
/// Used by <see cref="OutdatedPackagesCheck"/> as a fallback when `dotnet list package --outdated` returns
/// "Not found at the sources" for a prerelease-installed package.
/// </summary>
public sealed class NuGetOrgVersionLookup : INuGetVersionLookup
{
    private static readonly SourceRepository Repository =
        NuGet.Protocol.Core.Types.Repository.Factory.GetCoreV3(new PackageSource("https://api.nuget.org/v3/index.json"));

    public async Task<string?> GetLatestVersionAsync(string packageId, bool includePrerelease, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await Repository.GetResourceAsync<MetadataResource>(cancellationToken).ConfigureAwait(false);
            using var cacheContext = new SourceCacheContext();
            var latest = await metadata.GetLatestVersion(
                packageId,
                includePrerelease: includePrerelease,
                includeUnlisted: false,
                sourceCacheContext: cacheContext,
                log: NullLogger.Instance,
                token: cancellationToken).ConfigureAwait(false);

            return latest?.ToNormalizedString();
        }
        catch (Exception)
        {
            // Network/protocol failures are non-fatal — the caller falls back to the original "Not found" message.
            return null;
        }
    }
}

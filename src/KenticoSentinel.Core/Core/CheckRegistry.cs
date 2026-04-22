using RefinedElement.Kentico.Sentinel.Checks.Configuration;
using RefinedElement.Kentico.Sentinel.Checks.Content;
using RefinedElement.Kentico.Sentinel.Checks.Dependencies;

namespace RefinedElement.Kentico.Sentinel.Core;

/// <summary>
/// Central list of every built-in check. Add new checks here — in one place — so they ship in the default scan.
/// </summary>
public static class CheckRegistry
{
    public static IReadOnlyList<ICheck> BuiltIn() =>
    [
        // Static (code-only)
        new HashStringSaltCheck(),           // CFG001
        new MiddlewareOrderCheck(),          // CFG002
        new PlaintextSecretsCheck(),         // CFG003
        new OutdatedPackagesCheck(),         // DEP001
        new KenticoVersionCheck(),           // VER001
        // Runtime (DB-connected)
        new UnusedContentTypesCheck(),       // CNT001
        new OrphanedContentItemsCheck(),     // CNT002
        new StaleContentCheck(),             // CNT003
        new OrphanedMediaCheck(),            // CNT004
        new MalformedWidgetsCheck(),         // CNT005
        new EventLogCheck(),                 // CNT006
        new UnusedImagesCheck(),             // CNT010
        new UnusedDocumentsCheck(),          // CNT011
    ];
}

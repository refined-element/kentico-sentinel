namespace RefinedElement.Kentico.Sentinel.Core;

/// <summary>
/// Inputs made available to every check during a scan. Immutable after construction.
/// </summary>
public sealed record ScanContext
{
    /// <summary>Absolute path to the Xperience by Kentico project root (folder containing the .csproj).</summary>
    public required string RepoPath { get; init; }

    /// <summary>SQL Server connection string. When null, runtime checks are skipped.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>Stale-content threshold in days. Items not edited in this window are flagged by CNT003.</summary>
    public int StaleDays { get; init; } = 180;

    /// <summary>HTTP client factory for checks that reach out to external services (NuGet feed, Kentico release feed).</summary>
    public required IHttpClientFactory HttpClientFactory { get; init; }

    /// <summary>True when a connection string is present and runtime checks should execute.</summary>
    public bool RuntimeEnabled => !string.IsNullOrWhiteSpace(ConnectionString);
}

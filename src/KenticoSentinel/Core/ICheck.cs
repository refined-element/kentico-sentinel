namespace RefinedElement.Kentico.Sentinel.Core;

/// <summary>
/// Every check implements this interface. Adding a new check means dropping one class into Checks/
/// and registering it in <see cref="CheckRegistry"/>.
/// </summary>
public interface ICheck
{
    /// <summary>Stable rule identifier (e.g. "CFG001"). Never reused, never renumbered.</summary>
    string RuleId { get; }

    /// <summary>Short title shown in reports.</summary>
    string Title { get; }

    /// <summary>Category for grouping in the HTML report.</summary>
    string Category { get; }

    /// <summary>Whether the check runs against the source tree only (Static) or requires a DB connection (Runtime).</summary>
    CheckKind Kind { get; }

    /// <summary>
    /// Execute the check. Must not throw for expected failure modes — encode them as findings.
    /// Unexpected exceptions are caught by the runner and reported as a CheckFailed finding.
    /// </summary>
    Task<IReadOnlyList<Finding>> RunAsync(ScanContext context, CancellationToken cancellationToken);
}

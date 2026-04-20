using Microsoft.Data.SqlClient;
using RefinedElement.Kentico.Sentinel.Core;

namespace RefinedElement.Kentico.Sentinel.Checks.Content;

/// <summary>
/// CNT002 — Reusable content items with no inbound references (CMS_ContentItemReference.TargetItemID).
/// These are orphans: nothing points to them, nothing uses them, and they quietly accrue storage and noise.
/// </summary>
public sealed class OrphanedContentItemsCheck : ICheck
{
    public string RuleId => "CNT002";
    public string Title => "Orphaned reusable content items";
    public string Category => "Content Model";
    public CheckKind Kind => CheckKind.Runtime;

    public async Task<IReadOnlyList<Finding>> RunAsync(ScanContext context, CancellationToken cancellationToken)
    {
        var findings = new List<Finding>();

        const string sql = @"
            SELECT ci.ContentItemID, ci.ContentItemName, c.ClassDisplayName
            FROM CMS_ContentItem ci
            INNER JOIN CMS_Class c ON c.ClassID = ci.ContentItemContentTypeID
            LEFT JOIN CMS_ContentItemReference r ON r.ContentItemReferenceTargetItemID = ci.ContentItemID
            WHERE ci.ContentItemIsReusable = 1
              AND r.ContentItemReferenceID IS NULL
            ORDER BY c.ClassDisplayName, ci.ContentItemName;";

        await using var connection = new SqlConnection(context.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var itemId = reader.GetInt32(0);
            var itemName = reader.GetString(1);
            var typeName = reader.GetString(2);

            findings.Add(new Finding(
                RuleId, Title, Category, Severity.Info,
                $"Reusable content item '{itemName}' ({typeName}) has no inbound references.",
                Location: $"CMS_ContentItem.ContentItemID={itemId}",
                Remediation: "Review in Content Hub. If genuinely unused, delete or archive. Paid tier can bulk-unpublish these after an age threshold."));
        }

        return findings;
    }
}

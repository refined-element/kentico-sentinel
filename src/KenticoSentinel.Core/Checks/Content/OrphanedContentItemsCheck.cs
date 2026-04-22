using Microsoft.Data.SqlClient;
using RefinedElement.Kentico.Sentinel.Core;

namespace RefinedElement.Kentico.Sentinel.Checks.Content;

/// <summary>
/// CNT002 — Content hub items with no inbound references AND last modified longer ago than
/// <see cref="ScanContext.StaleDays"/>. The age filter is the practical difference between "this
/// is genuinely forgotten content you can safely delete" and "an editor made this yesterday and
/// just hasn't wired it up yet"; without it the rule produces a flood of false positives on any
/// active content-hub workflow.
///
/// <para>
/// Detection model: reusable content items are "used" when some other content item references
/// them via <c>CMS_ContentItemReference</c>. Web pages, reusable-field links, and widget blobs
/// all flow through that join table, so a row that has zero inbound references is a strong
/// signal nothing points at it. Combined with "hasn't been touched in 6 months" (default
/// threshold via <see cref="ScanContext.StaleDays"/>), these are the safe-to-delete candidates
/// operators actually want a list of — images, documents, and reusable content alike.
/// </para>
/// </summary>
public sealed class OrphanedContentItemsCheck : ICheck
{
    public string RuleId => "CNT002";
    public string Title => "Stale unused content hub items";
    public string Category => "Content Model";
    public CheckKind Kind => CheckKind.Runtime;

    public async Task<IReadOnlyList<Finding>> RunAsync(ScanContext context, CancellationToken cancellationToken)
    {
        var findings = new List<Finding>();

        // MAX() + GROUP BY on the language-metadata join collapses multi-language items to one
        // row keyed by ContentItemID — fires one finding per item, not per language. Using MAX
        // on the ModifiedWhen so a still-active translation in any language keeps the item out
        // of the unused bucket (translate-only editors shouldn't accidentally trigger deletes
        // of content that's being maintained in one language but not the others).
        const string sql = @"
            SELECT
                ci.ContentItemID,
                MAX(COALESCE(m.ContentItemLanguageMetadataDisplayName, ci.ContentItemName)) AS DisplayName,
                MAX(c.ClassDisplayName) AS TypeName,
                MAX(m.ContentItemLanguageMetadataModifiedWhen) AS LatestModifiedWhen
            FROM CMS_ContentItem ci
            INNER JOIN CMS_Class c
                ON c.ClassID = ci.ContentItemContentTypeID
            LEFT JOIN CMS_ContentItemReference r
                ON r.ContentItemReferenceTargetItemID = ci.ContentItemID
            LEFT JOIN CMS_ContentItemLanguageMetadata m
                ON m.ContentItemLanguageMetadataContentItemID = ci.ContentItemID
            WHERE ci.ContentItemIsReusable = 1
              AND r.ContentItemReferenceID IS NULL
            GROUP BY ci.ContentItemID
            HAVING MAX(m.ContentItemLanguageMetadataModifiedWhen) < DATEADD(day, -@StaleDays, SYSUTCDATETIME())
               OR MAX(m.ContentItemLanguageMetadataModifiedWhen) IS NULL
            ORDER BY MAX(m.ContentItemLanguageMetadataModifiedWhen) ASC;";

        await using var connection = new SqlConnection(context.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@StaleDays", context.StaleDays);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var itemId = reader.GetInt32(0);
            var displayName = reader.IsDBNull(1) ? "(unnamed)" : reader.GetString(1);
            var typeName = reader.IsDBNull(2) ? "(unknown)" : reader.GetString(2);
            var latestModified = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);

            // Age phrase is either a numeric day-count (modified sometime) or "no modifications
            // recorded" (item was created but never edited — a common signature for test content
            // uploaded during a feature spike and forgotten). Both deserve equal attention.
            var agePhrase = latestModified.HasValue
                ? $"last edited {(int)(DateTime.UtcNow - latestModified.Value).TotalDays} days ago"
                : "no modifications recorded";

            findings.Add(new Finding(
                RuleId, Title, Category, Severity.Info,
                $"'{displayName}' ({typeName}) has no inbound references and {agePhrase} (threshold: {context.StaleDays} days).",
                Location: $"CMS_ContentItem.ContentItemID={itemId}",
                Remediation:
                    "Review in Content Hub. If genuinely unused and you've verified no offline/exported " +
                    "reference is keeping it around, delete or archive it. Bulk cleanup: select all " +
                    "CNT002 findings, sort by age, batch-delete the oldest tier. Re-run Sentinel after " +
                    "deletion to confirm the findings cleared."));
        }

        return findings;
    }
}

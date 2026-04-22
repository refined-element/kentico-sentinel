using System.Text.Json;

using CMS.DataEngine;

using RefinedElement.Kentico.Sentinel.XbyK.InfoModels.SentinelSettingsOverride;

namespace RefinedElement.Kentico.Sentinel.XbyK.SettingsOverride;

/// <summary>
/// Info-provider-backed implementation of the override store. The "single row" invariant is
/// enforced by always targeting <c>OrderBy(ID).TopN(1)</c> on read/write and deleting any
/// stragglers on save. If a concurrent insert slips in (two admins saving at once), the loser's
/// row becomes orphaned and is cleaned up by the next save.
/// </summary>
internal sealed class SentinelSettingsOverrideStore(
    IInfoProvider<SentinelSettingsOverrideInfo> provider) : ISentinelSettingsOverrideStore
{
    // JSON is the serialization format for list-valued columns (ExcludedChecks, EmailRecipients).
    // We use Kentico's Info framework for the table, not a dedicated migration, which means the
    // column type is nvarchar(max) — perfectly fine to hold a JSON array of a few strings.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public SentinelSettingsSnapshot? Get()
    {
        var row = LoadRow();
        return row is null ? null : ToSnapshot(row);
    }

    public void Save(SentinelSettingsSnapshot snapshot, int userId)
    {
        var row = LoadRow() ?? new SentinelSettingsOverrideInfo
        {
            SentinelSettingsOverrideGuid = Guid.NewGuid(),
        };
        row.SentinelSettingsOverrideEnabled = snapshot.Enabled;
        row.SentinelSettingsOverrideExcludedChecks = JsonSerializer.Serialize(snapshot.ExcludedChecks, JsonOpts);
        row.SentinelSettingsOverrideStaleDays = snapshot.StaleDays;
        row.SentinelSettingsOverrideEventLogDays = snapshot.EventLogDays;
        row.SentinelSettingsOverrideEventLogEnabled = snapshot.EventLogEnabled;
        row.SentinelSettingsOverrideEventLogSeverityThreshold = snapshot.EventLogSeverityThreshold;
        row.SentinelSettingsOverrideEventLogMaxEntriesPerScan = snapshot.EventLogMaxEntriesPerScan;
        row.SentinelSettingsOverrideEmailDigestEnabled = snapshot.EmailDigestEnabled;
        row.SentinelSettingsOverrideEmailDigestRecipients = JsonSerializer.Serialize(snapshot.EmailDigestRecipients, JsonOpts);
        row.SentinelSettingsOverrideEmailDigestSeverityThreshold = snapshot.EmailDigestSeverityThreshold;
        row.SentinelSettingsOverrideEmailDigestOnlyWhenThresholdFindings = snapshot.EmailDigestOnlyWhenThresholdFindings;
        row.SentinelSettingsOverrideLastModifiedBy = userId;
        row.SentinelSettingsOverrideLastModifiedAt = DateTime.UtcNow;
        provider.Set(row);

        // Clean up any stragglers from a past concurrent-insert race. Keeps the single-row
        // invariant true even if a bug slipped in somewhere.
        var rows = provider.Get()
            .OrderBy(nameof(SentinelSettingsOverrideInfo.SentinelSettingsOverrideID))
            .ToList();
        foreach (var stale in rows.Skip(1))
        {
            provider.Delete(stale);
        }
    }

    public void Clear()
    {
        var rows = provider.Get().ToList();
        foreach (var row in rows)
        {
            provider.Delete(row);
        }
    }

    private SentinelSettingsOverrideInfo? LoadRow() =>
        provider.Get()
            .OrderBy(nameof(SentinelSettingsOverrideInfo.SentinelSettingsOverrideID))
            .TopN(1)
            .FirstOrDefault();

    internal static SentinelSettingsSnapshot ToSnapshot(SentinelSettingsOverrideInfo row) => new(
        Enabled: row.SentinelSettingsOverrideEnabled,
        ExcludedChecks: ParseStringList(row.SentinelSettingsOverrideExcludedChecks),
        StaleDays: row.SentinelSettingsOverrideStaleDays,
        EventLogDays: row.SentinelSettingsOverrideEventLogDays,
        EventLogEnabled: row.SentinelSettingsOverrideEventLogEnabled,
        EventLogSeverityThreshold: row.SentinelSettingsOverrideEventLogSeverityThreshold,
        EventLogMaxEntriesPerScan: row.SentinelSettingsOverrideEventLogMaxEntriesPerScan,
        EmailDigestEnabled: row.SentinelSettingsOverrideEmailDigestEnabled,
        EmailDigestRecipients: ParseStringList(row.SentinelSettingsOverrideEmailDigestRecipients),
        EmailDigestSeverityThreshold: row.SentinelSettingsOverrideEmailDigestSeverityThreshold,
        EmailDigestOnlyWhenThresholdFindings: row.SentinelSettingsOverrideEmailDigestOnlyWhenThresholdFindings);

    /// <summary>
    /// Parses a JSON-serialized string array, tolerating empty / null / malformed input by
    /// returning an empty list. A corrupted JSON column shouldn't brick the whole options
    /// resolve — degrading to "no override for this field" is safer than throwing.
    /// </summary>
    internal static IReadOnlyList<string> ParseStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }
        try
        {
            return JsonSerializer.Deserialize<string[]>(json, JsonOpts) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}

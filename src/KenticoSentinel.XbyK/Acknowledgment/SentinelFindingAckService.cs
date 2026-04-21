using CMS.DataEngine;

using RefinedElement.Kentico.Sentinel.XbyK.InfoModels.SentinelFindingAck;

namespace RefinedElement.Kentico.Sentinel.XbyK.Acknowledgment;

/// <summary>
/// Info-provider backed implementation. Snoozed rows whose <c>SnoozeUntil</c> has passed are
/// reported as <see cref="AckState.Active"/> on read — the row stays in the DB so re-snooze
/// or conversion to Acknowledged preserves the operator note, but the finding visibly
/// re-enters the active set automatically without a scheduled cleanup job.
/// </summary>
internal sealed class SentinelFindingAckService(
    IInfoProvider<SentinelFindingAckInfo> ackProvider) : ISentinelFindingAckService
{
    private const string StateAcknowledged = "Acknowledged";
    private const string StateSnoozed = "Snoozed";

    public FindingAck? Get(string fingerprint)
    {
        var row = LoadRow(fingerprint);
        return row is null ? null : ToAck(row);
    }

    public IReadOnlyDictionary<string, FindingAck> GetAll(IEnumerable<string> fingerprints)
    {
        var hashes = fingerprints.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (hashes.Length == 0)
        {
            return new Dictionary<string, FindingAck>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = ackProvider.Get()
            .WhereIn(nameof(SentinelFindingAckInfo.SentinelFindingAckFingerprintHash), hashes)
            .ToList();

        // Defensive group-by: duplicate ack rows for the same fingerprint can appear from
        // concurrent upserts, manual DB edits, or historical bugs. Picking the most recently
        // acked row keeps dashboard / scan-detail pages rendering — losing a stale ack is
        // preferable to throwing on every page load the way a naked ToDictionary would.
        return rows
            .GroupBy(r => r.SentinelFindingAckFingerprintHash, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => ToAck(g.OrderByDescending(r => r.SentinelFindingAckAckedAt).First()),
                StringComparer.OrdinalIgnoreCase);
    }

    public void Acknowledge(string fingerprint, int userId, string? note = null) =>
        Upsert(fingerprint, StateAcknowledged, snoozeUntil: null, userId, note);

    public void Snooze(string fingerprint, DateTime until, int userId, string? note = null)
    {
        if (until.Kind == DateTimeKind.Unspecified)
        {
            until = DateTime.SpecifyKind(until, DateTimeKind.Utc);
        }
        Upsert(fingerprint, StateSnoozed, snoozeUntil: until.ToUniversalTime(), userId, note);
    }

    public void Revoke(string fingerprint)
    {
        var row = LoadRow(fingerprint);
        if (row is null)
        {
            return;
        }
        ackProvider.Delete(row);
    }

    public int CountActive(IEnumerable<string> fingerprints)
    {
        var states = GetAll(fingerprints);
        var total = fingerprints.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var suppressed = states.Values.Count(a => a.State != AckState.Active);
        return total - suppressed;
    }

    public int AcknowledgeMany(IEnumerable<string> fingerprints, int userId, string? note = null)
    {
        var written = 0;
        foreach (var fingerprint in Deduplicate(fingerprints))
        {
            Upsert(fingerprint, StateAcknowledged, snoozeUntil: null, userId, note);
            written++;
        }
        return written;
    }

    public int SnoozeMany(IEnumerable<string> fingerprints, DateTime until, int userId, string? note = null)
    {
        if (until.Kind == DateTimeKind.Unspecified)
        {
            until = DateTime.SpecifyKind(until, DateTimeKind.Utc);
        }
        var utc = until.ToUniversalTime();
        var written = 0;
        foreach (var fingerprint in Deduplicate(fingerprints))
        {
            Upsert(fingerprint, StateSnoozed, snoozeUntil: utc, userId, note);
            written++;
        }
        return written;
    }

    public int RevokeMany(IEnumerable<string> fingerprints)
    {
        // Single query to fetch all matching rows, one Delete call each. Kentico's generic
        // IInfoProvider doesn't expose bulk-delete by predicate, so we eat N round-trips —
        // fine at the 10s-to-100s scale of a batch admin action.
        var hashes = Deduplicate(fingerprints).ToArray();
        if (hashes.Length == 0) return 0;

        var rows = ackProvider.Get()
            .WhereIn(nameof(SentinelFindingAckInfo.SentinelFindingAckFingerprintHash), hashes)
            .ToList();
        foreach (var row in rows)
        {
            ackProvider.Delete(row);
        }
        return rows.Count;
    }

    // Strip whitespace-only fingerprints AND duplicates before any bulk writer iterates. Keeps
    // the per-method code focused on the actual operation.
    private static IEnumerable<string> Deduplicate(IEnumerable<string> fingerprints) =>
        fingerprints
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private void Upsert(string fingerprint, string state, DateTime? snoozeUntil, int userId, string? note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);

        var row = LoadRow(fingerprint) ?? new SentinelFindingAckInfo
        {
            SentinelFindingAckGuid = Guid.NewGuid(),
            SentinelFindingAckFingerprintHash = fingerprint,
        };
        row.SentinelFindingAckState = state;
        // DateTime fields on Kentico Info objects are not-null by default; use DateTime.MinValue
        // as a sentinel for "no snooze set" so the DB row accepts the write. Read-side translates
        // MinValue back to null in ToAck below.
        row.SentinelFindingAckSnoozeUntil = snoozeUntil ?? default;
        row.SentinelFindingAckUserID = userId;
        row.SentinelFindingAckAckedAt = DateTime.UtcNow;
        row.SentinelFindingAckNote = note ?? string.Empty;
        ackProvider.Set(row);
    }

    private SentinelFindingAckInfo? LoadRow(string fingerprint) =>
        ackProvider.Get()
            .WhereEquals(nameof(SentinelFindingAckInfo.SentinelFindingAckFingerprintHash), fingerprint)
            .TopN(1)
            .FirstOrDefault();

    private static FindingAck ToAck(SentinelFindingAckInfo row)
    {
        var isSnoozedNow = row.SentinelFindingAckState == StateSnoozed
            && row.SentinelFindingAckSnoozeUntil > DateTime.UtcNow;
        var state = row.SentinelFindingAckState switch
        {
            StateAcknowledged => AckState.Acknowledged,
            StateSnoozed when isSnoozedNow => AckState.Snoozed,
            StateSnoozed => AckState.Active, // snooze expired — natural reversion without cleanup job
            _ => AckState.Active,
        };
        // Only surface SnoozeUntil while the snooze is still active. Once it expires the finding
        // reports Active; leaving a non-null SnoozeUntil on an Active result would confuse the
        // UI ("looks snoozed but behaves active"). Acknowledged is permanent so never carries an
        // expiry either.
        var snoozeUntil = isSnoozedNow && row.SentinelFindingAckSnoozeUntil != default
            ? DateTime.SpecifyKind(row.SentinelFindingAckSnoozeUntil, DateTimeKind.Utc)
            : (DateTime?)null;
        return new FindingAck(
            Fingerprint: row.SentinelFindingAckFingerprintHash,
            State: state,
            SnoozeUntil: snoozeUntil,
            UserId: row.SentinelFindingAckUserID,
            Note: string.IsNullOrWhiteSpace(row.SentinelFindingAckNote) ? null : row.SentinelFindingAckNote,
            AckedAt: DateTime.SpecifyKind(row.SentinelFindingAckAckedAt, DateTimeKind.Utc));
    }
}

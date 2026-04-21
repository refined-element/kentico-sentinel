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

        return rows
            .ToDictionary(
                r => r.SentinelFindingAckFingerprintHash,
                ToAck,
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
        var state = row.SentinelFindingAckState switch
        {
            StateAcknowledged => AckState.Acknowledged,
            StateSnoozed when row.SentinelFindingAckSnoozeUntil > DateTime.UtcNow => AckState.Snoozed,
            StateSnoozed => AckState.Active, // snooze expired — natural reversion without cleanup job
            _ => AckState.Active,
        };
        return new FindingAck(
            Fingerprint: row.SentinelFindingAckFingerprintHash,
            State: state,
            SnoozeUntil: row.SentinelFindingAckSnoozeUntil == default ? null : DateTime.SpecifyKind(row.SentinelFindingAckSnoozeUntil, DateTimeKind.Utc),
            UserId: row.SentinelFindingAckUserID,
            Note: string.IsNullOrWhiteSpace(row.SentinelFindingAckNote) ? null : row.SentinelFindingAckNote,
            AckedAt: DateTime.SpecifyKind(row.SentinelFindingAckAckedAt, DateTimeKind.Utc));
    }
}

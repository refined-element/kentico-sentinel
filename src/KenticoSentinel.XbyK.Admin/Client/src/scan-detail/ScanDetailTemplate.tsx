import React, { useMemo, useState } from 'react';
import { usePageCommand } from '@kentico/xperience-admin-base';
import { Button, ButtonColor, ButtonSize, ButtonType } from '@kentico/xperience-admin-components';

interface ScanOption {
    readonly runId: number;
    readonly label: string;
    readonly findingsCount: number;
}

interface ScanSummary {
    readonly runId: number;
    readonly startedAt: string;
    readonly status: string;
    readonly trigger: string;
    readonly totalFindings: number;
    readonly errorCount: number;
    readonly warningCount: number;
    readonly infoCount: number;
    readonly durationSeconds: number;
    readonly sentinelVersion: string;
}

interface FindingDetail {
    readonly findingId: number;
    readonly fingerprint: string;
    readonly ruleId: string;
    readonly ruleTitle: string;
    readonly category: string;
    readonly severity: string;
    readonly message: string;
    readonly location: string | null;
    readonly remediation: string | null;
    readonly quoteEligible: boolean;
    readonly ackState: string;
    readonly snoozeUntilUtc: string | null;
    readonly ackNote: string | null;
    readonly remediationTitle: string | null;
    readonly remediationSummary: string | null;
    readonly remediationSteps: string | null;
}

interface ScanDetail {
    readonly run: ScanSummary | null;
    readonly findings: ReadonlyArray<FindingDetail>;
}

interface ScanDetailClientProperties {
    readonly availableScans: ReadonlyArray<ScanOption>;
    readonly detail: ScanDetail;
    readonly currentUserId: number;
}

interface ScanDetailResponse {
    readonly detail: ScanDetail;
}

interface AckMutationResponse {
    readonly success: boolean;
    readonly message: string;
    readonly fingerprint: string;
    readonly newState: string;
    readonly snoozeUntilUtc: string | null;
    readonly note: string | null;
}

const COLORS = {
    lime: '#D6F08D',
    limeDark: '#B8D870',
    bg: '#FFFFFF',
    bgMuted: '#F7F7F9',
    border: '#E5E7EB',
    textPrimary: '#1A1A2E',
    textMuted: '#6B7280',
    error: '#DC2626',
    warning: '#D97706',
    info: '#6B7280',
    success: '#10B981',
} as const;

const severityColor = (severity: string) =>
    severity === 'Error' ? COLORS.error : severity === 'Warning' ? COLORS.warning : COLORS.info;

export const ScanDetailTemplate = (initial: ScanDetailClientProperties) => {
    const [detail, setDetail] = useState<ScanDetail>(initial.detail);
    const [selectedRunId, setSelectedRunId] = useState<number | null>(initial.detail.run?.runId ?? null);
    const [ackStates, setAckStates] = useState<Record<string, { ackState: string; snoozeUntilUtc: string | null; ackNote: string | null }>>(() =>
        Object.fromEntries(
            initial.detail.findings.map((f) => [
                f.fingerprint,
                { ackState: f.ackState, snoozeUntilUtc: f.snoozeUntilUtc, ackNote: f.ackNote },
            ]),
        ),
    );
    const [filter, setFilter] = useState<'all' | 'active' | 'acked' | 'snoozed'>('active');
    const [feedback, setFeedback] = useState<string | null>(null);

    // Kentico's Command<T> only exposes `execute` — track in-flight state locally.
    const [isLoading, setIsLoading] = useState(false);
    const { execute: loadScanCmd } = usePageCommand<ScanDetailResponse, { runId: number }>('LoadScanDetail', {
        after: (r) => {
            setIsLoading(false);
            if (r?.detail) {
                setDetail(r.detail);
                setAckStates(
                    Object.fromEntries(
                        r.detail.findings.map((f) => [
                            f.fingerprint,
                            { ackState: f.ackState, snoozeUntilUtc: f.snoozeUntilUtc, ackNote: f.ackNote },
                        ]),
                    ),
                );
                setFeedback(null);
            }
        },
    });

    const { execute: mutateAck } = usePageCommand<AckMutationResponse, { fingerprint: string; action: string; snoozeUntilUtc?: string; note?: string }>('SetAckState', {
        after: (r) => {
            if (r?.success) {
                setAckStates((prev) => ({
                    ...prev,
                    [r.fingerprint]: { ackState: r.newState, snoozeUntilUtc: r.snoozeUntilUtc, ackNote: r.note },
                }));
                setFeedback(`${r.newState === 'Active' ? 'Revoked' : r.newState === 'Acknowledged' ? 'Acknowledged' : 'Snoozed'} finding.`);
                window.setTimeout(() => setFeedback(null), 3000);
            } else if (r) {
                setFeedback(r.message || 'Ack update failed.');
            }
        },
    });

    const loadScan = (args: { runId: number }) => { setIsLoading(true); loadScanCmd(args); };

    const onScanChange = (runId: number) => {
        setSelectedRunId(runId);
        loadScan({ runId });
    };

    const visibleFindings = useMemo(() => {
        return detail.findings.filter((f) => {
            const state = ackStates[f.fingerprint]?.ackState ?? f.ackState;
            if (filter === 'all') return true;
            if (filter === 'active') return state === 'Active';
            if (filter === 'acked') return state === 'Acknowledged';
            if (filter === 'snoozed') return state === 'Snoozed';
            return true;
        });
    }, [detail.findings, ackStates, filter]);

    const byCategory = useMemo(() => {
        const grouped = new Map<string, FindingDetail[]>();
        for (const f of visibleFindings) {
            const arr = grouped.get(f.category) ?? [];
            arr.push(f);
            grouped.set(f.category, arr);
        }
        return Array.from(grouped.entries()).sort((a, b) => a[0].localeCompare(b[0]));
    }, [visibleFindings]);

    if (initial.availableScans.length === 0) {
        return (
            <div style={{ padding: 48, maxWidth: 640, margin: '64px auto', textAlign: 'center', color: COLORS.textMuted }}>
                <h2 style={{ margin: '0 0 12px', color: COLORS.textPrimary }}>No scans recorded</h2>
                <p>Run Sentinel at least once to populate the detail view.</p>
            </div>
        );
    }

    return (
        <div style={{ padding: '24px 32px', maxWidth: 1200, margin: '0 auto' }}>
            <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16, flexWrap: 'wrap', gap: 12 }}>
                <div>
                    <h1 style={{ margin: 0, fontSize: 24, fontWeight: 600, color: COLORS.textPrimary }}>
                        Scan detail
                    </h1>
                    {detail.run && (
                        <p style={{ margin: '4px 0 0', color: COLORS.textMuted, fontSize: 14 }}>
                            #{detail.run.runId} · {new Date(detail.run.startedAt).toLocaleString()} · {detail.run.trigger} · {detail.run.durationSeconds.toFixed(1)}s
                        </p>
                    )}
                </div>
                <div>
                    <label htmlFor="sentinel-scan-picker" style={{ fontSize: 13, color: COLORS.textMuted, marginRight: 8 }}>
                        Scan:
                    </label>
                    <select
                        id="sentinel-scan-picker"
                        value={selectedRunId ?? ''}
                        onChange={(e) => onScanChange(Number(e.target.value))}
                        disabled={isLoading}
                        style={{ padding: '6px 10px', border: `1px solid ${COLORS.border}`, borderRadius: 6, fontSize: 13 }}
                    >
                        {initial.availableScans.map((s) => (
                            <option key={s.runId} value={s.runId}>
                                {s.label}
                            </option>
                        ))}
                    </select>
                </div>
            </header>

            {feedback && (
                <div
                    style={{
                        padding: '8px 14px',
                        marginBottom: 12,
                        background: '#F0FDF4',
                        border: `1px solid ${COLORS.success}`,
                        borderRadius: 6,
                        color: '#14532D',
                        fontSize: 13,
                    }}
                >
                    {feedback}
                </div>
            )}

            {detail.run && <ScanHeader run={detail.run} />}

            <FilterBar
                filter={filter}
                onChange={setFilter}
                counts={{
                    all: detail.findings.length,
                    active: detail.findings.filter((f) => (ackStates[f.fingerprint]?.ackState ?? f.ackState) === 'Active').length,
                    acked: detail.findings.filter((f) => (ackStates[f.fingerprint]?.ackState ?? f.ackState) === 'Acknowledged').length,
                    snoozed: detail.findings.filter((f) => (ackStates[f.fingerprint]?.ackState ?? f.ackState) === 'Snoozed').length,
                }}
            />

            {byCategory.length === 0 ? (
                <div style={{ padding: 48, textAlign: 'center', color: COLORS.textMuted, background: COLORS.bg, border: `1px solid ${COLORS.border}`, borderRadius: 10 }}>
                    <div style={{ fontSize: 32, marginBottom: 8, opacity: 0.5 }}>✓</div>
                    No findings match the current filter.
                </div>
            ) : (
                byCategory.map(([category, items]) => (
                    <CategorySection
                        key={category}
                        category={category}
                        findings={items}
                        ackStates={ackStates}
                        onMutate={(payload) => mutateAck(payload)}
                    />
                ))
            )}
        </div>
    );
};

const ScanHeader = ({ run }: { run: ScanSummary }) => (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12, marginBottom: 16 }}>
        <StatTile label="Total" value={run.totalFindings} color={COLORS.textPrimary} />
        <StatTile label="Errors" value={run.errorCount} color={run.errorCount > 0 ? COLORS.error : COLORS.info} />
        <StatTile label="Warnings" value={run.warningCount} color={run.warningCount > 0 ? COLORS.warning : COLORS.info} />
        <StatTile label="Info" value={run.infoCount} color={COLORS.info} />
    </div>
);

const StatTile = ({ label, value, color }: { label: string; value: number; color: string }) => (
    <div style={{ padding: 14, background: COLORS.bg, border: `1px solid ${COLORS.border}`, borderRadius: 8, borderLeft: `3px solid ${color}` }}>
        <div style={{ fontSize: 11, color: COLORS.textMuted, textTransform: 'uppercase', letterSpacing: 0.4 }}>{label}</div>
        <div style={{ fontSize: 24, fontWeight: 700, color, lineHeight: 1, marginTop: 4 }}>{value.toLocaleString()}</div>
    </div>
);

const FilterBar = ({
    filter,
    onChange,
    counts,
}: {
    filter: 'all' | 'active' | 'acked' | 'snoozed';
    onChange: (f: 'all' | 'active' | 'acked' | 'snoozed') => void;
    counts: { all: number; active: number; acked: number; snoozed: number };
}) => {
    const tab = (id: 'all' | 'active' | 'acked' | 'snoozed', label: string, count: number) => (
        <button
            key={id}
            type="button"
            onClick={() => onChange(id)}
            style={{
                padding: '6px 14px',
                border: `1px solid ${COLORS.border}`,
                borderRadius: 20,
                background: filter === id ? COLORS.lime : COLORS.bg,
                color: COLORS.textPrimary,
                fontSize: 13,
                fontWeight: filter === id ? 600 : 500,
                cursor: 'pointer',
            }}
        >
            {label} <span style={{ color: COLORS.textMuted, marginLeft: 4 }}>({count})</span>
        </button>
    );
    return (
        <div style={{ display: 'flex', gap: 8, marginBottom: 16, flexWrap: 'wrap' }}>
            {tab('active', 'Active', counts.active)}
            {tab('acked', 'Acknowledged', counts.acked)}
            {tab('snoozed', 'Snoozed', counts.snoozed)}
            {tab('all', 'All', counts.all)}
        </div>
    );
};

const CategorySection = ({
    category,
    findings,
    ackStates,
    onMutate,
}: {
    category: string;
    findings: FindingDetail[];
    ackStates: Record<string, { ackState: string; snoozeUntilUtc: string | null; ackNote: string | null }>;
    onMutate: (data: { fingerprint: string; action: string; snoozeUntilUtc?: string; note?: string }) => void;
}) => (
    <section
        style={{
            background: COLORS.bg,
            border: `1px solid ${COLORS.border}`,
            borderRadius: 10,
            overflow: 'hidden',
            marginBottom: 16,
        }}
    >
        <header style={{ padding: '12px 20px', background: COLORS.bgMuted, borderBottom: `1px solid ${COLORS.border}` }}>
            <h3 style={{ margin: 0, fontSize: 13, fontWeight: 700, color: COLORS.textPrimary, textTransform: 'uppercase', letterSpacing: 0.3 }}>
                {category} <span style={{ color: COLORS.textMuted, fontWeight: 500, marginLeft: 8 }}>{findings.length}</span>
            </h3>
        </header>
        <ul style={{ listStyle: 'none', margin: 0, padding: 0 }}>
            {findings.map((f) => {
                const currentAck = ackStates[f.fingerprint]?.ackState ?? f.ackState;
                return (
                    <FindingRow
                        key={f.findingId}
                        finding={f}
                        ackState={currentAck}
                        snoozeUntil={ackStates[f.fingerprint]?.snoozeUntilUtc ?? f.snoozeUntilUtc}
                        onMutate={onMutate}
                    />
                );
            })}
        </ul>
    </section>
);

const FindingRow = ({
    finding,
    ackState,
    snoozeUntil,
    onMutate,
}: {
    finding: FindingDetail;
    ackState: string;
    snoozeUntil: string | null;
    onMutate: (data: { fingerprint: string; action: string; snoozeUntilUtc?: string; note?: string }) => void;
}) => {
    const [expanded, setExpanded] = useState(false);
    const color = severityColor(finding.severity);

    return (
        <li style={{ padding: '14px 20px', borderBottom: `1px solid ${COLORS.border}` }}>
            <div style={{ display: 'grid', gridTemplateColumns: 'auto 1fr auto', gap: 12, alignItems: 'start' }}>
                <SeverityBadge severity={finding.severity} color={color} />
                <div>
                    <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, flexWrap: 'wrap' }}>
                        <code style={{ color: COLORS.textPrimary, fontWeight: 700 }}>{finding.ruleId}</code>
                        <span style={{ color: COLORS.textPrimary, fontWeight: 500 }}>{finding.ruleTitle}</span>
                        <AckBadge state={ackState} until={snoozeUntil} />
                    </div>
                    <div style={{ color: COLORS.textMuted, fontSize: 13, marginTop: 4, lineHeight: 1.5 }}>
                        {finding.message}
                    </div>
                    {finding.location && (
                        <div style={{ color: COLORS.textMuted, fontSize: 12, marginTop: 4, fontFamily: 'monospace' }}>
                            → {finding.location}
                        </div>
                    )}
                    {(finding.remediation || finding.remediationTitle) && (
                        <button
                            type="button"
                            onClick={() => setExpanded(!expanded)}
                            style={{
                                marginTop: 8,
                                padding: '4px 10px',
                                border: `1px solid ${COLORS.border}`,
                                borderRadius: 4,
                                background: 'transparent',
                                color: COLORS.limeDark,
                                fontSize: 12,
                                fontWeight: 600,
                                cursor: 'pointer',
                            }}
                        >
                            {expanded ? 'Hide' : 'Show'} remediation
                        </button>
                    )}
                </div>
                <AckActions fingerprint={finding.fingerprint} ackState={ackState} onMutate={onMutate} />
            </div>
            {expanded && (
                <div
                    style={{
                        marginTop: 12,
                        padding: 14,
                        background: COLORS.bgMuted,
                        borderRadius: 6,
                        fontSize: 13,
                        color: COLORS.textPrimary,
                        lineHeight: 1.6,
                    }}
                >
                    {finding.remediationTitle && (
                        <div style={{ fontWeight: 600, marginBottom: 4 }}>{finding.remediationTitle}</div>
                    )}
                    {finding.remediationSummary && <div style={{ marginBottom: 8, color: COLORS.textMuted }}>{finding.remediationSummary}</div>}
                    {finding.remediationSteps && (
                        <div>
                            <strong style={{ fontSize: 12, color: COLORS.textMuted, textTransform: 'uppercase', letterSpacing: 0.3 }}>Steps</strong>
                            <div style={{ marginTop: 4 }}>{finding.remediationSteps}</div>
                        </div>
                    )}
                    {finding.remediation && (
                        <div style={{ marginTop: 8, paddingTop: 8, borderTop: `1px solid ${COLORS.border}` }}>
                            <strong style={{ fontSize: 12, color: COLORS.textMuted, textTransform: 'uppercase', letterSpacing: 0.3 }}>From scan</strong>
                            <div style={{ marginTop: 4 }}>{finding.remediation}</div>
                        </div>
                    )}
                </div>
            )}
        </li>
    );
};

const SeverityBadge = ({ severity, color }: { severity: string; color: string }) => (
    <span
        style={{
            display: 'inline-block',
            padding: '2px 8px',
            fontSize: 11,
            fontWeight: 700,
            color: '#FFF',
            background: color,
            borderRadius: 4,
            textTransform: 'uppercase',
            letterSpacing: 0.3,
            minWidth: 60,
            textAlign: 'center',
        }}
    >
        {severity}
    </span>
);

const AckBadge = ({ state, until }: { state: string; until: string | null }) => {
    if (state === 'Active') return null;
    const isSnooze = state === 'Snoozed';
    return (
        <span
            style={{
                fontSize: 11,
                padding: '2px 8px',
                borderRadius: 10,
                background: isSnooze ? '#FEF3C7' : '#E0E7FF',
                color: isSnooze ? '#78350F' : '#3730A3',
                fontWeight: 600,
            }}
        >
            {isSnooze && until ? `Snoozed until ${new Date(until).toLocaleDateString()}` : 'Acknowledged'}
        </span>
    );
};

const AckActions = ({
    fingerprint,
    ackState,
    onMutate,
}: {
    fingerprint: string;
    ackState: string;
    onMutate: (data: { fingerprint: string; action: string; snoozeUntilUtc?: string; note?: string }) => void;
}) => {
    const [snoozeOpen, setSnoozeOpen] = useState(false);
    const [snoozeDays, setSnoozeDays] = useState(7);
    const [note, setNote] = useState('');

    if (ackState !== 'Active') {
        return (
            <Button
                type={ButtonType.Button}
                label="Revoke"
                size={ButtonSize.S}
                color={ButtonColor.Secondary}
                onClick={() => onMutate({ fingerprint, action: 'revoke' })}
            />
        );
    }

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6, alignItems: 'flex-end' }}>
            <div style={{ display: 'flex', gap: 6 }}>
                <Button
                    type={ButtonType.Button}
                    label="Acknowledge"
                    size={ButtonSize.S}
                    color={ButtonColor.Secondary}
                    onClick={() => onMutate({ fingerprint, action: 'acknowledge', note: note.trim() || undefined })}
                />
                <Button
                    type={ButtonType.Button}
                    label={snoozeOpen ? 'Cancel' : 'Snooze'}
                    size={ButtonSize.S}
                    color={ButtonColor.Secondary}
                    onClick={() => setSnoozeOpen(!snoozeOpen)}
                />
            </div>
            {snoozeOpen && (
                <div
                    style={{
                        padding: 10,
                        background: COLORS.bgMuted,
                        border: `1px solid ${COLORS.border}`,
                        borderRadius: 6,
                        display: 'flex',
                        flexDirection: 'column',
                        gap: 6,
                        minWidth: 240,
                    }}
                >
                    <label style={{ fontSize: 12, color: COLORS.textMuted }}>
                        Snooze for
                        <select
                            value={snoozeDays}
                            onChange={(e) => setSnoozeDays(Number(e.target.value))}
                            style={{ marginLeft: 6, padding: '2px 6px', border: `1px solid ${COLORS.border}`, borderRadius: 4, fontSize: 12 }}
                        >
                            <option value={1}>1 day</option>
                            <option value={7}>7 days</option>
                            <option value={30}>30 days</option>
                            <option value={90}>90 days</option>
                        </select>
                    </label>
                    <input
                        type="text"
                        placeholder="Optional note"
                        value={note}
                        onChange={(e) => setNote(e.target.value)}
                        style={{ padding: '4px 8px', border: `1px solid ${COLORS.border}`, borderRadius: 4, fontSize: 12 }}
                    />
                    <Button
                        type={ButtonType.Button}
                        label={`Snooze ${snoozeDays}d`}
                        size={ButtonSize.S}
                        color={ButtonColor.Primary}
                        onClick={() => {
                            const until = new Date();
                            until.setUTCDate(until.getUTCDate() + snoozeDays);
                            onMutate({
                                fingerprint,
                                action: 'snooze',
                                snoozeUntilUtc: until.toISOString(),
                                note: note.trim() || undefined,
                            });
                            setSnoozeOpen(false);
                            setNote('');
                        }}
                    />
                </div>
            )}
        </div>
    );
};

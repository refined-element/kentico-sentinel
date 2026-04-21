import React, { useEffect, useState } from 'react';
import { usePageCommand } from '@kentico/xperience-admin-base';
import { Button, ButtonColor, ButtonSize } from '@kentico/xperience-admin-components';

interface ScanOption {
    readonly runId: number;
    readonly label: string;
    readonly findingsCount: number;
}

interface DiffFinding {
    readonly fingerprint: string;
    readonly ruleId: string;
    readonly ruleTitle: string;
    readonly category: string;
    readonly severity: string;
    readonly message: string;
    readonly location: string | null;
}

interface DiffResponse {
    readonly resolved: ReadonlyArray<DiffFinding>;
    readonly introduced: ReadonlyArray<DiffFinding>;
    readonly stillOpen: ReadonlyArray<DiffFinding>;
    readonly message: string;
}

interface DiffClientProperties {
    readonly availableScans: ReadonlyArray<ScanOption>;
    readonly defaultBeforeRunId: number;
    readonly defaultAfterRunId: number;
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
    introduced: '#B45309',
    resolved: '#15803D',
} as const;

export const DiffTemplate = (initial: DiffClientProperties) => {
    const [beforeId, setBeforeId] = useState<number>(initial.defaultBeforeRunId);
    const [afterId, setAfterId] = useState<number>(initial.defaultAfterRunId);
    const [diff, setDiff] = useState<DiffResponse | null>(null);

    const { execute, isPending } = usePageCommand<DiffResponse, { beforeRunId: number; afterRunId: number }>('ComputeDiff', {
        after: (r) => {
            if (r) setDiff(r);
        },
    });

    // Auto-compute on first mount so the user sees a diff without having to click — uses the
    // server's default selections (latest vs. previous). They can change the pickers after.
    useEffect(() => {
        if (initial.defaultBeforeRunId > 0 && initial.defaultAfterRunId > 0) {
            execute({ beforeRunId: initial.defaultBeforeRunId, afterRunId: initial.defaultAfterRunId });
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const canCompute = beforeId > 0 && afterId > 0 && !isPending;

    if (initial.availableScans.length < 2) {
        return (
            <div style={{ padding: 48, maxWidth: 640, margin: '64px auto', textAlign: 'center', color: COLORS.textMuted }}>
                <h2 style={{ margin: '0 0 12px', color: COLORS.textPrimary }}>Need at least two scans to compare</h2>
                <p>Run a second Sentinel scan — then come back here to diff.</p>
            </div>
        );
    }

    return (
        <div style={{ padding: '24px 32px', maxWidth: 1200, margin: '0 auto' }}>
            <header style={{ marginBottom: 16 }}>
                <h1 style={{ margin: 0, fontSize: 24, fontWeight: 600, color: COLORS.textPrimary }}>
                    Compare scans
                </h1>
                <p style={{ margin: '6px 0 0', color: COLORS.textMuted, fontSize: 14, lineHeight: 1.6 }}>
                    Pick two scan runs to compare. Findings are matched by fingerprint, so a finding with a minor
                    wording drift between scans (a digit changed, a location renamed) still lines up — a "Resolved"
                    finding really is resolved.
                </p>
            </header>

            <div
                style={{
                    padding: 16,
                    background: COLORS.bg,
                    border: `1px solid ${COLORS.border}`,
                    borderRadius: 10,
                    display: 'grid',
                    gridTemplateColumns: '1fr auto 1fr auto',
                    alignItems: 'end',
                    gap: 16,
                    marginBottom: 20,
                }}
            >
                <div>
                    <label htmlFor="sentinel-diff-before" style={{ display: 'block', fontSize: 13, color: COLORS.textMuted, marginBottom: 6 }}>
                        Before
                    </label>
                    <select
                        id="sentinel-diff-before"
                        value={beforeId}
                        onChange={(e) => setBeforeId(Number(e.target.value))}
                        style={{ width: '100%', padding: '8px 10px', border: `1px solid ${COLORS.border}`, borderRadius: 6, fontSize: 14 }}
                    >
                        {initial.availableScans.map((s) => (
                            <option key={s.runId} value={s.runId}>
                                {s.label}
                            </option>
                        ))}
                    </select>
                </div>
                <div style={{ color: COLORS.textMuted, fontSize: 20, paddingBottom: 6 }}>→</div>
                <div>
                    <label htmlFor="sentinel-diff-after" style={{ display: 'block', fontSize: 13, color: COLORS.textMuted, marginBottom: 6 }}>
                        After
                    </label>
                    <select
                        id="sentinel-diff-after"
                        value={afterId}
                        onChange={(e) => setAfterId(Number(e.target.value))}
                        style={{ width: '100%', padding: '8px 10px', border: `1px solid ${COLORS.border}`, borderRadius: 6, fontSize: 14 }}
                    >
                        {initial.availableScans.map((s) => (
                            <option key={s.runId} value={s.runId}>
                                {s.label}
                            </option>
                        ))}
                    </select>
                </div>
                <Button
                    type="button"
                    label={isPending ? 'Comparing…' : 'Compare'}
                    onClick={() => execute({ beforeRunId: beforeId, afterRunId: afterId })}
                    size={ButtonSize.M}
                    color={ButtonColor.Primary}
                    disabled={!canCompute}
                />
            </div>

            {diff && (
                <>
                    <SummaryRow diff={diff} />
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 16, marginTop: 16 }}>
                        <DiffColumn title="Introduced" tone={COLORS.introduced} icon="+" findings={diff.introduced} emptyText="No new findings 🎉" />
                        <DiffColumn title="Resolved" tone={COLORS.resolved} icon="✓" findings={diff.resolved} emptyText="No findings resolved" />
                        <DiffColumn title="Still open" tone={COLORS.textMuted} icon="=" findings={diff.stillOpen} emptyText="No carry-over findings" />
                    </div>
                </>
            )}
        </div>
    );
};

const SummaryRow = ({ diff }: { diff: DiffResponse }) => {
    const delta = diff.introduced.length - diff.resolved.length;
    const tone = delta === 0 ? COLORS.textMuted : delta > 0 ? COLORS.introduced : COLORS.resolved;
    const label = delta === 0 ? 'No net change' : delta > 0 ? `+${delta} net new` : `${delta} net resolved`;
    return (
        <div
            style={{
                padding: 14,
                background: COLORS.bg,
                border: `1px solid ${COLORS.border}`,
                borderRadius: 10,
                display: 'flex',
                gap: 24,
                alignItems: 'center',
            }}
        >
            <div style={{ fontSize: 24, fontWeight: 700, color: tone }}>{label}</div>
            <div style={{ display: 'flex', gap: 16, fontSize: 13, color: COLORS.textMuted }}>
                <span>
                    <strong style={{ color: COLORS.introduced }}>{diff.introduced.length}</strong> introduced
                </span>
                <span>
                    <strong style={{ color: COLORS.resolved }}>{diff.resolved.length}</strong> resolved
                </span>
                <span>
                    <strong style={{ color: COLORS.textPrimary }}>{diff.stillOpen.length}</strong> still open
                </span>
            </div>
        </div>
    );
};

const DiffColumn = ({
    title,
    tone,
    icon,
    findings,
    emptyText,
}: {
    title: string;
    tone: string;
    icon: string;
    findings: ReadonlyArray<DiffFinding>;
    emptyText: string;
}) => (
    <section
        style={{
            background: COLORS.bg,
            border: `1px solid ${COLORS.border}`,
            borderRadius: 10,
            overflow: 'hidden',
        }}
    >
        <header
            style={{
                padding: '10px 14px',
                background: COLORS.bgMuted,
                borderBottom: `1px solid ${COLORS.border}`,
                borderLeft: `3px solid ${tone}`,
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
            }}
        >
            <h3 style={{ margin: 0, fontSize: 13, fontWeight: 700, color: tone, textTransform: 'uppercase', letterSpacing: 0.3 }}>
                <span style={{ marginRight: 6 }}>{icon}</span>{title}
            </h3>
            <span style={{ fontSize: 12, color: COLORS.textMuted }}>{findings.length}</span>
        </header>
        {findings.length === 0 ? (
            <div style={{ padding: 18, fontSize: 13, color: COLORS.textMuted, textAlign: 'center' }}>{emptyText}</div>
        ) : (
            <ul style={{ listStyle: 'none', margin: 0, padding: 0, maxHeight: 500, overflowY: 'auto' }}>
                {findings.map((f) => (
                    <li key={f.fingerprint} style={{ padding: '10px 14px', borderBottom: `1px solid ${COLORS.border}`, fontSize: 13 }}>
                        <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 2 }}>
                            <SeverityChip severity={f.severity} />
                            <code style={{ color: COLORS.textPrimary, fontWeight: 700, fontSize: 12 }}>{f.ruleId}</code>
                        </div>
                        <div style={{ color: COLORS.textMuted, fontSize: 12, lineHeight: 1.5 }}>{f.message}</div>
                        {f.location && (
                            <div style={{ color: COLORS.textMuted, fontSize: 11, marginTop: 2, fontFamily: 'monospace' }}>
                                → {f.location}
                            </div>
                        )}
                    </li>
                ))}
            </ul>
        )}
    </section>
);

const SeverityChip = ({ severity }: { severity: string }) => {
    const color = severity === 'Error' ? COLORS.error : severity === 'Warning' ? COLORS.warning : COLORS.info;
    return (
        <span
            style={{
                fontSize: 10,
                fontWeight: 700,
                padding: '1px 6px',
                background: color,
                color: '#FFF',
                borderRadius: 3,
                textTransform: 'uppercase',
                letterSpacing: 0.3,
            }}
        >
            {severity[0]}
        </span>
    );
};

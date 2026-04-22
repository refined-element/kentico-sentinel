import React from 'react';

import { THEME_COLORS as COLORS } from '../theme';

interface SettingsClientProperties {
    readonly enabled: boolean;
    readonly excludedChecks: ReadonlyArray<string>;
    readonly runtimeConnectionString: string;
    readonly staleDays: number;
    readonly eventLogDays: number;
    readonly emailDigestEnabled: boolean;
    readonly emailDigestRecipients: ReadonlyArray<string>;
    readonly emailDigestSeverityThreshold: string;
    readonly emailDigestOnlyWhenThresholdFindings: boolean;
    readonly eventLogEnabled: boolean;
    readonly eventLogSeverityThreshold: string;
    readonly eventLogMaxEntriesPerScan: number;
    readonly contactEndpoint: string;
    readonly contactIncludeContextByDefault: boolean;
    readonly scheduledTasksUrl: string;
    readonly scheduleState: 'enabled' | 'disabled' | 'missing' | string;
    readonly scheduleIntervalRaw: string;
    readonly scheduleIntervalHint: string;
    readonly scheduleLastRunUtc: string | null;
    readonly scheduleNextRunUtc: string | null;
}

export const SettingsTemplate = (props: SettingsClientProperties) => (
    <div style={{ padding: '24px 32px', maxWidth: 900, margin: '0 auto' }}>
        <header style={{ marginBottom: 24 }}>
            <h1 style={{ margin: 0, fontSize: 24, fontWeight: 600, color: COLORS.textPrimary }}>
                Settings
            </h1>
            <p style={{ margin: '6px 0 0', color: COLORS.textMuted, fontSize: 14, lineHeight: 1.6 }}>
                Effective Sentinel configuration, read from <code>appsettings.json</code> and any override layers
                (environment variables, Azure App Service config, delegate overloads). Edit the source and redeploy
                to change any value — the page refreshes on app restart.
            </p>
        </header>

        <MasterSwitch enabled={props.enabled} />

        <Section
            title="Scan behavior"
            rows={[
                ['Master switch', <Badge tone={props.enabled ? 'success' : 'error'} text={props.enabled ? 'Enabled' : 'Disabled'} />, 'Sentinel:Enabled'],
                ['Excluded checks', props.excludedChecks.length === 0 ? <em style={{ color: COLORS.textMuted }}>none</em> : <code>{props.excludedChecks.join(', ')}</code>, 'Sentinel:Checks:Excluded'],
                ['Runtime connection string', <code>{props.runtimeConnectionString}</code>, 'Sentinel:RuntimeChecks:ConnectionString'],
                ['Stale content threshold', <span>{props.staleDays} days</span>, 'Sentinel:RuntimeChecks:StaleDays'],
                ['Event log recency window', <span>{props.eventLogDays} days</span>, 'Sentinel:RuntimeChecks:EventLogDays'],
            ]}
        />

        <ScheduleSection
            state={props.scheduleState}
            intervalRaw={props.scheduleIntervalRaw}
            intervalHint={props.scheduleIntervalHint}
            lastRunUtc={props.scheduleLastRunUtc}
            nextRunUtc={props.scheduleNextRunUtc}
            scheduledTasksUrl={props.scheduledTasksUrl}
        />

        <Section
            title="Event log mirror"
            rows={[
                ['Enabled', <Badge tone={props.eventLogEnabled ? 'success' : 'muted'} text={props.eventLogEnabled ? 'Yes' : 'No'} />, 'Sentinel:EventLogIntegration:Enabled'],
                ['Severity threshold', <code>{props.eventLogSeverityThreshold}</code>, 'Sentinel:EventLogIntegration:SeverityThreshold'],
                ['Max entries per scan', <span>{props.eventLogMaxEntriesPerScan}</span>, 'Sentinel:EventLogIntegration:MaxEntriesPerScan'],
            ]}
        />

        <Section
            title="Email digest"
            rows={[
                ['Enabled', <Badge tone={props.emailDigestEnabled && props.emailDigestRecipients.length > 0 ? 'success' : 'muted'} text={props.emailDigestEnabled && props.emailDigestRecipients.length > 0 ? 'Active' : props.emailDigestEnabled ? 'Enabled but no recipients' : 'Disabled'} />, 'Sentinel:EmailDigest:Enabled'],
                ['Recipients', props.emailDigestRecipients.length === 0 ? <em style={{ color: COLORS.textMuted }}>none — digest never sends</em> : <code>{props.emailDigestRecipients.join(', ')}</code>, 'Sentinel:EmailDigest:Recipients'],
                ['Severity threshold', <code>{props.emailDigestSeverityThreshold}</code>, 'Sentinel:EmailDigest:SeverityThreshold'],
                ['Only when findings ≥ threshold', <Badge tone={props.emailDigestOnlyWhenThresholdFindings ? 'success' : 'muted'} text={props.emailDigestOnlyWhenThresholdFindings ? 'Yes' : 'No'} />, 'Sentinel:EmailDigest:OnlyWhenThresholdFindings'],
            ]}
        />

        <Section
            title="Refined Element contact"
            rows={[
                ['Quote endpoint', <code>{props.contactEndpoint}</code>, 'Sentinel:Contact:Endpoint'],
                ['Include context by default', <Badge tone={props.contactIncludeContextByDefault ? 'success' : 'muted'} text={props.contactIncludeContextByDefault ? 'Yes' : 'No'} />, 'Sentinel:Contact:IncludeContextByDefault'],
            ]}
        />

        <div
            style={{
                marginTop: 24,
                padding: 16,
                background: COLORS.bgMuted,
                borderRadius: 8,
                fontSize: 14,
                color: COLORS.textMuted,
                lineHeight: 1.6,
            }}
        >
            <div style={{ fontWeight: 600, color: COLORS.textPrimary, marginBottom: 6 }}>Where do I change these?</div>
            <ul style={{ margin: '0 0 0 18px', padding: 0 }}>
                <li>
                    <strong>Local dev:</strong> edit <code>appsettings.json</code> or <code>appsettings.Development.json</code>, restart the app.
                </li>
                <li>
                    <strong>Production:</strong> Azure Portal → App Service → Configuration → Application settings.
                    Keys use <code>Sentinel__EventLogIntegration__SeverityThreshold</code> form (double underscore = colon).
                </li>
                <li>
                    <strong>Scan cadence / enable:</strong>{' '}
                    <a href={props.scheduledTasksUrl} style={{ color: COLORS.limeText, fontWeight: 600 }}>
                        Scheduled tasks
                    </a>
                    {' — not an '}
                    <code>appsettings</code>
                    {' value. Edit the '}
                    <code>RefinedElement.SentinelScan</code>
                    {' row.'}
                </li>
            </ul>
        </div>
    </div>
);

const MasterSwitch = ({ enabled }: { enabled: boolean }) => (
    <div
        style={{
            padding: 16,
            marginBottom: 20,
            background: enabled ? '#F0FDF4' : '#FEF2F2',
            border: `1px solid ${enabled ? COLORS.success : COLORS.error}`,
            borderRadius: 10,
            display: 'flex',
            alignItems: 'center',
            gap: 12,
        }}
    >
        <div
            style={{
                width: 32,
                height: 32,
                borderRadius: 16,
                background: enabled ? COLORS.success : COLORS.error,
                color: '#FFF',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontSize: 16,
                fontWeight: 700,
                flexShrink: 0,
            }}
        >
            {enabled ? '✓' : '✕'}
        </div>
        <div style={{ color: enabled ? '#14532D' : '#7F1D1D', fontSize: 14 }}>
            <div style={{ fontWeight: 600 }}>Sentinel is {enabled ? 'enabled' : 'disabled'}</div>
            <div style={{ fontSize: 14 }}>
                {enabled
                    ? 'Scans run on the scheduled cadence; admin can trigger manual scans from the Dashboard.'
                    : 'Scheduled scans are skipped. Flip Sentinel:Enabled to true to re-activate.'}
            </div>
        </div>
    </div>
);

const Section = ({ title, rows }: { title: string; rows: ReadonlyArray<[string, React.ReactNode, string]> }) => (
    <section
        style={{
            background: COLORS.bg,
            border: `1px solid ${COLORS.border}`,
            borderRadius: 10,
            overflow: 'hidden',
            marginBottom: 20,
        }}
    >
        <header
            style={{
                padding: '12px 20px',
                borderBottom: `1px solid ${COLORS.border}`,
                background: COLORS.bgMuted,
            }}
        >
            <h3 style={{ margin: 0, fontSize: 14, fontWeight: 700, color: COLORS.textPrimary, textTransform: 'uppercase', letterSpacing: 0.4 }}>
                {title}
            </h3>
        </header>
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <tbody>
                {rows.map(([label, value, configKey], i) => (
                    <tr key={configKey} style={{ borderBottom: i < rows.length - 1 ? `1px solid ${COLORS.border}` : 'none' }}>
                        <td style={{ padding: '12px 20px', width: 260, color: COLORS.textPrimary, fontSize: 14, fontWeight: 500, verticalAlign: 'top' }}>
                            {label}
                            <div style={{ fontSize: 11, color: COLORS.textMuted, fontFamily: 'monospace', marginTop: 4 }}>{configKey}</div>
                        </td>
                        <td style={{ padding: '12px 20px', color: COLORS.textPrimary, fontSize: 14 }}>{value}</td>
                    </tr>
                ))}
            </tbody>
        </table>
    </section>
);

const ScheduleSection = ({
    state,
    intervalRaw,
    intervalHint,
    lastRunUtc,
    nextRunUtc,
    scheduledTasksUrl,
}: {
    state: string;
    intervalRaw: string;
    intervalHint: string;
    lastRunUtc: string | null;
    nextRunUtc: string | null;
    scheduledTasksUrl: string;
}) => {
    // Cadence lives in Kentico's scheduled-task row, not in appsettings, so this section is
    // read-only-with-deep-link rather than a config key path. Admin sees the effective cadence
    // here, then clicks through to change it in the Scheduled Tasks app.
    const stateBadge = state === 'enabled'
        ? <Badge tone="success" text="Running on schedule" />
        : state === 'disabled'
            ? <Badge tone="muted" text="Disabled" />
            : <Badge tone="error" text="Task row missing" />;
    const lastRunText = lastRunUtc ? new Date(lastRunUtc).toLocaleString() : '—';
    const nextRunText = nextRunUtc ? new Date(nextRunUtc).toLocaleString() : '—';
    return (
        <section
            style={{
                background: COLORS.bg,
                border: `1px solid ${COLORS.border}`,
                borderRadius: 10,
                overflow: 'hidden',
                marginBottom: 16,
            }}
        >
            <header style={{ padding: '10px 20px', background: COLORS.bgMuted, borderBottom: `1px solid ${COLORS.border}`, display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
                <h2 style={{ margin: 0, fontSize: 15, fontWeight: 700, color: COLORS.textPrimary }}>
                    Scan cadence
                </h2>
                <a
                    href={scheduledTasksUrl}
                    style={{ fontSize: 13, color: COLORS.limeText, fontWeight: 600, textDecoration: 'none' }}
                >
                    Edit in Scheduled tasks →
                </a>
            </header>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <tbody>
                    <tr style={{ borderBottom: `1px solid ${COLORS.border}` }}>
                        <td style={{ padding: '10px 20px', color: COLORS.textMuted, width: '35%', fontSize: 14 }}>Status</td>
                        <td style={{ padding: '10px 20px', fontSize: 14 }}>{stateBadge}</td>
                    </tr>
                    <tr style={{ borderBottom: `1px solid ${COLORS.border}` }}>
                        <td style={{ padding: '10px 20px', color: COLORS.textMuted, fontSize: 14 }}>Cadence</td>
                        <td style={{ padding: '10px 20px', fontSize: 14 }}>
                            {intervalHint || <em style={{ color: COLORS.textMuted }}>unknown</em>}
                            {intervalRaw && (
                                <span style={{ color: COLORS.textMuted, fontSize: 12, marginLeft: 8 }}>
                                    (raw: <code>{intervalRaw}</code>)
                                </span>
                            )}
                        </td>
                    </tr>
                    <tr style={{ borderBottom: `1px solid ${COLORS.border}` }}>
                        <td style={{ padding: '10px 20px', color: COLORS.textMuted, fontSize: 14 }}>Last run</td>
                        <td style={{ padding: '10px 20px', fontSize: 14 }}>{lastRunText}</td>
                    </tr>
                    <tr>
                        <td style={{ padding: '10px 20px', color: COLORS.textMuted, fontSize: 14 }}>Next run</td>
                        <td style={{ padding: '10px 20px', fontSize: 14 }}>{nextRunText}</td>
                    </tr>
                </tbody>
            </table>
        </section>
    );
};

const Badge = ({ tone, text }: { tone: 'success' | 'error' | 'muted'; text: string }) => {
    const bg = tone === 'success' ? '#ECFDF5' : tone === 'error' ? '#FEF2F2' : '#F3F4F6';
    const fg = tone === 'success' ? '#14532D' : tone === 'error' ? '#7F1D1D' : COLORS.textMuted;
    const border = tone === 'success' ? COLORS.success : tone === 'error' ? COLORS.error : COLORS.border;
    return (
        <span
            style={{
                display: 'inline-block',
                padding: '3px 10px',
                fontSize: 12,
                fontWeight: 600,
                background: bg,
                color: fg,
                border: `1px solid ${border}`,
                borderRadius: 10,
            }}
        >
            {text}
        </span>
    );
};

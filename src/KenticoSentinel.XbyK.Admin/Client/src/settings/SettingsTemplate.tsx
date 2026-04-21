import React from 'react';

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
    success: '#10B981',
} as const;

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
                fontSize: 13,
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
                    <a href={props.scheduledTasksUrl} style={{ color: COLORS.limeDark, fontWeight: 600 }}>
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
            <div style={{ fontSize: 13 }}>
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
            <h3 style={{ margin: 0, fontSize: 13, fontWeight: 700, color: COLORS.textPrimary, textTransform: 'uppercase', letterSpacing: 0.4 }}>
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

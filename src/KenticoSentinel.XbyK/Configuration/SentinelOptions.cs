namespace RefinedElement.Kentico.Sentinel.XbyK.Configuration;

/// <summary>
/// Strongly-typed options bound from <c>appsettings.json</c> section <c>Sentinel</c>.
/// Every value has a sensible default so the minimal configuration — just calling
/// <c>AddKenticoSentinel()</c> — produces a working install.
/// </summary>
public sealed class SentinelOptions
{
    public const string SectionName = "Sentinel";

    public bool Enabled { get; set; } = true;
    public ScheduleOptions Schedule { get; set; } = new();
    public ChecksOptions Checks { get; set; } = new();
    public RuntimeCheckOptions RuntimeChecks { get; set; } = new();
    public EmailDigestOptions EmailDigest { get; set; } = new();
    public EventLogOptions EventLogIntegration { get; set; } = new();
    public ContactOptions ContactRefinedElement { get; set; } = new();

    public sealed class ScheduleOptions
    {
        /// <summary>
        /// 6-field cron (with seconds). Default "0 0 9 * * MON" = 9am every Monday UTC.
        /// Parsed by Cronos; invalid expressions fail fast at startup.
        /// </summary>
        public string CronExpression { get; set; } = "0 0 9 * * MON";
        public bool RunOnStartup { get; set; } = true;
        public int InitialDelayMinutes { get; set; } = 5;
    }

    public sealed class ChecksOptions
    {
        /// <summary>Rule IDs to skip, e.g. ["DEP001"].</summary>
        public List<string> Excluded { get; set; } = [];
    }

    public sealed class RuntimeCheckOptions
    {
        /// <summary>Connection string for runtime checks. Empty = fall back to Kentico's CMSConnectionString.</summary>
        public string ConnectionString { get; set; } = string.Empty;
        public int StaleDays { get; set; } = 180;
        public int EventLogDays { get; set; } = 30;
    }

    public sealed class EmailDigestOptions
    {
        public bool Enabled { get; set; } = true;
        public List<string> Recipients { get; set; } = [];

        /// <summary>Skip digest when the scan produced nothing new vs. the last one.</summary>
        public bool OnlyWhenNewFindings { get; set; } = true;

        /// <summary>"Error" | "Warning" | "Info" — findings below this don't trigger the digest.</summary>
        public string SeverityThreshold { get; set; } = "Warning";
    }

    public sealed class EventLogOptions
    {
        public bool Enabled { get; set; } = true;
        public string SeverityThreshold { get; set; } = "Warning";
    }

    public sealed class ContactOptions
    {
        public string Endpoint { get; set; } = "https://kentico-developer.com/api/scanner/submit";
    }
}

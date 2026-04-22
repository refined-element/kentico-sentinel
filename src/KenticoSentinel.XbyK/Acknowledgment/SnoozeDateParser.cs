using System.Globalization;

namespace RefinedElement.Kentico.Sentinel.XbyK.Acknowledgment;

/// <summary>
/// Parses the ISO-8601 snooze-until string that the admin UI sends with every Snooze action.
/// Extracted from the page command so the (surprisingly nuanced) date-parsing rules are unit
/// testable without booting Kentico infrastructure.
///
/// <para>
/// The original v0.3.0 / v0.3.1-alpha implementation used <c>DateTime.TryParseExact("O", ...)</c>
/// which silently rejected the JS <c>Date.toISOString()</c> output: the "O" format specifier
/// requires exactly 7 fractional-second digits, but JavaScript emits 3. Every snooze from the
/// admin UI bounced with "Invalid snooze date" and the write never happened. <c>TryParse</c>
/// with <see cref="DateTimeStyles.RoundtripKind"/> accepts any valid ISO-8601 variant while
/// still requiring an explicit UTC designator or offset — a server culture running dd/MM/yyyy
/// can't swap the month and day since we also pin <see cref="CultureInfo.InvariantCulture"/>.
/// </para>
/// </summary>
public static class SnoozeDateParser
{
    /// <summary>
    /// Attempts to parse an ISO-8601 snooze-until timestamp and validate that it's at least
    /// one minute in the future. One-minute grace covers client/server clock drift — a snooze
    /// expiring "right now" would read back as Active on the very next refresh, giving the
    /// admin misleading "Snoozed" feedback for a finding that's already un-snoozed.
    /// </summary>
    /// <param name="input">Raw value from the page command — typically JS Date.toISOString().</param>
    /// <param name="nowUtc">Injected "now" so tests can exercise the one-minute-grace boundary.</param>
    /// <param name="utcSnoozeUntil">Parsed UTC timestamp on success.</param>
    /// <param name="errorMessage">Human-readable reason for failure on false return.</param>
    public static bool TryParse(string? input, DateTime nowUtc, out DateTime utcSnoozeUntil, out string errorMessage)
    {
        if (!DateTime.TryParse(
                input,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed)
            // Reject inputs that parsed successfully but lack a UTC designator or offset.
            // With RoundtripKind, "2026-04-29T12:00:00" parses with Kind=Unspecified; silently
            // treating that as UTC would create an off-by-hours snooze for any admin whose
            // browser sent a local-time string, so we fail closed instead.
            || parsed.Kind == DateTimeKind.Unspecified)
        {
            utcSnoozeUntil = default;
            errorMessage = "Invalid snooze date — expected ISO-8601 format with UTC designator or offset.";
            return false;
        }
        // Normalize to UTC regardless of whether the input came in as "...Z" (Kind=Utc) or
        // "...-04:00" (Kind=Local) — both are valid explicit-timezone inputs at this point.
        var utc = parsed.ToUniversalTime();
        if (utc <= nowUtc.AddMinutes(1))
        {
            utcSnoozeUntil = default;
            errorMessage = "Snooze date must be at least one minute in the future.";
            return false;
        }
        utcSnoozeUntil = utc;
        errorMessage = string.Empty;
        return true;
    }
}

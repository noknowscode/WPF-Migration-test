using System.Collections.Generic;

namespace EventLookUno.Models;

/// <summary>
/// Typed settings record — replaces the WPF source's <c>Properties.Settings</c> stack.
/// Persisted via <c>IWritableOptions&lt;EventLookSettings&gt;</c> (per-platform storage:
/// appsettings.json where writable, <c>ApplicationData.LocalSettings</c> on WASM).
/// </summary>
public record EventLookSettings
{
    /// <summary>Event-log channel names to show in the startup dropdown.</summary>
    public IReadOnlyList<string> StartupLogNames { get; init; } = new[] { "System", "Application" };

    /// <summary>Days-from-now for the startup time-range selection. 0 = "All time".</summary>
    public int StartupRangeDays { get; init; }

    /// <summary>Whether to render milliseconds in the Time column.</summary>
    public bool ShowsMillisec { get; init; }

    /// <summary>Whether the Record ID column is visible.</summary>
    public bool ShowsRecordId { get; init; }

    /// <summary>Last window width (two-way round-tripped on resize).</summary>
    public int WinWidth { get; init; } = 1100;

    /// <summary>Last window height (two-way round-tripped on resize).</summary>
    public int WinHeight { get; init; } = 640;
}

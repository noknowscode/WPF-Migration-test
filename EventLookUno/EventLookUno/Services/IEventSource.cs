using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventLookUno.Models;

namespace EventLookUno.Services;

/// <summary>
/// Platform-abstracted Windows Event Log source.
/// <para>
/// WindowsEventSource (<c>#if WINDOWS</c>) adapts <c>System.Diagnostics.Eventing.Reader.EventRecord</c>
/// into <see cref="EventEntry"/> at the service boundary. Other heads get
/// <c>MockEventSource</c>, which streams canned data so the UI is fully exercisable
/// on WebAssembly, macOS, and Linux Desktop without the Windows APIs.
/// </para>
/// See <c>MIGRATION-GUIDE.md</c> §6 — Platform-Specific API Abstraction (Level 2).
/// </summary>
public interface IEventSource
{
    /// <summary>
    /// Reads events from <paramref name="source"/> between the given UTC bounds. Reports
    /// <see cref="ProgressInfo"/> batches as events arrive. Long-running; respect the token.
    /// </summary>
    /// <returns>Total events read.</returns>
    Task<int> ReadAsync(
        LogSource source,
        DateTime fromUtc,
        DateTime toUtc,
        bool readFromNewest,
        IProgress<ProgressInfo> progress,
        CancellationToken ct);

    /// <summary>
    /// Subscribes to the live event stream on a channel (auto-refresh). Returns false for
    /// .evtx-file sources or when the underlying platform doesn't support live subscription.
    /// </summary>
    bool SubscribeEvents(LogSource source, IProgress<ProgressInfo> progress);

    /// <summary>Stops any active subscription started by <see cref="SubscribeEvents"/>.</summary>
    void UnsubscribeEvents();

    /// <summary>Validates that <paramref name="path"/> resolves to a readable event log.</summary>
    bool IsValidEventLog(string path, PathType type);

    /// <summary>
    /// For an .evtx file, returns the machine name recorded in the latest event, or an empty string.
    /// </summary>
    string GetComputerNameFromEvtx(string filePath);

    /// <summary>
    /// Enumerates available event-log channels on the local machine. Used by the Log Picker dialog.
    /// </summary>
    IAsyncEnumerable<LogChannel> EnumerateChannelsAsync(CancellationToken ct);
}

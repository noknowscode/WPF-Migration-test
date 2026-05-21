using System.Collections.Generic;

namespace EventLookUno.Models;

/// <summary>
/// Reported by <see cref="Services.IEventSource"/> implementations as batches of events are read.
/// </summary>
public class ProgressInfo(
    IReadOnlyList<EventEntry> eventEntries,
    bool isComplete,
    bool isFirst,
    int totalEventCount = 0,
    string message = "")
{
    public IReadOnlyList<EventEntry> LoadedEvents { get; } = eventEntries;
    public bool IsComplete { get; } = isComplete;
    public bool IsFirst { get; } = isFirst;

    /// <summary>
    /// Record count for a local event-log channel. Can be 0 when the count isn't determined
    /// yet, isn't applicable (e.g. .evtx file), or when auto refresh is on.
    /// </summary>
    public int RecordCountInfo { get; } = totalEventCount;

    public string Message { get; } = message;
}

namespace EventLookUno.Models;

/// <summary>
/// Metadata for a discoverable Windows event-log channel (name + optional record count).
/// Populated by <see cref="Services.IEventSource.EnumerateChannelsAsync"/>.
/// </summary>
public class LogChannel
{
    public string Path { get; set; } = string.Empty;
    public long? RecordCount { get; set; }
}

namespace EventLookUno.Models;

/// <summary>
/// Cross-platform replacement for <c>System.Diagnostics.Eventing.Reader.PathType</c>.
/// Indicates whether a <see cref="LogSource.Path"/> is an event-log channel name or a file path to an .evtx file.
/// </summary>
public enum PathType
{
    LogName = 1,
    FilePath = 2,
}

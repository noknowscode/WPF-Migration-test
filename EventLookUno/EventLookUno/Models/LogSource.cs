using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace EventLookUno.Models;

/// <summary>
/// Represents a log source — either a local event-log channel or an .evtx file.
/// </summary>
public class LogSource
{
    public LogSource(string path, PathType type = PathType.LogName, string? computerName = null)
    {
        Path = path;
        PathType = type;
        FileWriteTime = DateTime.Now;

        if (type == PathType.FilePath)
        {
            DisplayName = System.IO.Path.GetFileName(Path);
            if (!string.IsNullOrEmpty(computerName))
                DisplayName += $" ({computerName})";

            try
            {
                FileWriteTime = File.GetLastWriteTime(path);
            }
            catch (Exception) { }
        }
        else
        {
            DisplayName = $"{path} (local)";
        }
    }

    public string Path { get; }
    public PathType PathType { get; }
    public string DisplayName { get; }

    /// <summary>
    /// If the source is a .evtx file, represents the file's last write time.
    /// </summary>
    public DateTime FileWriteTime { get; }
}

public class LogSourceMgr
{
    public LogSourceMgr(IEnumerable<string>? logNames)
    {
        var names = logNames?.ToList();
        if (names is null || names.Count == 0)
            names = DefaultLogSources.ToList();

        LogSources = new ObservableCollection<LogSource>(names.Select(x => new LogSource(x)));
    }

    public ObservableCollection<LogSource> LogSources { get; }

    public static readonly IReadOnlyList<string> DefaultLogSources = new[] { "System", "Application" };

    /// <summary>
    /// Adds an evtx file or a local event-log channel to the source list.
    /// Pre-validation must be done by the caller (via <see cref="Services.IEventSource"/>).
    /// </summary>
    public LogSource AddLogSource(string path, PathType type, string? computerName = null, bool addToBottom = false)
    {
        var src = new LogSource(path, type, computerName);
        if (addToBottom)
            LogSources.Add(src);
        else
            LogSources.Insert(0, src);
        return src;
    }
}

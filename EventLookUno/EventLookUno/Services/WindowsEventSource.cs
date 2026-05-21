// Windows-only implementation. The file is always compiled (no #if WINDOWS guard anymore
// since Slot 2 dropped the WinAppSDK TFM), but every method is gated by
// [SupportedOSPlatform("windows")]; DI only picks this impl at runtime on Windows.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventLookUno.Models;
// Models.PathType is the contract type; Windows EventLog API has its own PathType in the same
// simple-name space, so alias the Windows one to avoid CS0104 ambiguity.
using WinPathType = System.Diagnostics.Eventing.Reader.PathType;

namespace EventLookUno.Services;

/// <summary>
/// Windows implementation of <see cref="IEventSource"/>, wrapping
/// <see cref="EventLogReader"/>, <see cref="EventLogQuery"/>, and <see cref="EventLogWatcher"/>.
/// <para>
/// Compiled only under the <c>net10.0-windows*</c> TFM — excluded from Desktop and WebAssembly builds.
/// Adapts <see cref="EventRecord"/> into the cross-platform <see cref="EventEntry"/> POCO at the
/// service boundary so no Windows-only type leaks into the ViewModel or XAML.
/// </para>
/// Ported from the source app's <c>Model/DataService.cs</c>. The inner RPC-retry workaround
/// for <c>WIN32ERROR_RPC_S_INVALID_BOUND</c> is preserved verbatim.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsEventSource : IEventSource
{
    private const int WIN32ERROR_RPC_S_INVALID_BOUND = unchecked((int)0x800706C6);

    private EventLogWatcher? _watcher;
    private IProgress<ProgressInfo>? _watcherProgress;
    private LogSource? _watcherSource;

    public async Task<int> ReadAsync(
        LogSource source,
        DateTime fromUtc,
        DateTime toUtc,
        bool readFromNewest,
        IProgress<ProgressInfo> progress,
        CancellationToken ct)
    {
        var entries = new List<EventEntry>();
        var toDispose = new List<EventRecord>();
        EventLogReader? reader = null;
        EventRecord? eventRecord = null;
        string errorMessage = string.Empty;
        int totalCount = 0;
        int count = 0;
        bool isFirst = true;

        try
        {
            string queryXPath = string.Format(
                " *[System[TimeCreated[@SystemTime > '{0}' and @SystemTime <= '{1}']]]",
                fromUtc.ToString("o"),
                toUtc.ToString("o"));

            var query = new EventLogQuery(source.Path, (WinPathType)(int)source.PathType, queryXPath)
            {
                ReverseDirection = readFromNewest,
            };

            _ = Task.Run(() => totalCount = GetRecordCount(source), ct);

            reader = new EventLogReader(query);
            await Task.Run(() =>
            {
                int retryCount = 0;
                while (true)
                {
                    try
                    {
                        eventRecord = reader.ReadEvent();
                    }
                    catch (EventLogException ex)
                        when (ex.HResult == WIN32ERROR_RPC_S_INVALID_BOUND
                              && retryCount < 3
                              && eventRecord?.Bookmark != null)
                    {
                        // Workaround: EventLogReader.ReadEvent can throw "The array bounds are invalid".
                        reader.Seek(eventRecord.Bookmark, 1);
                        if (reader.BatchSize > 1)
                            reader.BatchSize /= 2;
                        retryCount++;
                        Debug.WriteLine($"Retry #{retryCount} after RPC bounds error. Last RecordId={eventRecord.RecordId}.");
                        continue;
                    }

                    if (eventRecord == null)
                        break;

                    ct.ThrowIfCancellationRequested();

                    entries.Add(Adapt(eventRecord, source));
                    toDispose.Add(eventRecord);
                    count++;

                    if (count % 100 == 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        progress.Report(new ProgressInfo(entries.AsReadOnly(), isComplete: false, isFirst, totalCount));
                        isFirst = false;
                        toDispose.DisposeAll();
                        toDispose.Clear();
                        entries = new List<EventEntry>();
                    }
                }
            }, ct);
        }
        catch (OperationCanceledException ex)
        {
            errorMessage = ex.Message;
        }
        catch (EventLogNotFoundException ex)
        {
            errorMessage = ex.Message;
        }
        catch (EventLogException ex)
        {
            errorMessage = ex.Message;
        }
        catch (UnauthorizedAccessException)
        {
            errorMessage = "Unauthorized access to the channel. Try Run as Administrator.";
        }
        finally
        {
            progress.Report(new ProgressInfo(entries.AsReadOnly(), isComplete: true, isFirst, totalCount, errorMessage));
            toDispose.DisposeAll();
            reader?.Dispose();
        }

        return count;
    }

    public bool SubscribeEvents(LogSource source, IProgress<ProgressInfo> progress)
    {
        if (source.PathType == EventLookUno.Models.PathType.FilePath)
            return false;

        UnsubscribeEvents();

        try
        {
            _watcherSource = source;
            _watcherProgress = progress;
            _watcher = new EventLogWatcher(source.Path);
            _watcher.EventRecordWritten += Watcher_EventRecordWritten;
            _watcher.Enabled = true;
            return true;
        }
        catch (EventLogException)
        {
            return false;
        }
    }

    public void UnsubscribeEvents()
    {
        if (_watcher is null) return;

        _watcher.EventRecordWritten -= Watcher_EventRecordWritten;
        _watcher.Enabled = false;
        _watcher.Dispose();
        _watcher = null;
        _watcherProgress = null;
        _watcherSource = null;
    }

    private void Watcher_EventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
    {
        if (e.EventRecord is null || _watcherSource is null || _watcherProgress is null)
            return;

        try
        {
            var entry = Adapt(e.EventRecord, _watcherSource);
            _watcherProgress.Report(new ProgressInfo(new[] { entry }, isComplete: true, isFirst: true));
        }
        catch (Exception ex)
        {
            _watcherProgress.Report(new ProgressInfo(Array.Empty<EventEntry>(), isComplete: true, isFirst: true, 0, ex.Message));
        }
        finally
        {
            e.EventRecord?.Dispose();
        }
    }

    public bool IsValidEventLog(string path, EventLookUno.Models.PathType type)
    {
        if (type == EventLookUno.Models.PathType.LogName)
        {
            try
            {
                _ = new EventLogConfiguration(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        try
        {
            using var session = new EventLogSession();
            _ = session.GetLogInformation(path, (WinPathType)(int)type);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string GetComputerNameFromEvtx(string filePath)
    {
        try
        {
            var query = new EventLogQuery(filePath, WinPathType.FilePath, "*")
            {
                ReverseDirection = true,
            };
            using var reader = new EventLogReader(query);
            using var record = reader.ReadEvent();
            return record?.MachineName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public async IAsyncEnumerable<LogChannel> EnumerateChannelsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var session = new EventLogSession();

        var channelNames = session.GetLogNames();
        foreach (var name in channelNames)
        {
            ct.ThrowIfCancellationRequested();
            long? recordCount = null;
            try
            {
                var info = session.GetLogInformation(name, WinPathType.LogName);
                recordCount = (long?)info.RecordCount;
            }
            catch { /* not all channels expose a count; leave null */ }

            yield return new LogChannel { Path = name, RecordCount = recordCount };
            await Task.Yield();
        }
    }

    private static int GetRecordCount(LogSource source)
    {
        if (source.PathType != EventLookUno.Models.PathType.LogName)
            return 0;

        try
        {
            using var session = new EventLogSession();
            var info = session.GetLogInformation(source.Path, WinPathType.LogName);
            return Convert.ToInt32(info.RecordCount ?? 0);
        }
        catch
        {
            return 0;
        }
    }

    private static EventEntry Adapt(EventRecord record, LogSource logSource)
    {
        string message;
        try
        {
            message = record.FormatDescription() ?? DumpEventData(record);
        }
        catch (Exception ex)
        {
            message = "(EventLookUno) Exception occurred while reading the description:\r\n" + ex.Message;
        }

        return new EventEntry
        {
            RecordId = record.RecordId,
            Id = record.Id,
            Level = record.Level,
            ProviderName = record.ProviderName ?? string.Empty,
            TimeOfEvent = (record.TimeCreated?.ToUniversalTime()) ?? DateTime.MinValue.ToUniversalTime(),
            Message = message,
            LogSourcePath = logSource.Path,
            LogSourcePathType = logSource.PathType,
        };
    }

    private static string DumpEventData(EventRecord record)
    {
        var sb = new StringBuilder("(EventLookUno - dump of EventData)\r\n");
        for (int i = 0; i < record.Properties.Count; i++)
        {
            if (i > 0) sb.Append("\r\n");
            var value = record.Properties[i].Value;
            switch (value)
            {
                case string s: sb.Append(s); break;
                case byte[] bytes:
                    foreach (var b in bytes) sb.Append($"{b:X2}");
                    break;
            }
        }
        return sb.ToString();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventLookUno.Models;

namespace EventLookUno.Services;

/// <summary>
/// Cross-platform fallback implementation of <see cref="IEventSource"/>. Streams a
/// deterministic set of synthetic events so the DataGrid, filters, sort, and auto-refresh
/// highlight all exercise on Desktop and WebAssembly heads where the Windows Event Log APIs
/// aren't available.
/// <para>
/// Registered by <c>App.xaml.cs</c> for every non-Windows TFM. On Windows, the real
/// <see cref="WindowsEventSource"/> takes its place via <c>#if WINDOWS</c> DI selection.
/// </para>
/// </summary>
public sealed class MockEventSource : IEventSource
{
    private static readonly string[] Providers = new[]
    {
        "Microsoft-Windows-Kernel-General",
        "Microsoft-Windows-WLAN-AutoConfig",
        "Service Control Manager",
        "Application Popup",
        "Microsoft-Windows-DNS-Client",
        "Microsoft-Windows-Winlogon",
        "Microsoft-Windows-Security-Auditing",
        "Microsoft-Windows-TaskScheduler",
        "Microsoft-Windows-Diagnostics-Performance",
    };

    private static readonly (byte Level, string Message)[] Templates = new[]
    {
        ((byte)2, "An error was encountered while processing request {0}. Retry scheduled."),
        ((byte)3, "Warning: disk space on volume C:\\ is below the healthy threshold."),
        ((byte)4, "The service started successfully and is accepting requests."),
        ((byte)4, "Scheduled task '{0}' completed successfully."),
        ((byte)3, "The configured network adapter failed to connect to the access point."),
        ((byte)1, "CRITICAL: Unexpected shutdown detected on restart at {0:yyyy-MM-dd HH:mm:ss}."),
        ((byte)5, "Verbose trace: handler invoked for correlation {0}."),
        ((byte)4, "User session {0} established from local console."),
    };

    private static readonly int[] Ids = new[] { 1, 4, 11, 42, 100, 1000, 4624, 6005, 7036, 16384 };

    public Task<int> ReadAsync(
        LogSource source,
        DateTime fromUtc,
        DateTime toUtc,
        bool readFromNewest,
        IProgress<ProgressInfo> progress,
        CancellationToken ct)
    {
        var entries = Generate(source, fromUtc, toUtc, count: 150).ToList();
        if (readFromNewest)
            entries = entries.OrderByDescending(e => e.TimeOfEvent).ToList();
        else
            entries = entries.OrderBy(e => e.TimeOfEvent).ToList();

        // Report in two batches so the UI exercises partial-load + append paths.
        var first = entries.Take(100).ToList();
        var rest = entries.Skip(100).ToList();

        progress.Report(new ProgressInfo(first, isComplete: false, isFirst: true, entries.Count));
        progress.Report(new ProgressInfo(rest, isComplete: true, isFirst: false, entries.Count));

        return Task.FromResult(entries.Count);
    }

    public bool SubscribeEvents(LogSource source, IProgress<ProgressInfo> progress) => false;

    public void UnsubscribeEvents() { }

    public bool IsValidEventLog(string path, PathType type) => true;

    public string GetComputerNameFromEvtx(string filePath) => "MOCK-PC";

    public async IAsyncEnumerable<LogChannel> EnumerateChannelsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var name in new[] { "Application", "Security", "Setup", "System", "ForwardedEvents" })
        {
            ct.ThrowIfCancellationRequested();
            yield return new LogChannel { Path = name, RecordCount = 1000 };
            await Task.Yield();
        }
    }

    private static IEnumerable<EventEntry> Generate(LogSource source, DateTime fromUtc, DateTime toUtc, int count)
    {
        var rng = new Random(42); // deterministic
        var span = toUtc - fromUtc;
        if (span <= TimeSpan.Zero)
            span = TimeSpan.FromDays(1);

        for (int i = 0; i < count; i++)
        {
            var template = Templates[rng.Next(Templates.Length)];
            var provider = Providers[rng.Next(Providers.Length)];
            var time = fromUtc + TimeSpan.FromSeconds(rng.NextDouble() * span.TotalSeconds);

            yield return new EventEntry
            {
                RecordId = 1000 + i,
                Id = Ids[rng.Next(Ids.Length)],
                Level = template.Level,
                ProviderName = provider,
                TimeOfEvent = time,
                Message = string.Format(template.Message, i, time),
                LogSourcePath = source.Path,
                LogSourcePathType = source.PathType,
            };
        }
    }
}

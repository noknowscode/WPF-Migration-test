using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Collections;
using EventLookUno.Models;
using EventLookUno.Services;
using Microsoft.Extensions.Options;
using Microsoft.UI.Dispatching;

// Model type names that clash with BCL types in the using set above:
using TimeRange = EventLookUno.Models.Range;          // vs System.Range
using ProviderFilter = EventLookUno.Models.SourceFilter;  // vs System.Diagnostics.SourceFilter

namespace EventLookUno.Presentation;

/// <summary>
/// DataGrid-focused port of the source <c>MainViewModel</c> (800 LoC WPF original).
/// <para>
/// Ported surface: time-range read, multi-column sort via <see cref="AdvancedCollectionView"/>,
/// column filters, auto-refresh, new-loaded highlight.
/// Deferred surface (tracked as TODO stubs): secondary dialogs (Detail, LogPicker, Settings, About),
/// CSV export, drag-drop .evtx, run-as-admin, OpenFile via FileOpenPicker.
/// </para>
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IEventSource _source;
    private readonly EventLookSettings _settings;
    private readonly DispatcherQueue _dispatcher;

    private readonly RangeMgr _rangeMgr = new();
    private readonly LogSourceMgr _logSourceMgr;

    public ProviderFilter SourceFilter { get; } = new();
    public LevelFilter LevelFilter { get; } = new();
    public IdFilter IdFilter { get; } = new();
    public MessageFilter MsgFilter { get; } = new();
    private readonly List<FilterBase> _filters;

    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _newLoadedTimer;
    private readonly TimeSpan _newLoadedThreshold = TimeSpan.FromSeconds(5);
    private CancellationTokenSource? _cts;

    public MainViewModel(IEventSource source, IOptions<EventLookSettings> settingsOptions)
    {
        _source = source;
        _settings = settingsOptions.Value;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _logSourceMgr = new LogSourceMgr(_settings.StartupLogNames);
        SelectedLogSource = _logSourceMgr.LogSources.FirstOrDefault();

        SelectedRange = _rangeMgr.GetStartupRange(_settings.StartupRangeDays);

        ShowsMillisec = _settings.ShowsMillisec;
        ShowsRecordId = _settings.ShowsRecordId;

        TimeZones = new ObservableCollection<TimeZoneInfo>(TimeZoneInfo.GetSystemTimeZones());
        SelectedTimeZone = TimeZones.FirstOrDefault(x => x.Id == TimeZoneInfo.Local.Id);

        _filters = new List<FilterBase> { SourceFilter, LevelFilter, IdFilter, MsgFilter };

        Events = new ObservableCollection<EventEntry>();
        FilteredEvents = new AdvancedCollectionView(Events, isLiveShaping: true);
        FilteredEvents.Filter = ComposedFilter;
        FilteredEvents.SortDescriptions.Add(new SortDescription(nameof(EventEntry.TimeOfEvent), SortDirection.Descending));

        foreach (var f in _filters)
            f.FilterUpdated += OnFilterUpdated;

        _newLoadedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _newLoadedTimer.Tick += (_, _) =>
        {
            if (!UpdateIsNewLoaded())
                _newLoadedTimer.Stop();
        };

        IsRunAsAdmin = ProcessHelper.IsElevated;
    }

    // ===== Public surface bound to by XAML =====

    public ObservableCollection<EventEntry> Events { get; }
    public AdvancedCollectionView FilteredEvents { get; }
    public ObservableCollection<LogSource> LogSources => _logSourceMgr.LogSources;
    public ObservableCollection<TimeRange> Ranges => _rangeMgr.Ranges;
    public ObservableCollection<TimeZoneInfo> TimeZones { get; }
    public bool IsRunAsAdmin { get; }

    [ObservableProperty] public partial LogSource? SelectedLogSource { get; set; }
    [ObservableProperty] public partial TimeRange? SelectedRange { get; set; }
    [ObservableProperty] public partial TimeZoneInfo? SelectedTimeZone { get; set; }
    [ObservableProperty] public partial EventEntry? SelectedEventEntry { get; set; }

    [ObservableProperty] public partial DateTime FromDateTime { get; set; }
    [ObservableProperty] public partial DateTime ToDateTime { get; set; }

    [ObservableProperty] public partial bool ShowsMillisec { get; set; }
    [ObservableProperty] public partial bool ShowsRecordId { get; set; }
    [ObservableProperty] public partial bool IsAutoRefreshEnabled { get; set; }

    [ObservableProperty] public partial bool IsUpdating { get; set; }
    [ObservableProperty] public partial bool IsAutoRefreshing { get; set; }
    [ObservableProperty] public partial bool IsAppend { get; set; }

    [ObservableProperty] public partial int LoadedEventCount { get; set; }
    [ObservableProperty] public partial int TotalEventCount { get; set; }
    [ObservableProperty] public partial int VisibleEventCount { get; set; }
    [ObservableProperty] public partial int AppendCount { get; set; }
    [ObservableProperty] public partial bool AreEventsFiltered { get; set; }
    [ObservableProperty] public partial string ErrorMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial TimeSpan LastElapsedTime { get; set; }

    /// <summary>
    /// Computed status text replacing the WPF <c>StatusTextConverter</c> (MultiBinding with
    /// 8 source bindings). WinUI has no IMultiValueConverter — the VM composes the text.
    /// </summary>
    public string StatusText =>
        !string.IsNullOrEmpty(ErrorMessage) ? $"Error: {ErrorMessage}" :
        IsUpdating ? (IsAppend ? $"Appending… {AppendCount} new" : "Loading…") :
        IsAutoRefreshing ? $"Auto-refresh on · {LoadedEventCount} events loaded ({LastElapsedTime.TotalSeconds:F1}s)" :
        TotalEventCount > 0 ? $"{LoadedEventCount} of {TotalEventCount} events ({LastElapsedTime.TotalSeconds:F1}s)" :
        $"{LoadedEventCount} events ({LastElapsedTime.TotalSeconds:F1}s)";

    /// <summary>
    /// Computed filter-info text replacing WPF <c>FilterInfoTextConverter</c> (MultiBinding).
    /// </summary>
    public string FilterInfoText => (IsUpdating || !AreEventsFiltered)
        ? string.Empty
        : $"  (filtered: {VisibleEventCount} shown)";

    // ===== Commands =====

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private Task RefreshAsync() => RefreshCoreAsync(reset: false, append: true);

    private bool CanRefresh() => !IsUpdating && !IsAutoRefreshing;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
    }
    private bool CanCancel() => IsUpdating;

    [RelayCommand]
    private void ResetFilters()
    {
        foreach (var f in _filters)
            f.Clear();
    }

    [RelayCommand]
    private void LaunchEventViewer() => ProcessHelper.LaunchEventViewer(SelectedLogSource);

    [RelayCommand]
    private void OpenGitHub() => ProcessHelper.OpenUri("https://github.com/kmaki565/EventLook");

    // TODO(phase-7): OpenFile → FileOpenPicker → LogSourceMgr.AddLogSource
    // TODO(phase-7): OpenLogPicker → ContentDialog(LogPickerViewModel)
    // TODO(phase-7): OpenSettings → ContentDialog(SettingsViewModel)
    // TODO(phase-7): OpenDetails → ContentDialog(DetailViewModel)
    // TODO(phase-8): ExportToCsv → FileSavePicker + TextHelper.EscapeCsvValue
    // TODO(phase-8): CopyMessageText → Windows.ApplicationModel.DataTransfer.Clipboard

    // ===== Lifecycle =====

    public Task OnLoadedAsync() => RefreshCoreAsync(reset: true, append: false);

    // ===== Core read loop =====

    private async Task RefreshCoreAsync(bool reset, bool append)
    {
        if (SelectedLogSource is null || SelectedRange is null)
            return;

        IsAppend = append && !SelectedRange.IsCustom
            && SelectedLogSource.PathType == PathType.LogName;

        if (IsAppend)
            UpdateIsNewLoaded(all: true);

        IsAutoRefreshing = false;

        UpdateDateTimeInUi();

        if (reset)
            foreach (var f in _filters) f.Reset();

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        await UpdateAsync(_source.ReadAsync(
            SelectedLogSource,
            IsAppend && Events.Count > 0 ? Events.First().TimeOfEvent : FromDateTime,
            ToDateTime,
            readFromNewest: !IsAppend,
            new System.Progress<ProgressInfo>(OnProgress),
            _cts.Token));

        if (!IsUpdating)
            foreach (var f in _filters) f.Populate(Events, reset);
    }

    private async Task UpdateAsync(Task readTask)
    {
        try
        {
            _stopwatch.Restart();
            TotalEventCount = 0;
            if (!IsAppend) LoadedEventCount = 0;
            IsUpdating = true;
            OnPropertyChanged(nameof(StatusText));
            RefreshCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();

            await readTask;
        }
        finally
        {
            IsUpdating = false;
            _stopwatch.Stop();
            LastElapsedTime = _stopwatch.Elapsed;
            OnPropertyChanged(nameof(StatusText));
            RefreshCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    private void OnProgress(ProgressInfo info)
    {
        // IProgress<T> marshals back to the captured SynchronizationContext which, in Uno
        // apps, is the UI thread — so direct mutation is safe. Keep the DispatcherQueue
        // guard in case future code paths bypass IProgress.
        void Apply()
        {
            if (IsAppend)
            {
                if (info.IsFirst) AppendCount = 0;
                AppendCount += InsertEvents(info.LoadedEvents);
            }
            else
            {
                if (info.IsFirst)
                    Events.Clear();

                foreach (var e in info.LoadedEvents)
                    Events.Add(e);
            }

            LoadedEventCount = Events.Count;
            TotalEventCount = (SelectedRange is { DaysFromNow: 0, IsCustom: false }) ? info.RecordCountInfo : 0;
            ErrorMessage = info.Message;
            VisibleEventCount = FilteredEvents.Count;
            AreEventsFiltered = VisibleEventCount < Events.Count;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(FilterInfoText));
        }

        if (_dispatcher.HasThreadAccess)
            Apply();
        else
            _dispatcher.TryEnqueue(Apply);
    }

    private int InsertEvents(IReadOnlyList<EventEntry> events)
    {
        int count = 0;
        foreach (var e in events)
        {
            e.IsNewLoaded = true;
            e.TimeLoaded = DateTime.Now;
            Events.Insert(0, e);
            count++;
        }
        if (count > 0 && !_newLoadedTimer.IsEnabled)
            _newLoadedTimer.Start();
        return count;
    }

    private bool UpdateIsNewLoaded(bool all = false)
    {
        var now = DateTime.Now;
        foreach (var e in Events.TakeWhile(e => e.IsNewLoaded).ToList())
        {
            if (all || now - e.TimeLoaded >= _newLoadedThreshold)
                e.IsNewLoaded = false;
        }
        return Events.Any(e => e.IsNewLoaded);
    }

    private void UpdateDateTimeInUi()
    {
        if (SelectedRange is null) return;

        if (SelectedRange.IsCustom)
        {
            FromDateTime = TruncateToMinutes(FromDateTime);
            ToDateTime = TruncateToMinutes(ToDateTime);
        }
        else if (SelectedRange.DaysFromNow == 0)
        {
            // "All time" — unbounded range
            FromDateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            ToDateTime = new DateTime(2030, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        }
        else
        {
            ToDateTime = SelectedLogSource?.PathType == PathType.FilePath
                ? SelectedLogSource.FileWriteTime.ToUniversalTime()
                : DateTime.UtcNow;
            if (!IsAppend)
                FromDateTime = ToDateTime - TimeSpan.FromDays(SelectedRange.DaysFromNow);
        }
    }

    private static DateTime TruncateToMinutes(DateTime dt) =>
        new(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, dt.Kind);

    // ===== Filter plumbing =====

    private bool ComposedFilter(object o) => _filters.All(f => f.IsFilterMatched((EventEntry)o));

    private void OnFilterUpdated(object? sender, EventArgs e)
    {
        FilteredEvents.RefreshFilter();
        VisibleEventCount = FilteredEvents.Count;
        AreEventsFiltered = VisibleEventCount < Events.Count;
        OnPropertyChanged(nameof(FilterInfoText));
    }

    // React to property changes that affect status text / range
    partial void OnSelectedRangeChanged(TimeRange? value)
    {
        if (value is null) return;
        if (value.IsCustom) IsAutoRefreshEnabled = false;
    }

    partial void OnSelectedLogSourceChanged(LogSource? value)
    {
        if (value is null) return;
        _source.UnsubscribeEvents();
        // Defer auto-refresh here until a subsequent Refresh call succeeds
    }

    partial void OnIsAutoRefreshEnabledChanged(bool value)
    {
        if (!value)
        {
            _source.UnsubscribeEvents();
            IsAutoRefreshing = false;
        }
    }
}

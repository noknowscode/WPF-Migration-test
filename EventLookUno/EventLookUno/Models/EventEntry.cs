using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EventLookUno.Models;

/// <summary>
/// Cross-platform row model for a single event-log record.
/// <para>
/// Flattens the fields of <c>System.Diagnostics.Eventing.Reader.EventRecord</c> (Windows-only)
/// into a POCO that XAML and filters can bind to on every Uno head. The Windows implementation
/// of <see cref="Services.IEventSource"/> adapts <c>EventRecord</c> into <see cref="EventEntry"/>;
/// other heads produce these directly from mock/JSON sources.
/// </para>
/// </summary>
public partial class EventEntry : ObservableObject
{
    public EventEntry()
    {
        TimeLoaded = System.DateTime.UtcNow;
    }

    public long? RecordId { get; init; }
    public int Id { get; init; }
    public byte? Level { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public System.DateTime TimeOfEvent { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Xml { get; init; }

    public string LogSourcePath { get; init; } = string.Empty;
    public PathType LogSourcePathType { get; init; } = PathType.LogName;

    public string MessageOneLine => Regex.Replace(Message, @"[\r\n]+", " ");

    public System.DateTime TimeLoaded { get; set; }

    // Partial-property syntax (C# 13): required by MVVMTK0045 — [ObservableProperty] on a field
    // generates code that isn't AOT-compatible for WinRT/WinUI 3. See MIGRATION-GUIDE §17.
    [ObservableProperty]
    public partial bool IsNewLoaded { get; set; }
}

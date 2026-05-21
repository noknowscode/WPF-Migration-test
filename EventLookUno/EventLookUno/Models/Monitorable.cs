using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EventLookUno.Models;

/// <summary>
/// Minimal INPC base used by filter items (SourceFilterItem, LevelFilterItem).
/// </summary>
public class Monitorable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void NotifyPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EventLookUno.Models;

public class LevelFilter : FilterBase
{
    private readonly ObservableCollection<LevelFilterItem> _items;
    public ReadOnlyObservableCollection<LevelFilterItem> LevelFilters { get; }

    public LevelFilter()
    {
        _items = new ObservableCollection<LevelFilterItem>();
        LevelFilters = new ReadOnlyObservableCollection<LevelFilterItem>(_items);
    }

    public override void Populate(IEnumerable<EventEntry> events, bool reset)
    {
        var prev = reset ? null : _items.Select(f => new { f.Level, f.Selected }).ToList();
        _items.Clear();

        foreach (var lv in events.Select(e => e.Level).Distinct().OrderBy(lv => lv))
        {
            _items.Add(new LevelFilterItem
            {
                Level = lv,
                Selected = prev?.FirstOrDefault(f => f.Level == lv)?.Selected ?? true
            });
        }
        Apply();
    }

    public override void Clear()
    {
        foreach (var item in _items)
            item.Selected = true;
        Apply();
    }

    public override void Reset() => _items.Clear();

    public override void Apply()
    {
        if (IsFilterSelectionsChanged())
            base.Apply();
    }

    public override bool IsFilterMatched(EventEntry evt)
    {
        if (_items.Count == 0)
            return true;

        return _items.Where(item => item.Selected).Any(item => item.Level == evt.Level);
    }

    /// <summary>Checks only the given level, unchecks the rest. Returns false when the level isn't in the list.</summary>
    public bool SetSingleFilter(byte? level)
    {
        if (!_items.Any(x => x.Level == level))
            return false;

        foreach (var item in _items)
            item.Selected = item.Level == level;

        return true;
    }

    /// <summary>Unchecks (excludes) the given level. Returns false when the level isn't in the list.</summary>
    public bool UncheckFilter(byte? level)
    {
        if (!_items.Any(x => x.Level == level))
            return false;

        foreach (var item in _items)
        {
            if (item.Level == level)
                item.Selected = false;
        }
        return true;
    }

    private readonly List<LevelFilterItem> _prev = new();
    private bool IsFilterSelectionsChanged()
    {
        var changed = _items.Count != _prev.Count
            || _items.Where((item, i) => item.Level != _prev[i].Level || item.Selected != _prev[i].Selected).Any();

        if (changed)
        {
            _prev.Clear();
            foreach (var item in _items)
                _prev.Add(new LevelFilterItem { Level = item.Level, Selected = item.Selected });
        }
        return changed;
    }
}

public class LevelFilterItem : Monitorable
{
    private byte? _level;
    public byte? Level
    {
        get => _level;
        set
        {
            if (value == _level) return;
            _level = value;
            NotifyPropertyChanged();
        }
    }

    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set
        {
            if (value == _selected) return;
            _selected = value;
            NotifyPropertyChanged();
        }
    }

    public override string ToString()
    {
        if (!_level.HasValue)
            return "Invalid level";

        // Level 0 is shown as Information in the Windows Event Viewer.
        return _level.Value switch
        {
            1 => "Critical",
            2 => "Error",
            3 => "Warning",
            4 => "Information",
            5 => "Verbose",
            _ => "Unknown level"
        };
    }
}

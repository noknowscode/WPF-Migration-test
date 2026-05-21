using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EventLookUno.Models;

public class SourceFilter : FilterBase
{
    private readonly ObservableCollection<SourceFilterItem> _items;
    public ReadOnlyObservableCollection<SourceFilterItem> SourceFilters { get; }

    public SourceFilter()
    {
        _items = new ObservableCollection<SourceFilterItem>();
        SourceFilters = new ReadOnlyObservableCollection<SourceFilterItem>(_items);
    }

    public override void Populate(IEnumerable<EventEntry> events, bool reset)
    {
        var prev = reset ? null : _items.Select(f => new { f.Name, f.Selected }).ToList();
        _items.Clear();

        foreach (var name in events.Select(e => e.ProviderName).Distinct().OrderBy(s => s))
        {
            _items.Add(new SourceFilterItem
            {
                Name = name,
                Selected = prev?.FirstOrDefault(f => f.Name == name)?.Selected ?? true
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
        // When "Select all" toggles in the CheckComboBox, this fires once per checkbox.
        // The selection delta is already present on the first call, so subsequent calls noop.
        if (IsFilterSelectionsChanged())
            base.Apply();
    }

    public override bool IsFilterMatched(EventEntry evt)
    {
        if (_items.Count == 0)
            return true;

        return _items.Where(item => item.Selected).Any(item => string.Equals(item.Name, evt.ProviderName));
    }

    public bool SetSingleFilter(string name)
    {
        if (!_items.Any(x => x.Name == name))
            return false;

        foreach (var item in _items)
            item.Selected = item.Name == name;

        return true;
    }

    public bool UncheckFilter(string name)
    {
        if (!_items.Any(x => x.Name == name))
            return false;

        foreach (var item in _items)
        {
            if (item.Name == name)
                item.Selected = false;
        }
        return true;
    }

    private readonly List<SourceFilterItem> _prev = new();
    private bool IsFilterSelectionsChanged()
    {
        var changed = _items.Count != _prev.Count
            || _items.Where((item, i) => item.Name != _prev[i].Name || item.Selected != _prev[i].Selected).Any();

        if (changed)
        {
            _prev.Clear();
            foreach (var item in _items)
                _prev.Add(new SourceFilterItem { Name = item.Name, Selected = item.Selected });
        }
        return changed;
    }
}

public class SourceFilterItem : Monitorable
{
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            if (value == _name) return;
            _name = value;
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
}

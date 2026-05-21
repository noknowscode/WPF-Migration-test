using System;
using System.Collections.Generic;
using System.Linq;

namespace EventLookUno.Models;

public class IdFilter : FilterBase
{
    // Event IDs are always >= 0.
    private readonly List<uint> _filterIds = new();
    private readonly List<uint> _excludeIds = new();

    // Exposed for tests.
    public IReadOnlyList<uint> FilterIds => _filterIds.AsReadOnly();
    public IReadOnlyList<uint> ExcludeIds => _excludeIds.AsReadOnly();

    private string _filterText = string.Empty;
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (value == _filterText)
                return;

            _filterText = value;
            UpdateFilterIds(_filterText);
            Apply();
            NotifyPropertyChanged();
        }
    }

    /// <summary>
    /// Appends an ID to the filter text. Pass <paramref name="isExclude"/>=true to prefix with '-'.
    /// </summary>
    public void AddFilterId(int id, bool isExclude)
    {
        FilterText += ((_filterIds.Count + _excludeIds.Count > 0) ? "," : string.Empty)
            + (isExclude ? $"-{id}" : $"{id}");
    }

    public override void Clear() => FilterText = string.Empty;

    public override bool IsFilterMatched(EventEntry evt)
    {
        if (_filterIds.Count + _excludeIds.Count == 0)
            return true;

        if (_excludeIds.Any(fi => evt.Id == fi))
            return false;

        if (_filterIds.Count == 0)
            return true;

        return _filterIds.Any(fi => evt.Id == fi);
    }

    private void UpdateFilterIds(string filterText)
    {
        _filterIds.Clear();
        _excludeIds.Clear();

        foreach (var part in filterText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();
            if (trimmed == "-0")
            {
                _excludeIds.Add(0);
                continue;
            }

            // Parse-exception is intentional — the binding catches it via ValidatesOnExceptions.
            int id = int.Parse(trimmed);

            if (id < 0)
                _excludeIds.Add((uint)(-1 * id));
            else
                _filterIds.Add((uint)id);
        }
    }
}

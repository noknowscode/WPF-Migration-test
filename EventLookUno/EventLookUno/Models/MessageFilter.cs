using System.Collections.Generic;
using System.Linq;

namespace EventLookUno.Models;

public class MessageFilter : FilterBase
{
    private readonly List<AndSearchMaterial> _andGroups = new();

    private string _messageFilterText = string.Empty;
    public string MessageFilterText
    {
        get => _messageFilterText;
        set
        {
            if (value == _messageFilterText)
                return;

            _messageFilterText = value;
            NotifyPropertyChanged();

            _andGroups.Clear();
            // '|' separates OR groups; each group is then AND-searched.
            foreach (var group in _messageFilterText.Split('|').Where(x => !string.IsNullOrWhiteSpace(x)))
                _andGroups.Add(new AndSearchMaterial(group));

            Apply();
        }
    }

    public override void Clear() => MessageFilterText = string.Empty;

    public override bool IsFilterMatched(EventEntry evt)
    {
        if (_messageFilterText.Length == 0)
            return true;

        return _andGroups.Any(g => TextHelper.IsTextMatched(evt.Message, g));
    }
}

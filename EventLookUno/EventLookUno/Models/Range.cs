using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EventLookUno.Models;

public class Range
{
    public string Text { get; set; } = string.Empty;
    public int DaysFromNow { get; set; }
    public bool IsCustom { get; set; }
}

public class RangeMgr
{
    public RangeMgr()
    {
        Ranges = new ObservableCollection<Range>
        {
            new Range { Text = "Last 24 hours", DaysFromNow = 1,  IsCustom = false },
            new Range { Text = "Last 3 days",   DaysFromNow = 3,  IsCustom = false },
            new Range { Text = "Last 7 days",   DaysFromNow = 7,  IsCustom = false },
            new Range { Text = "Last 15 days",  DaysFromNow = 15, IsCustom = false },
            new Range { Text = "Last 30 days",  DaysFromNow = 30, IsCustom = false },
            new Range { Text = "All time",      DaysFromNow = 0,  IsCustom = false },
            new Range { Text = "Custom range",  DaysFromNow = 0,  IsCustom = true  },
        };
    }

    public ObservableCollection<Range> Ranges { get; }

    /// <summary>
    /// Picks the startup range matching the given days-from-now value, or "All time" as fallback.
    /// Called with <see cref="EventLookSettings.StartupRangeDays"/> at app launch.
    /// </summary>
    public Range GetStartupRange(int startupRangeDays)
    {
        return Ranges.FirstOrDefault(r => r.DaysFromNow == startupRangeDays && !r.IsCustom)
            ?? Ranges.First(r => r.DaysFromNow == 0 && !r.IsCustom);
    }
}

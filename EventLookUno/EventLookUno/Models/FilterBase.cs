using System;
using System.Collections.Generic;

namespace EventLookUno.Models;

/// <summary>
/// Base class for DataGrid column filters.
/// <para>
/// Refactored from the WPF version which hooked <c>CollectionViewSource.Filter</c>.
/// In Uno/WinUI, <see cref="CommunityToolkit.WinUI.Collections.AdvancedCollectionView"/>
/// takes a predicate and the view layer wires up <see cref="FilterUpdated"/> to call
/// <c>acv.RefreshFilter()</c>. Filters own no XAML dependency.
/// </para>
/// </summary>
public abstract class FilterBase : Monitorable
{
    /// <summary>
    /// For filters with checkboxes, populates the list of checkboxes from the loaded events.
    /// Text-based filters can ignore this. When <paramref name="reset"/> is true, all filters are cleared.
    /// </summary>
    public virtual void Populate(IEnumerable<EventEntry> events, bool reset) { }

    /// <summary>Removes the filter but keeps any checkbox items populated.</summary>
    public abstract void Clear();

    /// <summary>Removes the filter AND resets any populated items.</summary>
    public virtual void Reset() => Clear();

    /// <summary>
    /// Raised when the filter state changes. The view attaches a handler that calls
    /// <c>AdvancedCollectionView.RefreshFilter()</c>.
    /// </summary>
    public event EventHandler? FilterUpdated;

    public virtual void Apply() => FireFilterUpdated(EventArgs.Empty);

    protected void FireFilterUpdated(EventArgs e) => FilterUpdated?.Invoke(this, e);

    /// <summary>
    /// Returns true if the row should be shown. Consumed by <c>AdvancedCollectionView.Filter</c>
    /// through the <see cref="Match"/> predicate wrapper.
    /// </summary>
    public abstract bool IsFilterMatched(EventEntry evt);

    /// <summary>
    /// Predicate form for composition — typed for <c>AdvancedCollectionView.Filter</c> (object → bool).
    /// </summary>
    public Predicate<object> Match => o => o is EventEntry e && IsFilterMatched(e);
}

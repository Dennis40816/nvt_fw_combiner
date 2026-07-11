using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Viewport rows that can swap as one collection reset instead of one layout pass per row.</summary>
public sealed class HexEditorViewportRowCollection : ObservableCollection<HexEditorViewportRowViewModel>
{
    /// <summary>Replaces the current viewport rows with one reset notification.</summary>
    public void ReplaceAll(IEnumerable<HexEditorViewportRowViewModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        CheckReentrancy();
        Items.Clear();
        foreach (HexEditorViewportRowViewModel row in rows)
        {
            Items.Add(row);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>Appends one progressively rendered page with a single collection notification.</summary>
    public void AppendAll(IEnumerable<HexEditorViewportRowViewModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        List<HexEditorViewportRowViewModel> additions = [.. rows];
        if (additions.Count == 0)
        {
            return;
        }

        CheckReentrancy();
        int startIndex = Items.Count;
        foreach (HexEditorViewportRowViewModel row in additions)
        {
            Items.Add(row);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Add,
            additions,
            startIndex));
    }
}

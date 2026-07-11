using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One bounded, replaceable window of raw-BIN rows. The document scrollbar owns total extent.</summary>
public sealed class HexEditorViewportRowCollection : ObservableCollection<HexEditorViewportRowViewModel>
{
    /// <summary>Replaces the current bounded window with one collection reset notification.</summary>
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
}

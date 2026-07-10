using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Viewport rows that can swap their complete projection without one layout pass per row.</summary>
public sealed class GeneralReplaceHexViewportRowCollection
    : ObservableCollection<GeneralReplaceHexViewportRowViewModel>
{
    /// <summary>Replaces the visible viewport with one reset notification.</summary>
    public void ReplaceAll(IEnumerable<GeneralReplaceHexViewportRowViewModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        CheckReentrancy();
        Items.Clear();
        foreach (GeneralReplaceHexViewportRowViewModel row in rows)
        {
            Items.Add(row);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}

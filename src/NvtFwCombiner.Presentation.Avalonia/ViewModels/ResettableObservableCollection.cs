using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> whose bulk replacement raises exactly one
/// <see cref="NotifyCollectionChangedAction.Reset"/> instead of a Clear followed by one Add per element.
/// </summary>
internal sealed class ResettableObservableCollection<T> : ObservableCollection<T>
{
    /// <summary>
    /// Replaces every element with <paramref name="items"/>, keeping this collection's identity while
    /// raising exactly one Reset (plus Count and Item[] property changes) instead of per-item Add notifications.
    /// </summary>
    internal void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        CheckReentrancy();
        Items.Clear();
        foreach (T item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}

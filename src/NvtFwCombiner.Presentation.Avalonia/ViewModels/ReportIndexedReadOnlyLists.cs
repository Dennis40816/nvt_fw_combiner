using System.Collections;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Thread-safe indexed projection that creates each report row at most once.</summary>
internal sealed class MemoizedIndexedReadOnlyList<T> : IReadOnlyList<T>
    where T : class
{
    private readonly Func<int, T> _factory;
    private readonly Lazy<T>?[] _items;
    private int _materializedCount;

    internal MemoizedIndexedReadOnlyList(int count, Func<int, T> factory)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _items = new Lazy<T>?[count];
    }

    public int Count => _items.Length;

    internal int MaterializedCount => Volatile.Read(ref _materializedCount);

    internal bool HasMaterializedReference(int index, object? value)
    {
        if ((uint)index >= (uint)_items.Length)
        {
            return false;
        }

        Lazy<T>? item = Volatile.Read(ref _items[index]);
        return item is { IsValueCreated: true } && ReferenceEquals(item.Value, value);
    }

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_items.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            Lazy<T>? item = Volatile.Read(ref _items[index]);
            if (item is null)
            {
                int itemIndex = index;
                var candidate = new Lazy<T>(
                    () => CreateItem(_factory, itemIndex),
                    LazyThreadSafetyMode.ExecutionAndPublication);
                item = Interlocked.CompareExchange(ref _items[index], candidate, null) ?? candidate;
            }

            return item.Value;
        }
    }

    private T CreateItem(Func<int, T> factory, int index)
    {
        T item = factory(index) ?? throw new InvalidOperationException("A report row factory returned null.");
        _ = Interlocked.Increment(ref _materializedCount);
        return item;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>Indexed projection that creates non-retained rows for fixed-size UI windows.</summary>
internal sealed class FactoryReadOnlyList<T>(int count, Func<int, T> factory) : IReadOnlyList<T>
{
    private readonly Func<int, T> _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public int Count { get; } = count >= 0 ? count : throw new ArgumentOutOfRangeException(nameof(count));

    public T this[int index] => (uint)index < (uint)Count
        ? _factory(index)
        : throw new ArgumentOutOfRangeException(nameof(index));

    public IEnumerator<T> GetEnumerator()
    {
        for (int index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>Non-copying object view over a typed read-only list.</summary>
internal sealed class ObjectReadOnlyList<T>(IReadOnlyList<T> items) : IReadOnlyList<object>
{
    private readonly IReadOnlyList<T> _items = items ?? throw new ArgumentNullException(nameof(items));

    public int Count => _items.Count;

    public object this[int index] => _items[index]!;

    public IEnumerator<object> GetEnumerator()
    {
        for (int index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>Read-only ordered view over selected indices from a shared report row projection.</summary>
internal sealed class IndexedReadOnlyList<T> : IReadOnlyList<T>, IList
    where T : class
{
    private const string ReadOnlyMessage = "The indexed report projection is read-only.";
    private readonly IReadOnlyList<T> _source;
    private readonly int[] _indices;

    internal IndexedReadOnlyList(IReadOnlyList<T> source, IReadOnlyList<int> indices)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(indices);
        _source = source;
        _indices = [.. indices];
        if (_indices.Any(index => index < 0 || index >= source.Count))
        {
            throw new ArgumentOutOfRangeException(nameof(indices));
        }
    }

    public int Count => _indices.Length;

    public T this[int index] => _source[_indices[index]];

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException(ReadOnlyMessage);
    }

    bool IList.IsFixedSize => true;

    bool IList.IsReadOnly => true;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    int IList.Add(object? value)
    {
        throw new NotSupportedException(ReadOnlyMessage);
    }

    void IList.Clear()
    {
        throw new NotSupportedException(ReadOnlyMessage);
    }

    bool IList.Contains(object? value)
    {
        return ((IList)this).IndexOf(value) >= 0;
    }

    int IList.IndexOf(object? value)
    {
        for (int index = 0; index < _indices.Length; index++)
        {
            int sourceIndex = _indices[index];
            bool matches = _source is MemoizedIndexedReadOnlyList<T> memoized
                ? memoized.HasMaterializedReference(sourceIndex, value)
                : Equals(_source[sourceIndex], value);
            if (matches)
            {
                return index;
            }
        }

        return -1;
    }

    void IList.Insert(int index, object? value)
    {
        throw new NotSupportedException(ReadOnlyMessage);
    }

    void IList.Remove(object? value)
    {
        throw new NotSupportedException(ReadOnlyMessage);
    }

    void IList.RemoveAt(int index)
    {
        throw new NotSupportedException(ReadOnlyMessage);
    }

    void ICollection.CopyTo(Array array, int index)
    {
        ArgumentNullException.ThrowIfNull(array);
        for (int sourceIndex = 0; sourceIndex < Count; sourceIndex++)
        {
            array.SetValue(this[sourceIndex], checked(index + sourceIndex));
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

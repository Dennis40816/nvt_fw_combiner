using System.Collections;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Thread-safe indexed projection that creates each report row at most once.</summary>
internal sealed class MemoizedIndexedReadOnlyList<T> : IReadOnlyList<T>
    where T : class
{
    private readonly Lazy<T>[] _items;
    private int _materializedCount;

    internal MemoizedIndexedReadOnlyList(int count, Func<int, T> factory)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentNullException.ThrowIfNull(factory);
        _items = new Lazy<T>[count];
        for (int index = 0; index < count; index++)
        {
            int itemIndex = index;
            _items[index] = new Lazy<T>(
                () => CreateItem(factory, itemIndex),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public int Count => _items.Length;

    internal int MaterializedCount => Volatile.Read(ref _materializedCount);

    public T this[int index] => (uint)index < (uint)_items.Length
        ? _items[index].Value
        : throw new ArgumentOutOfRangeException(nameof(index));

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
internal sealed class IndexedReadOnlyList<T> : IReadOnlyList<T>
{
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

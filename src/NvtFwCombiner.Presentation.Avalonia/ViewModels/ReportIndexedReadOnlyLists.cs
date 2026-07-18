using System.Collections;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed class MemoizedIndexedReadOnlyList<T>(int count, Func<int, T> factory) : IReadOnlyList<T>
    where T : class
{
    private readonly Func<int, T> _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    private readonly T?[] _items = new T?[count >= 0 ? count : throw new ArgumentOutOfRangeException(nameof(count))];
    private int _materializedCount;

    public int Count => _items.Length;

    internal int MaterializedCount => Volatile.Read(ref _materializedCount);

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_items.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            T? item = Volatile.Read(ref _items[index]);
            if (item is not null)
            {
                return item;
            }

            T created = _factory(index) ?? throw new InvalidOperationException("A report row factory returned null.");
            T? existing = Interlocked.CompareExchange(ref _items[index], created, null);
            if (existing is null)
            {
                _ = Interlocked.Increment(ref _materializedCount);
                return created;
            }

            return existing;
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

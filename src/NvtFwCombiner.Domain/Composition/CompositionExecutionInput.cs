namespace NvtFwCombiner.Domain.Composition;

/// <summary>Provides caller-owned address-space bytes for one execution request.</summary>
public sealed class CompositionExecutionInput
{
    private readonly Dictionary<string, byte[]> _addressSpaceBytes;

    /// <summary>Creates an execution input by cloning every supplied buffer.</summary>
    public CompositionExecutionInput(IReadOnlyDictionary<string, byte[]> addressSpaceBytes)
    {
        ArgumentNullException.ThrowIfNull(addressSpaceBytes);

        _addressSpaceBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, byte[]> item in addressSpaceBytes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(item.Key);
            ArgumentNullException.ThrowIfNull(item.Value);
            _addressSpaceBytes.Add(item.Key, [.. item.Value]);
        }

        string[] addressSpaceIds = [.. _addressSpaceBytes.Keys];
        Array.Sort(addressSpaceIds, StringComparer.Ordinal);
        AddressSpaceIds = Array.AsReadOnly(addressSpaceIds);
    }

    internal IReadOnlyList<string> AddressSpaceIds { get; }

    internal bool TryGetBytes(string addressSpaceId, out ReadOnlyMemory<byte> bytes)
    {
        if (_addressSpaceBytes.TryGetValue(addressSpaceId, out byte[]? buffer))
        {
            bytes = buffer;
            return true;
        }

        bytes = ReadOnlyMemory<byte>.Empty;
        return false;
    }
}

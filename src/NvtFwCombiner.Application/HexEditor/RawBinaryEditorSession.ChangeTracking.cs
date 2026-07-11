namespace NvtFwCombiner.Application.HexEditor;

public sealed partial class RawBinaryEditorSession
{
    /// <summary>Returns contiguous changed blocks and their current value/structural causes.</summary>
    public IReadOnlyList<RawBinaryEditorChangedRange> GetChangedRanges()
    {
        if (!TryRequireDocument(out _))
        {
            return [];
        }

        byte[] original = _original!;
        List<int?> originalOffsets = _originalOffsets!;
        List<byte> working = _working!;
        List<RawBinaryEditorValueChange> valueChanges = GetValueChanges(original, originalOffsets, working);
        List<RawBinaryEditorStructuralChange> structuralChanges = GetStructuralChanges(original, originalOffsets, working);
        var boundaries = new List<(int Start, int EndExclusive, RawBinaryEditorChangeKind Kind)>();
        int? rangeStart = null;
        RawBinaryEditorChangeKind rangeKind = RawBinaryEditorChangeKind.None;

        for (int index = 0; index < working.Count; index++)
        {
            RawBinaryEditorChangeKind changeKind = GetChangeKind(index, originalOffsets[index], working[index], original);
            if (changeKind != RawBinaryEditorChangeKind.None && rangeStart is null)
            {
                rangeStart = index;
                rangeKind = changeKind;
            }
            else if (changeKind != RawBinaryEditorChangeKind.None)
            {
                rangeKind |= changeKind;
            }
            else if (rangeStart is int start)
            {
                boundaries.Add((start, index, rangeKind));
                rangeStart = null;
                rangeKind = RawBinaryEditorChangeKind.None;
            }
        }

        if (rangeStart is int finalStart)
        {
            boundaries.Add((finalStart, working.Count, rangeKind));
        }

        if (original.Length > working.Count)
        {
            if (boundaries.Count > 0 && boundaries[^1].EndExclusive == working.Count)
            {
                (int start, _, RawBinaryEditorChangeKind kind) = boundaries[^1];
                boundaries[^1] = (start, original.Length, kind | RawBinaryEditorChangeKind.Structural);
            }
            else
            {
                boundaries.Add((working.Count, original.Length, RawBinaryEditorChangeKind.Structural));
            }
        }

        return [.. boundaries.Select(boundary => new RawBinaryEditorChangedRange(
            boundary.Start,
            boundary.EndExclusive,
            boundary.Kind,
            [.. valueChanges.Where(change => change.Start < boundary.EndExclusive && change.EndExclusive > boundary.Start)],
            [.. structuralChanges.Where(change => change.Address >= boundary.Start && change.Address < boundary.EndExclusive)]))];
    }

    private static RawBinaryEditorChangeKind GetChangeKind(
        int displayAddress,
        int? originalAddress,
        byte currentValue,
        byte[] originalDocument)
    {
        RawBinaryEditorChangeKind result = RawBinaryEditorChangeKind.None;
        if (originalAddress is int sourceAddress && originalDocument[sourceAddress] != currentValue)
        {
            result |= RawBinaryEditorChangeKind.Data;
        }

        if (originalAddress != displayAddress)
        {
            result |= RawBinaryEditorChangeKind.Structural;
        }

        return result;
    }

    private static List<RawBinaryEditorValueChange> GetValueChanges(
        byte[] original,
        List<int?> originalOffsets,
        List<byte> working)
    {
        var result = new List<RawBinaryEditorValueChange>();
        int? runStart = null;
        byte firstOriginal = 0;
        byte firstCurrent = 0;
        for (int index = 0; index < working.Count; index++)
        {
            bool changed = originalOffsets[index] is int sourceAddress && original[sourceAddress] != working[index];
            if (changed && runStart is null)
            {
                runStart = index;
                firstOriginal = original[originalOffsets[index]!.Value];
                firstCurrent = working[index];
            }
            else if (!changed && runStart is int start)
            {
                result.Add(new RawBinaryEditorValueChange(start, index, firstOriginal, firstCurrent));
                runStart = null;
            }
        }

        if (runStart is int finalStart)
        {
            result.Add(new RawBinaryEditorValueChange(finalStart, working.Count, firstOriginal, firstCurrent));
        }

        return result;
    }

    private static List<RawBinaryEditorStructuralChange> GetStructuralChanges(
        byte[] original,
        List<int?> originalOffsets,
        List<byte> working)
    {
        var result = new List<RawBinaryEditorStructuralChange>();
        int previousSourceAddress = -1;
        int index = 0;
        while (index < originalOffsets.Count)
        {
            if (originalOffsets[index] is null)
            {
                int insertedAt = index;
                while (index < originalOffsets.Count && originalOffsets[index] is null)
                {
                    index++;
                }

                result.Add(new RawBinaryEditorStructuralChange(
                    RawBinaryEditorStructuralChangeKind.Insert,
                    insertedAt,
                    index - insertedAt));
                continue;
            }

            int sourceAddress = originalOffsets[index]!.Value;
            int expectedSourceAddress = previousSourceAddress + 1;
            if (sourceAddress > expectedSourceAddress)
            {
                result.Add(new RawBinaryEditorStructuralChange(
                    RawBinaryEditorStructuralChangeKind.Delete,
                    index,
                    sourceAddress - expectedSourceAddress));
            }

            previousSourceAddress = sourceAddress;
            index++;
        }

        int trailingDeletedCount = original.Length - (previousSourceAddress + 1);
        if (trailingDeletedCount > 0)
        {
            result.Add(new RawBinaryEditorStructuralChange(
                RawBinaryEditorStructuralChangeKind.Delete,
                working.Count,
                trailingDeletedCount));
        }

        return result;
    }
}

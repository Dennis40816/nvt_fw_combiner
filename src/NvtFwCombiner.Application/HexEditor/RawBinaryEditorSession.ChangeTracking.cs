using System.Collections.ObjectModel;

namespace NvtFwCombiner.Application.HexEditor;

public sealed partial class RawBinaryEditorSession
{
    private static readonly ReadOnlyCollection<RawBinaryEditorStructuralChange> NoStructuralChanges =
        Array.AsReadOnly<RawBinaryEditorStructuralChange>([]);

    /// <summary>Returns contiguous changed blocks and their current value/structural causes.</summary>
    public IReadOnlyList<RawBinaryEditorChangedRange> GetChangedRanges()
    {
        if (!TryRequireDocument(out _))
        {
            return [];
        }

        if (!_changedRangesDirty)
        {
            return _cachedChangedRanges;
        }

        _cachedChangedRanges = BuildChangedRanges();
        _changedRangesDirty = false;
        return _cachedChangedRanges;
    }

    private ReadOnlyCollection<RawBinaryEditorChangedRange> BuildChangedRanges()
    {
        byte[] original = _original!;
        List<int> originalOffsets = _originalOffsets!;
        List<byte> working = _working!;
        List<RawBinaryEditorValueChange> valueChanges = GetValueChanges(
            original,
            originalOffsets,
            working,
            out bool hasIdentityOriginalOffsets);

        if (hasIdentityOriginalOffsets)
        {
            _hasIdentityOriginalOffsets = true;
            _identityChangedRanges = [.. valueChanges.Select(CreateIdentityChangedRange)];
            return CreateIdentityChangedRangeSnapshot(_identityChangedRanges);
        }

        _hasIdentityOriginalOffsets = false;
        _identityChangedRanges = [];
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

        var ranges = new RawBinaryEditorChangedRange[boundaries.Count];
        for (int index = 0; index < boundaries.Count; index++)
        {
            (int start, int endExclusive, RawBinaryEditorChangeKind kind) = boundaries[index];
            RawBinaryEditorValueChange[] rangeValueChanges = [.. valueChanges.Where(change =>
                change.Start < endExclusive && change.EndExclusive > start)];
            RawBinaryEditorStructuralChange[] rangeStructuralChanges = [.. structuralChanges.Where(change =>
                IsStructuralChangeInBoundary(change, start, endExclusive))];
            ranges[index] = new RawBinaryEditorChangedRange(
                start,
                endExclusive,
                kind,
                Array.AsReadOnly(rangeValueChanges),
                Array.AsReadOnly(rangeStructuralChanges));
        }

        return Array.AsReadOnly(ranges);
    }

    private void UpdateIdentityChangedRanges(int start, int length)
    {
        int endExclusive = checked(start + length);
        int firstAffected = _identityChangedRanges.FindIndex(range => range.EndExclusive >= start);
        if (firstAffected < 0)
        {
            firstAffected = _identityChangedRanges.Count;
        }

        int afterAffected = firstAffected;
        int scanStart = start;
        int scanEndExclusive = endExclusive;
        // Include every touching cached run so restoring bytes can split a run and new differences
        // can merge with either neighbor without inspecting the rest of the document.
        while (afterAffected < _identityChangedRanges.Count &&
               _identityChangedRanges[afterAffected].Start <= scanEndExclusive)
        {
            RawBinaryEditorChangedRange existing = _identityChangedRanges[afterAffected];
            scanStart = Math.Min(scanStart, checked((int)existing.Start));
            scanEndExclusive = Math.Max(scanEndExclusive, checked((int)existing.EndExclusive));
            afterAffected++;
        }

        var next = new List<RawBinaryEditorChangedRange>(
            _identityChangedRanges.Count - (afterAffected - firstAffected) + 2);
        for (int index = 0; index < firstAffected; index++)
        {
            next.Add(_identityChangedRanges[index]);
        }

        foreach (RawBinaryEditorValueChange change in GetIdentityValueChanges(scanStart, scanEndExclusive))
        {
            next.Add(CreateIdentityChangedRange(change));
        }

        for (int index = afterAffected; index < _identityChangedRanges.Count; index++)
        {
            next.Add(_identityChangedRanges[index]);
        }

        _identityChangedRanges = next;
        _cachedChangedRanges = CreateIdentityChangedRangeSnapshot(next);
        _changedRangesDirty = false;
    }

    private List<RawBinaryEditorValueChange> GetIdentityValueChanges(int start, int endExclusive)
    {
        byte[] original = _original!;
        List<byte> working = _working!;
        var result = new List<RawBinaryEditorValueChange>();
        int? runStart = null;
        byte firstOriginal = 0;
        byte firstCurrent = 0;
        for (int index = start; index < endExclusive; index++)
        {
            bool changed = original[index] != working[index];
            if (changed && runStart is null)
            {
                runStart = index;
                firstOriginal = original[index];
                firstCurrent = working[index];
            }
            else if (!changed && runStart is int valueChangeStart)
            {
                result.Add(new RawBinaryEditorValueChange(
                    valueChangeStart,
                    index,
                    firstOriginal,
                    firstCurrent));
                runStart = null;
            }
        }

        if (runStart is int finalStart)
        {
            result.Add(new RawBinaryEditorValueChange(
                finalStart,
                endExclusive,
                firstOriginal,
                firstCurrent));
        }

        return result;
    }

    private static RawBinaryEditorChangedRange CreateIdentityChangedRange(
        RawBinaryEditorValueChange change)
    {
        return new RawBinaryEditorChangedRange(
            change.Start,
            change.EndExclusive,
            RawBinaryEditorChangeKind.Data,
            Array.AsReadOnly([change]),
            NoStructuralChanges);
    }

    private static ReadOnlyCollection<RawBinaryEditorChangedRange> CreateIdentityChangedRangeSnapshot(
        List<RawBinaryEditorChangedRange> ranges)
    {
        return ranges.AsReadOnly();
    }

    private static bool IsStructuralChangeInBoundary(
        RawBinaryEditorStructuralChange change,
        int start,
        int endExclusive)
    {
        return change.Address >= start &&
               (change.Address < endExclusive ||
                (change.Kind == RawBinaryEditorStructuralChangeKind.Delete && change.Address == endExclusive));
    }

    private static RawBinaryEditorChangeKind GetChangeKind(
        int displayAddress,
        int originalAddress,
        byte currentValue,
        byte[] originalDocument)
    {
        RawBinaryEditorChangeKind result = RawBinaryEditorChangeKind.None;
        if (originalAddress != InsertedOriginalOffset && originalDocument[originalAddress] != currentValue)
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
        List<int> originalOffsets,
        List<byte> working,
        out bool hasIdentityOriginalOffsets)
    {
        var result = new List<RawBinaryEditorValueChange>();
        hasIdentityOriginalOffsets = original.Length == working.Count;
        int? runStart = null;
        byte firstOriginal = 0;
        byte firstCurrent = 0;
        for (int index = 0; index < working.Count; index++)
        {
            int sourceAddress = originalOffsets[index];
            hasIdentityOriginalOffsets &= sourceAddress == index;
            bool changed = sourceAddress != InsertedOriginalOffset && original[sourceAddress] != working[index];
            if (changed && runStart is null)
            {
                runStart = index;
                firstOriginal = original[sourceAddress];
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
        List<int> originalOffsets,
        List<byte> working)
    {
        var result = new List<RawBinaryEditorStructuralChange>();
        int previousSourceAddress = -1;
        int index = 0;
        while (index < originalOffsets.Count)
        {
            if (originalOffsets[index] == InsertedOriginalOffset)
            {
                int insertedAt = index;
                while (index < originalOffsets.Count && originalOffsets[index] == InsertedOriginalOffset)
                {
                    index++;
                }

                result.Add(new RawBinaryEditorStructuralChange(
                    RawBinaryEditorStructuralChangeKind.Insert,
                    insertedAt,
                    index - insertedAt));
                continue;
            }

            int sourceAddress = originalOffsets[index];
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

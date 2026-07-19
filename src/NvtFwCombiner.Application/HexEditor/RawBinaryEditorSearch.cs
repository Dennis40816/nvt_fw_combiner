using System.Text;

namespace NvtFwCombiner.Application.HexEditor;

/// <summary>Pure, bounded ASCII search over one immutable raw-BIN memory snapshot.</summary>
public static class RawBinaryEditorSearch
{
    /// <summary>Maximum match addresses retained for viewport highlighting.</summary>
    public const int MaximumRetainedMatches = 4096;

    /// <summary>
    /// Finds overlapping printable-ASCII occurrences while retaining a bounded highlight index.
    /// The selected result is always retained even when the complete match count exceeds the cap.
    /// </summary>
    public static RawBinaryEditorSearchResult Find(
        ReadOnlyMemory<byte> document,
        RawBinaryEditorState state,
        string text,
        long startOffset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        if (text.Length == 0 || text.Any(character => character is < (char)0x20 or > (char)0x7E))
        {
            return Failure(state, RawBinaryEditorIssueCode.InvalidAsciiText);
        }

        byte[] needle = Encoding.ASCII.GetBytes(text);
        ReadOnlySpan<byte> source = document.Span;
        if (needle.Length > source.Length)
        {
            return Failure(state, RawBinaryEditorIssueCode.AsciiTextNotFound);
        }

        bool startsAfterDocument = startOffset >= source.Length;
        int normalizedStart = startOffset is < 0 or > int.MaxValue || source.Length == 0 || startsAfterDocument
            ? 0
            : (int)startOffset;
        var retained = new List<long>(Math.Min(MaximumRetainedMatches, source.Length));
        long firstAddress = -1;
        long selectedAddress = -1;
        int selectedIndex = -1;
        int totalMatchCount = 0;
        int searchOffset = 0;

        while (searchOffset <= source.Length - needle.Length)
        {
            if ((totalMatchCount & 0xFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            int relative = source[searchOffset..].IndexOf(needle);
            if (relative < 0)
            {
                break;
            }

            int match = searchOffset + relative;
            firstAddress = firstAddress < 0 ? match : firstAddress;
            if (retained.Count < MaximumRetainedMatches)
            {
                retained.Add(match);
            }

            if (selectedAddress < 0 && match >= normalizedStart)
            {
                selectedAddress = match;
                selectedIndex = totalMatchCount;
            }

            totalMatchCount++;
            searchOffset = match + 1;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (totalMatchCount == 0)
        {
            return Failure(state, RawBinaryEditorIssueCode.AsciiTextNotFound);
        }

        bool wrapped = startsAfterDocument || selectedAddress < 0;
        if (wrapped)
        {
            selectedAddress = firstAddress;
            selectedIndex = 0;
        }

        if (!retained.Contains(selectedAddress))
        {
            retained[^1] = selectedAddress;
            retained.Sort();
        }

        return new RawBinaryEditorSearchResult(
            state,
            retained,
            selectedIndex,
            needle.Length,
            wrapped,
            totalMatchCount,
            selectedAddress,
            totalMatchCount > retained.Count);
    }

    /// <summary>
    /// Selects the occurrence at or after <paramref name="startOffset"/> from a canonical result
    /// anchored at its first occurrence. Returns false when the bounded retained index cannot
    /// answer authoritatively and the caller must run <see cref="Find"/> again.
    /// </summary>
    public static bool TrySelectFromAnchoredResult(
        RawBinaryEditorSearchResult anchoredResult,
        long startOffset,
        out RawBinaryEditorSearchResult selectedResult)
    {
        ArgumentNullException.ThrowIfNull(anchoredResult);
        selectedResult = null!;
        if (!anchoredResult.Succeeded)
        {
            selectedResult = anchoredResult;
            return true;
        }

        if (anchoredResult.MatchIndex != 0 ||
            anchoredResult.Matches.Count == 0 ||
            anchoredResult.Matches[0] != anchoredResult.SelectedAddress)
        {
            return false;
        }

        bool startsAfterDocument = startOffset >= anchoredResult.State.WorkingLength;
        long normalizedStart = startOffset < 0 || startsAfterDocument ? 0 : startOffset;
        int matchIndex = FindFirstMatchAtOrAfter(anchoredResult.Matches, normalizedStart);
        bool wrapped = startsAfterDocument;
        if (matchIndex < 0)
        {
            if (anchoredResult.IsTruncated)
            {
                return false;
            }

            matchIndex = 0;
            wrapped = true;
        }

        selectedResult = anchoredResult with
        {
            MatchIndex = matchIndex,
            Wrapped = wrapped,
            SelectedAddress = anchoredResult.Matches[matchIndex],
        };
        return true;
    }

    private static int FindFirstMatchAtOrAfter(IReadOnlyList<long> matches, long address)
    {
        int low = 0;
        int high = matches.Count;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (matches[middle] < address)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low < matches.Count ? low : -1;
    }

    private static RawBinaryEditorSearchResult Failure(
        RawBinaryEditorState state,
        RawBinaryEditorIssueCode issueCode)
    {
        return new RawBinaryEditorSearchResult(
            state,
            [],
            Issue: new RawBinaryEditorIssue(issueCode));
    }
}

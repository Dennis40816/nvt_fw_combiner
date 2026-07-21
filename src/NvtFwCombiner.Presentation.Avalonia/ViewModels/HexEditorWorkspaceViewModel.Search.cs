using System.Globalization;
using NvtFwCombiner.Application.HexEditor;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class HexEditorWorkspaceViewModel
{
    private IReadOnlyList<long> _asciiMatches = [];
    private int _asciiMatchIndex = -1;
    private int _asciiMatchCount;
    private int _asciiSearchLength;

    /// <summary>Compact current-result indicator for a repeated ASCII search.</summary>
    public string AsciiSearchResultLabel => _asciiMatchIndex >= 0
        ? string.Format(CultureInfo.InvariantCulture, Text.HexEditorAsciiSearchIndexTemplate, _asciiMatchIndex + 1, _asciiMatchCount)
        : string.Empty;

    /// <summary>True after a valid search has at least one result to expose in the toolbar.</summary>
    public bool HasAsciiSearchResults => _asciiMatchIndex >= 0;

    private bool CanFindAscii()
    {
        return HasDocument && !string.IsNullOrWhiteSpace(AsciiSearchText);
    }

    private async Task FindAsciiAsync(CancellationToken cancellationToken)
    {
        if (!CanFindAscii())
        {
            return;
        }

        long startOffset = 0;
        if (TryParseAddressLabel(SelectedByteAddress ?? string.Empty, out long selectedAddress) &&
            selectedAddress < _state.WorkingLength - 1)
        {
            startOffset = selectedAddress + 1;
        }

        string searchText = AsciiSearchText;
        RawBinaryEditorSearchResult result;
        try
        {
            result = await _findAsciiAsync(searchText, startOffset, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested ||
            !string.Equals(searchText, AsciiSearchText, StringComparison.Ordinal))
        {
            return;
        }

        if (!result.Succeeded)
        {
            EditorStatus = DescribeIssue(result.Issue!);
            return;
        }

        UpdateState(result.State);
        _asciiMatches = result.Matches;
        _asciiMatchIndex = result.MatchIndex;
        _asciiMatchCount = result.TotalMatchCount;
        _asciiSearchLength = result.Length;
        OnPropertyChanged(nameof(AsciiSearchResultLabel));
        OnPropertyChanged(nameof(HasAsciiSearchResults));
        int row = checked((int)(result.Address / BytesPerRow));
        SetViewportStartRow(Math.Max(0, row - 4));
        RefreshViewportRows();
        string startAddress = FormatAddress(result.Address);
        string endAddress = FormatAddress(result.Address + result.Length - 1L);
        ViewportAddress = startAddress;
        RangeStartAddress = startAddress;
        RangeEndAddress = endAddress;
        UpdateSelection(startAddress);
        EditorStatus = string.Format(
            CultureInfo.InvariantCulture,
            Text.HexEditorAsciiSearchFoundDetail,
            result.Wrapped ? Text.HexEditorAsciiSearchWrappedLabel : string.Empty,
            result.MatchIndex + 1,
            result.TotalMatchCount,
            startAddress);
    }

    private bool IsAsciiSearchMatch(long address)
    {
        if (_asciiSearchLength == 0 || _asciiMatches.Count == 0)
        {
            return false;
        }

        long earliestStart = address - _asciiSearchLength + 1L;
        int lower = 0;
        int upper = _asciiMatches.Count;
        while (lower < upper)
        {
            int middle = lower + ((upper - lower) / 2);
            if (_asciiMatches[middle] < earliestStart)
            {
                lower = middle + 1;
            }
            else
            {
                upper = middle;
            }
        }

        return lower < _asciiMatches.Count && _asciiMatches[lower] <= address;
    }

    private void ClearAsciiSearchResults(bool refreshViewport)
    {
        bool hadResults = _asciiMatches.Count > 0 || _asciiMatchIndex >= 0;
        _asciiMatches = [];
        _asciiMatchIndex = -1;
        _asciiMatchCount = 0;
        _asciiSearchLength = 0;
        OnPropertyChanged(nameof(AsciiSearchResultLabel));
        OnPropertyChanged(nameof(HasAsciiSearchResults));
        if (hadResults && refreshViewport && HasDocument)
        {
            RefreshViewportRows();
        }
    }
}

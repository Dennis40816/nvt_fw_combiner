// Copyright (c) 2026 Dennis Liu. All rights reserved.
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal enum RecalculationConfirmChoice
{
    Recalculate,
    WriteWithoutRecalculation,
}

internal enum RecalculationConfirmOutcome
{
    RecalculateAndOutput,
    WriteWithoutRecalculation,
    Cancel,
}

/// <summary>One dialog opening. All firmware facts are supplied by the host, in output-image coordinates.</summary>
internal sealed class RecalculationConfirmModel : ObservableObject
{
    private readonly Action<RecalculationConfirmOutcome> _reportOutcome;
    private bool _isExpanded;

    public RecalculationConfirmModel(string icName, bool canRecalculate,
        IEnumerable<long> crcStorageOffsets, IEnumerable<ByteRange>? crcCoverageRanges,
        IEnumerable<RecalculationConfirmSegment> coveringSegments, string nfcVersion, string legacyVersion,
        string commandModeName, string? crcAlgorithmName, Action<RecalculationConfirmOutcome> reportOutcome,
        RecalculationConfirmChoice preselectedChoice = RecalculationConfirmChoice.Recalculate,
        ShellLanguage language = ShellLanguage.English)
    {
        ArgumentNullException.ThrowIfNull(crcStorageOffsets);
        ArgumentNullException.ThrowIfNull(coveringSegments);
        ArgumentNullException.ThrowIfNull(reportOutcome);
        if (!Enum.IsDefined(preselectedChoice)) { throw new ArgumentOutOfRangeException(nameof(preselectedChoice)); }
        IcName = icName;
        CanRecalculate = canRecalculate;
        CrcStorageOffsets = Array.AsReadOnly(crcStorageOffsets.ToArray());
        CrcCoverageRanges = Array.AsReadOnly(crcCoverageRanges?.ToArray() ?? []);
        NfcVersion = nfcVersion;
        LegacyVersion = legacyVersion;
        CommandModeName = commandModeName;
        CrcAlgorithmName = crcAlgorithmName;
        PreselectedChoice = preselectedChoice;
        Text = new(language);
        CoveringSegments = Array.AsReadOnly(coveringSegments.Select(segment =>
            new RecalculationConfirmSegment(segment.Range, segment.CrcOffsets, Text)).ToArray());
        _reportOutcome = reportOutcome;
        RecalculateCommand = new RelayCommand(() => Complete(RecalculationConfirmOutcome.RecalculateAndOutput),
            () => IsPending && CanRecalculate);
        WriteCommand = new RelayCommand(
            () => Complete(RecalculationConfirmOutcome.WriteWithoutRecalculation),
            () => IsPending);
        CancelCommand = new RelayCommand(
            () => Complete(RecalculationConfirmOutcome.Cancel),
            () => IsPending);
        ToggleDetailsCommand = new RelayCommand(ToggleDetails, () => IsPending && CanRecalculate);
    }

    public string IcName { get; }
    public bool CanRecalculate { get; }
    public IReadOnlyList<long> CrcStorageOffsets { get; }
    public IReadOnlyList<ByteRange> CrcCoverageRanges { get; }
    public IReadOnlyList<RecalculationConfirmSegment> CoveringSegments { get; }
    public string NfcVersion { get; }
    public string LegacyVersion { get; }
    public string CommandModeName { get; }
    public string? CrcAlgorithmName { get; }
    public RecalculationConfirmChoice PreselectedChoice { get; }
    public RecalculationConfirmTextResources Text { get; }
    public IRelayCommand RecalculateCommand { get; }
    public IRelayCommand WriteCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand ToggleDetailsCommand { get; }
    // UI thread only. The nullable outcome is the single terminal-state authority.
    public RecalculationConfirmOutcome? Outcome { get; private set; }
    public bool IsPending => Outcome is null;
    public bool IsExpanded => _isExpanded;
    public bool HasCoveringSegments => CoveringSegments.Count > 0;
    public bool HasDeclaredCoverage => CrcCoverageRanges.Count > 0;
    public bool HasDeclaredStorage => CrcStorageOffsets.Count > 0;
    public string Title => CanRecalculate ? Text.Title : Text.NoRuleTitle;
    public string StorageText => HasDeclaredStorage
        ? FormatOffsets(CrcStorageOffsets, Text.ListSeparator) : Text.NotDeclared;
    public string CoverageText => HasDeclaredCoverage
        ? string.Join(Text.ListSeparator, CrcCoverageRanges.Select(FormatRange)) : Text.UnknownCoverage;
    public string ModeText => CrcAlgorithmName is null ? CommandModeName : $"{CommandModeName} · {CrcAlgorithmName}";
    public string WarningText => string.Join(Environment.NewLine, CoveringSegments.Select(segment => segment.WarningText));

    private void ToggleDetails()
    {
        if (!IsPending || !CanRecalculate) { return; }
        _ = SetProperty(ref _isExpanded, !_isExpanded, nameof(IsExpanded));
    }

    private void Complete(RecalculationConfirmOutcome outcome)
    {
        if (!IsPending || (outcome == RecalculationConfirmOutcome.RecalculateAndOutput && !CanRecalculate)) { return; }
        Outcome = outcome;
        OnPropertyChanged(nameof(Outcome));
        OnPropertyChanged(nameof(IsPending));
        RecalculateCommand.NotifyCanExecuteChanged();
        WriteCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        ToggleDetailsCommand.NotifyCanExecuteChanged();
        _reportOutcome(outcome);
    }

    internal static string FormatOffsets(IEnumerable<long> offsets, string separator)
    {
        return string.Join(separator, offsets.Select(static offset => FormattableString.Invariant($"0x{offset:X}")));
    }

    internal static string FormatRange(ByteRange range)
    {
        return FormattableString.Invariant($"[0x{range.Start:X5}, 0x{range.EndExclusive:X5})");
    }
}

/// <summary>A segment and the CRC offsets it covers, already determined by the host; no intersection is computed.</summary>
internal sealed class RecalculationConfirmSegment
{
    private readonly RecalculationConfirmTextResources _text;

    public RecalculationConfirmSegment(ByteRange range, IEnumerable<long> crcOffsets)
        : this(range, crcOffsets, new RecalculationConfirmTextResources(ShellLanguage.English))
    {
    }

    internal RecalculationConfirmSegment(ByteRange range, IEnumerable<long> crcOffsets, RecalculationConfirmTextResources text)
    {
        ArgumentNullException.ThrowIfNull(crcOffsets);
        Range = range;
        CrcOffsets = Array.AsReadOnly(crcOffsets.ToArray());
        _text = text;
    }

    public ByteRange Range { get; }
    public IReadOnlyList<long> CrcOffsets { get; }
    public string RangeText => RecalculationConfirmModel.FormatRange(Range);
    public string OffsetsText => RecalculationConfirmModel.FormatOffsets(CrcOffsets, _text.ListSeparator);
    public string Prefix => _text.SegmentPrefix;
    public string Middle => _text.SegmentMiddle(CrcOffsets.Count);
    public string Suffix => _text.SegmentSuffix(CrcOffsets.Count);
    public string WarningText => $"{Prefix}{RangeText}{Middle}{OffsetsText}{Suffix}";
}

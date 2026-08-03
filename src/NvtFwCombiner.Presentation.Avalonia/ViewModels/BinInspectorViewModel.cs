using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Read-only semantic structure and field navigation for resolved BIN metadata.</summary>
public sealed partial class BinInspectorViewModel : ObservableObject
{
    private readonly BinInspectorStructureSource[] _structures;
    private readonly RelayCommand<BinInspectorStructureSource> _selectStructureCommand;
    private readonly RelayCommand<FormattedMetadataField> _selectFieldCommand;
    private readonly RelayCommand<HexViewportInteractionIntent> _viewportInteractionCommand;
    private int _rangeScrollRow;
    private long? _selectedAddress;

    /// <summary>Creates a closed inspector without IC, filename, or profile inference inputs.</summary>
    public BinInspectorViewModel(IEnumerable<BinInspectorStructureSource> structures)
    {
        ArgumentNullException.ThrowIfNull(structures);
        _structures = [.. structures];
        if (_structures.Length == 0 ||
            _structures.Any(static source => source is null) ||
            _structures.Select(static source => source.Metadata.BindingId)
                .Distinct(StringComparer.Ordinal).Count() != _structures.Length)
        {
            throw new ArgumentException(
                "BIN inspection requires one or more uniquely bound resolved structures.",
                nameof(structures));
        }

        Structures = Array.AsReadOnly(_structures);
        _selectStructureCommand = new RelayCommand<BinInspectorStructureSource>(
            SelectStructure,
            CanSelectStructure);
        _selectFieldCommand = new RelayCommand<FormattedMetadataField>(SelectField, CanSelectField);
        _viewportInteractionCommand = new RelayCommand<HexViewportInteractionIntent>(HandleViewportIntent);
        SelectedStructure = _structures[0];
        SelectedField = SelectedStructure.Metadata.Fields.Count > 0
            ? SelectedStructure.Metadata.Fields[0]
            : null;
        _selectedAddress = SelectedField?.AddressedRange.Range.Start ??
            SelectedStructure.Metadata.AddressedRange!.Range.Start;
        RangeScrollMaximum = CalculateRangeScrollMaximum(SelectedStructure);
        ViewportSnapshot = BinInspectorViewportAdapter.Create(
            SelectedStructure,
            firstStructureRow: 0,
            _selectedAddress);
    }

    /// <summary>Resolved structures in canonical Application metadata-plan order.</summary>
    public IReadOnlyList<BinInspectorStructureSource> Structures { get; }

    /// <summary>The exact resolved structure controlling the current viewport.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fields))]
    public partial BinInspectorStructureSource SelectedStructure { get; private set; }

    /// <summary>Application-formatted fields for the selected structure.</summary>
    public IReadOnlyList<FormattedMetadataField> Fields => SelectedStructure.Metadata.Fields;

    /// <summary>The semantic field currently synchronized with the byte selection.</summary>
    [ObservableProperty]
    public partial FormattedMetadataField? SelectedField { get; private set; }

    /// <summary>First structure-local row currently materialized.</summary>
    public int RangeScrollRow
    {
        get => _rangeScrollRow;
        set
        {
            int next = Math.Clamp(value, 0, RangeScrollMaximum);
            if (_rangeScrollRow == next)
            {
                return;
            }

            _rangeScrollRow = next;
            OnPropertyChanged();
            PublishViewport();
        }
    }

    /// <summary>Last admitted range-local start row.</summary>
    public int RangeScrollMaximum { get; private set; }

    /// <summary>Selects one already-resolved structure; it does not resolve or infer firmware.</summary>
    public IRelayCommand<BinInspectorStructureSource> SelectStructureCommand => _selectStructureCommand;

    /// <summary>Selects one Application-owned field and reveals its exact absolute range.</summary>
    public IRelayCommand<FormattedMetadataField> SelectFieldCommand => _selectFieldCommand;

    /// <summary>Immutable input consumed by the shared #191 renderer.</summary>
    internal HexViewportSnapshot ViewportSnapshot { get; private set; }

    /// <summary>Receives source-neutral selection and range-scroll intents from the shared renderer.</summary>
    internal IRelayCommand<HexViewportInteractionIntent> ViewportInteractionCommand =>
        _viewportInteractionCommand;

    internal void HandleViewportIntent(HexViewportInteractionIntent intent)
    {
        switch (intent.Trigger)
        {
            case HexViewportInteractionTrigger.Scroll:
                RangeScrollRow = checked(RangeScrollRow + intent.Delta);
                break;
            case HexViewportInteractionTrigger.Select when intent.Address is long selected:
                SelectAddress(selected, ensureVisible: false);
                break;
            case HexViewportInteractionTrigger.MoveSelection:
                SelectAddress(
                    checked((_selectedAddress ?? CurrentStart) + intent.Delta),
                    ensureVisible: true);
                break;
            case HexViewportInteractionTrigger.Select:
            case HexViewportInteractionTrigger.Activate:
            case HexViewportInteractionTrigger.Context:
            case HexViewportInteractionTrigger.StructuralContext:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(intent), intent.Trigger, null);
        }
    }

    private long CurrentStart => SelectedStructure.Metadata.AddressedRange!.Range.Start;

    private long CurrentEndExclusive =>
        SelectedStructure.Metadata.AddressedRange!.Range.EndExclusive;

    private bool CanSelectStructure(BinInspectorStructureSource? source)
    {
        return source is not null && _structures.Any(candidate => ReferenceEquals(candidate, source));
    }

    private void SelectStructure(BinInspectorStructureSource? source)
    {
        if (!CanSelectStructure(source) || ReferenceEquals(source, SelectedStructure))
        {
            return;
        }

        SelectedStructure = source!;
        SelectedField = SelectedStructure.Metadata.Fields.Count > 0
            ? SelectedStructure.Metadata.Fields[0]
            : null;
        _rangeScrollRow = 0;
        _selectedAddress = SelectedField?.AddressedRange.Range.Start ?? CurrentStart;
        RangeScrollMaximum = CalculateRangeScrollMaximum(SelectedStructure);
        OnPropertyChanged(nameof(RangeScrollRow));
        OnPropertyChanged(nameof(RangeScrollMaximum));
        PublishViewport();
    }

    private bool CanSelectField(FormattedMetadataField? field)
    {
        return field is not null && Fields.Any(candidate => ReferenceEquals(candidate, field));
    }

    private void SelectField(FormattedMetadataField? field)
    {
        if (!CanSelectField(field))
        {
            return;
        }

        SelectedField = field;
        SelectAddress(field!.AddressedRange.Range.Start, ensureVisible: true);
    }

    private void SelectAddress(long address, bool ensureVisible)
    {
        long selected = Math.Clamp(address, CurrentStart, CurrentEndExclusive - 1);
        _selectedAddress = selected;
        if (ensureVisible)
        {
            int targetRow = checked((int)((selected - CurrentStart) /
                HexViewportSnapshot.BytesPerRow));
            if (targetRow < RangeScrollRow)
            {
                RangeScrollRow = targetRow;
                return;
            }

            int lastVisibleRow = RangeScrollRow +
                HexViewportCapabilityProfile.BinInspector.InitialRows - 1;
            if (targetRow > lastVisibleRow)
            {
                RangeScrollRow = targetRow -
                    HexViewportCapabilityProfile.BinInspector.InitialRows + 1;
                return;
            }
        }

        ViewportSnapshot = ViewportSnapshot.WithSelectedAddress(selected);
        OnPropertyChanged(nameof(ViewportSnapshot));
    }

    private void PublishViewport()
    {
        ViewportSnapshot = BinInspectorViewportAdapter.Create(
            SelectedStructure,
            RangeScrollRow,
            _selectedAddress);
        OnPropertyChanged(nameof(ViewportSnapshot));
    }

    private static int CalculateRangeScrollMaximum(BinInspectorStructureSource source)
    {
        int totalRows = checked((source.Bytes.Length + HexViewportSnapshot.BytesPerRow - 1) /
            HexViewportSnapshot.BytesPerRow);
        return Math.Max(0, totalRows - HexViewportCapabilityProfile.BinInspector.InitialRows);
    }
}

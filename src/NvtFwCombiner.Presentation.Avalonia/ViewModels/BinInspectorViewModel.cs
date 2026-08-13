using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Read-only semantic structure and field navigation for resolved BIN metadata.</summary>
internal sealed partial class BinInspectorViewModel : ObservableObject
{
    private readonly RelayCommand<FirmwareBinInspectionStructure> _selectStructureCommand;
    private readonly RelayCommand<FormattedMetadataField> _selectFieldCommand;
    private readonly RelayCommand<HexViewportInteractionIntent> _viewportInteractionCommand;
    private bool _isAddressSelection;
    private bool _isStructureTransition;
    private long? _selectedAddress;

    /// <summary>Creates a closed inspector without IC, filename, or profile inference inputs.</summary>
    public BinInspectorViewModel(
        FirmwareBinInspectionSnapshot inspection,
        ShellLanguage language)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        Inspection = inspection;
        Text = ShellTextResources.For(language);
        Structures = Array.AsReadOnly([.. inspection.Structures]);
        _selectStructureCommand = new RelayCommand<FirmwareBinInspectionStructure>(
            SelectStructure,
            CanSelectStructure);
        _selectFieldCommand = new RelayCommand<FormattedMetadataField>(SelectField, CanSelectField);
        _viewportInteractionCommand = new RelayCommand<HexViewportInteractionIntent>(HandleViewportIntent);
        ViewportSnapshot = HexViewportSnapshot.Empty(
            HexViewportCapabilityProfile.BinInspector,
            Structures[0].Metadata.AddressedRange!.AddressSpaceId);
        SelectedStructure = Structures[0];
    }

    /// <summary>One formatter-rooted, revision-bound Application inspection snapshot.</summary>
    public FirmwareBinInspectionSnapshot Inspection { get; }

    public ShellTextResources Text { get; }

    public IReadOnlyList<FirmwareBinInspectionStructure> Structures { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fields))]
    public partial FirmwareBinInspectionStructure SelectedStructure { get; set; }

    public IReadOnlyList<FormattedMetadataField> Fields => SelectedStructure.Metadata.Fields;

    [ObservableProperty]
    public partial FormattedMetadataField? SelectedField { get; set; }

    /// <summary>Localized screen-reader projection for the selected custom-drawn byte.</summary>
    public string SelectedByteAccessibleLabel
    {
        get
        {
            if (_selectedAddress is not long selected || !TryGetVisibleCell(selected, out HexViewportCell cell))
            {
                return Text.BinInspectorNoByteSelectedLabel;
            }

            string address = FormattableString.Invariant($"0x{selected:X6}");
            string value = FormattableString.Invariant($"0x{cell.PrimaryValue:X2}");
            string fieldLabel = SelectedField is { } selectedField &&
                selectedField.AddressedRange.Range.Contains(selected)
                    ? selectedField.DisplayName
                    : Text.BinInspectorNoFieldLabel;
            return string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                Text.BinInspectorSelectedByteFormat,
                address,
                value,
                fieldLabel);
        }
    }

    /// <summary>First structure-local row currently materialized.</summary>
    public int RangeScrollRow
    {
        get;
        set
        {
            int next = Math.Clamp(value, 0, RangeScrollMaximum);
            if (field == next)
            {
                return;
            }

            field = next;
            OnPropertyChanged();
            if (!_isStructureTransition)
            {
                PublishViewport();
            }
        }
    }

    /// <summary>Last admitted range-local start row.</summary>
    public int RangeScrollMaximum { get; private set; }

    /// <summary>Selects one already-resolved structure; it does not resolve or infer firmware.</summary>
    public IRelayCommand<FirmwareBinInspectionStructure> SelectStructureCommand => _selectStructureCommand;

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

    private bool CanSelectStructure(FirmwareBinInspectionStructure? source)
    {
        return source is not null && Structures.Any(candidate => ReferenceEquals(candidate, source));
    }

    private void SelectStructure(FirmwareBinInspectionStructure? source)
    {
        if (!CanSelectStructure(source) || ReferenceEquals(source, SelectedStructure))
        {
            return;
        }

        SelectedStructure = source!;
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
    }

    private void SelectAddress(long address, bool ensureVisible)
    {
        long selected = Math.Clamp(address, CurrentStart, CurrentEndExclusive - 1);
        _selectedAddress = selected;
        FormattedMetadataField? containingField = SelectedField is { } currentField &&
            currentField.AddressedRange.Range.Contains(selected)
                ? currentField
                : Fields.FirstOrDefault(field => field.AddressedRange.Range.Contains(selected));
        if (!ReferenceEquals(containingField, SelectedField))
        {
            _isAddressSelection = true;
            try
            {
                SelectedField = containingField;
            }
            finally
            {
                _isAddressSelection = false;
            }
        }

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
        OnPropertyChanged(nameof(SelectedByteAccessibleLabel));
    }

    private void PublishViewport()
    {
        ViewportSnapshot = BinInspectorViewportAdapter.Create(
            SelectedStructure,
            RangeScrollRow,
            _selectedAddress);
        OnPropertyChanged(nameof(ViewportSnapshot));
        OnPropertyChanged(nameof(SelectedByteAccessibleLabel));
    }

    private bool TryGetVisibleCell(long address, out HexViewportCell cell)
    {
        foreach (HexViewportRow row in ViewportSnapshot.Rows)
        {
            long index = address - row.Address;
            if ((ulong)index < (ulong)row.Cells.Count)
            {
                cell = row.Cells[(int)index];
                return true;
            }
        }

        cell = default;
        return false;
    }

    private static int CalculateRangeScrollMaximum(FirmwareBinInspectionStructure source)
    {
        int totalRows = checked((source.Bytes.Length + HexViewportSnapshot.BytesPerRow - 1) /
            HexViewportSnapshot.BytesPerRow);
        return Math.Max(0, totalRows - HexViewportCapabilityProfile.BinInspector.InitialRows);
    }

    partial void OnSelectedStructureChanging(FirmwareBinInspectionStructure value)
    {
        if (!CanSelectStructure(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The selected metadata structure is not owned by this inspection.");
        }
    }

    partial void OnSelectedStructureChanged(FirmwareBinInspectionStructure value)
    {
        _isStructureTransition = true;
        try
        {
            _selectedAddress = value.Metadata.AddressedRange!.Range.Start;
            RangeScrollMaximum = CalculateRangeScrollMaximum(value);
            RangeScrollRow = 0;
        }
        finally
        {
            _isStructureTransition = false;
        }

        OnPropertyChanged(nameof(RangeScrollMaximum));
        PublishViewport();
        SelectedField = value.Metadata.Fields.Count > 0
            ? value.Metadata.Fields[0]
            : null;
    }

    partial void OnSelectedFieldChanging(FormattedMetadataField? value)
    {
        if (value is not null && !CanSelectField(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The selected metadata field is not owned by the current structure.");
        }
    }

    partial void OnSelectedFieldChanged(FormattedMetadataField? value)
    {
        if (value is not null && !_isAddressSelection)
        {
            SelectAddress(value.AddressedRange.Range.Start, ensureVisible: true);
        }

        OnPropertyChanged(nameof(SelectedByteAccessibleLabel));
    }
}

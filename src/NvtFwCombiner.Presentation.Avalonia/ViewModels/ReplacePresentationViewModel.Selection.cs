namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReplacePresentationViewModel
{
    /// <summary>True when the compact Replace input selection overview is open.</summary>
    public bool IsReplaceSelectionModalOpen { get; private set; }

    /// <summary>Gets compact selected replacement count text for the Replace title row.</summary>
    public string ReplaceSelectionCountLabel
    {
        get
        {
            int selected = GetSelectedReplacementCount();
            int targetCount = GetReplaceTargetCount();
            string noun = IsGeneralReplaceModeSelected
                ? (targetCount == 1 ? "mapping" : "mappings")
                : (targetCount == 1 ? "target" : "targets");
            return $"{selected} / {targetCount} {noun} selected";
        }
    }

    /// <summary>Gets the Replace selection modal subtitle.</summary>
    public string ReplaceSelectionSubtitle => $"{SelectedIc} / {SelectedNumber} / {SelectedReplaceMode}";

    /// <summary>Gets the Replace selection readiness label.</summary>
    public string ReplaceSelectionStatusLabel => CanRunReplace()
        ? "Ready for Build"
        : ReplaceReadinessStatus;

    /// <summary>Gets a concise explanation of how selection review feeds Build.</summary>
    public string ReplaceSelectionRunHint => CanRunReplace()
        ? "Build will validate selected inputs, ask for an output BIN path, then record the report details."
        : "Complete the required inputs before Build can validate the operation trace.";

    /// <summary>Gets selected replacement inputs visible even when groups are collapsed.</summary>
    public IReadOnlyList<ReportLineViewModel> ReplaceSelectionRows
    {
        get
        {
            List<ReportLineViewModel> rows = IsGeneralReplaceModeSelected
                ? CreateGeneralReplaceSelectionRows()
                : CreateStructuredReplaceSelectionRows();
            return rows.Count > 0
                ? rows
                :
                [
                    new ReportLineViewModel(
                        "No replacement inputs selected",
                        "Collapsed groups keep the base firmware unchanged until a replacement BIN is selected.",
                        SelectedReplaceMode),
                ];
        }
    }

    /// <summary>Gets required Replace inputs that are still missing before Build can run.</summary>
    public IReadOnlyList<ReportLineViewModel> ReplaceSelectionMissingRows => CreateReplaceSelectionMissingRows();

    /// <summary>True when the Replace selection overview has missing required inputs.</summary>
    public bool HasReplaceSelectionMissingRows => ReplaceSelectionMissingRows.Count > 0;

    private void ShowReplaceSelection()
    {
        IsReplaceSelectionModalOpen = true;
        OnPropertyChanged(nameof(IsReplaceSelectionModalOpen));
    }

    private void CloseReplaceSelection()
    {
        if (!IsReplaceSelectionModalOpen)
        {
            return;
        }

        IsReplaceSelectionModalOpen = false;
        OnPropertyChanged(nameof(IsReplaceSelectionModalOpen));
    }

    internal void CloseSelectionForRun()
    {
        CloseReplaceSelection();
    }

    internal void RefreshSelectionState()
    {
        OnPropertyChanged(nameof(ReplaceSelectionCountLabel));
        OnPropertyChanged(nameof(ReplaceSelectionSubtitle));
        OnPropertyChanged(nameof(ReplaceSelectionStatusLabel));
        OnPropertyChanged(nameof(ReplaceSelectionRunHint));
        OnPropertyChanged(nameof(ReplaceSelectionRows));
        OnPropertyChanged(nameof(ReplaceSelectionMissingRows));
        OnPropertyChanged(nameof(HasReplaceSelectionMissingRows));
        OnPropertyChanged(nameof(CanBuildReplace));
    }

    private int GetSelectedReplacementCount()
    {
        return IsGeneralReplaceModeSelected
            ? GeneralReplaceMappings.Count(mapping => mapping.HasSource)
            : ReplaceSlots.Count(IsSelectedReplacementSlot);
    }

    private int GetReplaceTargetCount()
    {
        return IsGeneralReplaceModeSelected
            ? GeneralReplaceMappings.Count
            : ReplaceSlots.Count(slot => !ReferenceEquals(slot, ReplaceBaseSlot));
    }

    private List<ReportLineViewModel> CreateStructuredReplaceSelectionRows()
    {
        return
        [
            .. ReplaceSlots
            .Where(IsSelectedReplacementSlot)
            .Select(slot => new ReportLineViewModel(
                slot.Title,
                slot.DisplayName,
                slot.Description)),
        ];
    }

    private List<ReportLineViewModel> CreateGeneralReplaceSelectionRows()
    {
        return
        [
            .. GeneralReplaceMappings
            .Where(mapping => mapping.HasSource)
            .Select(mapping => new ReportLineViewModel(
                $"Range {mapping.Index}",
                $"{mapping.TargetStartAddress}+{mapping.Length} -> {mapping.DisplayName}",
                mapping.DisplayDetail)),
        ];
    }

    private List<ReportLineViewModel> CreateReplaceSelectionMissingRows()
    {
        List<ReportLineViewModel> rows = [];
        if (!ReplaceBaseSlot.HasFile)
        {
            rows.Add(new ReportLineViewModel(
                ReplaceBaseSlot.Title,
                ReplaceBaseSlot.DisplayName,
                "Required reference firmware before any Replace build."));
        }

        if (IsGeneralReplaceModeSelected)
        {
            if (!GeneralReplaceMappings.Any(mapping => mapping.HasSource))
            {
                rows.Add(new ReportLineViewModel(
                    "Replacement mapping",
                    "No mapping row has a source.",
                    "Add a range and choose a BIN, Hex Overwrite, or Hex Fill source before Build."));
            }

            return rows;
        }

        foreach (FirmwareSlotViewModel slot in ReplaceSlots.Where(slot =>
            !ReferenceEquals(slot, ReplaceBaseSlot) &&
            !slot.IsOptional &&
            !slot.HasFile))
        {
            rows.Add(new ReportLineViewModel(
                slot.Title,
                slot.DisplayName,
                slot.Description));
        }

        if (IsCtrlRamReplaceModeSelected && GetSelectedReplacementCount() == 0)
        {
            rows.Add(new ReportLineViewModel(
                "CtrlRAM replacement",
                "No CtrlRAM region BIN is selected.",
                "Select at least one region BIN; empty CtrlRAM regions stay from the base firmware."));
        }

        return rows;
    }

    private bool IsSelectedReplacementSlot(FirmwareSlotViewModel slot)
    {
        return !ReferenceEquals(slot, ReplaceBaseSlot) && slot.HasFile;
    }
}

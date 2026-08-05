using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MergePresentationViewModel
{
    internal void RefreshMergeSlotRequirements()
    {
        if (IsAbCodeMergeModeSelected)
        {
            RefreshAbMergeSlots();
            return;
        }

        IReadOnlyList<string> required = WorkbenchCompositionService.GetStandardMergeRequiredAddressSpaces(SelectedIc);
        IReadOnlyList<string> available = WorkbenchCompositionService.GetStandardMergeInputAddressSpaces(SelectedIc);
        foreach (FirmwareSlotViewModel slot in new[] { MergeDpSlot, MergeTpSlot, MergeLdcSlot })
        {
            slot.ApplyExperienceText(Text);
        }
        MergeDpSlot.IsOptional = !required.Contains(WorkbenchAddressSpaceIds.DpInput, StringComparer.Ordinal);
        MergeTpSlot.IsOptional = !required.Contains(WorkbenchAddressSpaceIds.TpInput, StringComparer.Ordinal);
        MergeLdcSlot.IsOptional = !required.Contains(WorkbenchAddressSpaceIds.LdcInput, StringComparer.Ordinal);
        MergeSlots.Clear();
        if (available.Contains(WorkbenchAddressSpaceIds.DpInput, StringComparer.Ordinal))
        {
            MergeSlots.Add(MergeDpSlot);
        }

        if (available.Contains(WorkbenchAddressSpaceIds.TpInput, StringComparer.Ordinal))
        {
            MergeSlots.Add(MergeTpSlot);
        }

        if (available.Contains(WorkbenchAddressSpaceIds.LdcInput, StringComparer.Ordinal))
        {
            MergeSlots.Add(MergeLdcSlot);
        }

        RefreshStandardMergeAuthoringState();
    }

    private void RefreshAbMergeSlots()
    {
        RefreshAbMergeTopologyChoices();
        RefreshAbMergeInputSlots();
    }

    private void RefreshAbMergeInputSlots()
    {
        MergeSlots.Clear();
        _abMergeAddressSpaceBySlotId.Clear();
        foreach (WorkbenchAbMergeInputSlot input in WorkbenchCompositionService.GetAbMergeInputSlots(
                     SelectedIc,
                     GetSelectedAbMergeTopologyToken()))
        {
            if (!_abMergeSlotsByAddressSpace.TryGetValue(input.AddressSpaceId, out FirmwareSlotViewModel? slot))
            {
                slot = new FirmwareSlotViewModel(
                    input.SlotId,
                    ShellTextResources.GetAbSlotTitle(input.Role),
                    Text.GetAbSlotDescription(input),
                    input.Role == WorkbenchAbMergeInputRole.DpAb ? FirmwareSlotKind.Dp : FirmwareSlotKind.Tp);
                _abMergeSlotsByAddressSpace.Add(input.AddressSpaceId, slot);
            }

            slot.ApplyDisplayText(
                ShellTextResources.GetAbSlotTitle(input.Role),
                Text.GetAbSlotDescription(input),
                Text.RequiredLabel,
                Text.OptionalLabel,
                Text.NoBinSelectedLabel);
            _abMergeAddressSpaceBySlotId[input.SlotId] = input.AddressSpaceId;
            MergeSlots.Add(slot);
        }

        RefreshAbMergeAuthoringState();
    }

    private void RefreshAbMergeTopologyChoices()
    {
        IReadOnlyList<WorkbenchAbMergeTopologyChoice> choices =
            AbMergeWorkbenchCompositionService.GetTopologyChoices(SelectedIc);
        AbMergeTopologyChoices.Clear();
        foreach (WorkbenchAbMergeTopologyChoice choice in choices)
        {
            AbMergeTopologyChoices.Add(choice);
        }

        OnPropertyChanged(nameof(HasAbMergeTopologyChoices));
    }

    internal string? GetSelectedAbMergeTopologyToken()
    {
        return AbMergeTopologyChoices.Any(choice =>
            StringComparer.Ordinal.Equals(choice.Token, SelectedNumber))
            ? SelectedNumber
            : null;
    }

    private string GetRequiredStandardMergeSlotLabels()
    {
        IReadOnlyList<string> required = WorkbenchCompositionService.GetStandardMergeRequiredAddressSpaces(SelectedIc);
        return required.Count == 0
            ? "none"
            : string.Join(", ", required.Select(AddressSpaceLabel));
    }

    private static string AddressSpaceLabel(string addressSpaceId)
    {
        return addressSpaceId switch
        {
            WorkbenchAddressSpaceIds.DpInput => "DP",
            WorkbenchAddressSpaceIds.TpInput => "TP",
            WorkbenchAddressSpaceIds.LdcInput => "LDC",
            _ => addressSpaceId,
        };
    }

    private bool CanRunStandardMerge()
    {
        ActiveSessionSnapshot? session = _authoringSessions.StandardMerge.CurrentSnapshot;
        return IsNormalMergeModeSelected &&
            session?.HasCurrentInputInspection == true &&
            StringComparer.Ordinal.Equals(session.SelectedIc, SelectedIc);
    }

    private bool CanRunGeneralMerge()
    {
        return IsGeneralMergeModeSelected &&
            _generalMergeDraft is not null &&
            _generalMergeAdmission?.IsAdmitted == true &&
            _generalMergeActionReadiness?.Preview.IsAvailable == true;
    }

    internal bool TryResolveGeneralMergeOutputInitializer(
        out WorkbenchGeneralMergeInitializer? initializer)
    {
        return WorkbenchCompositionService.TryResolveGeneralMergeOutputInitializer(
            GeneralMergeOutputLength,
            GeneralMergeOutputFillByte,
            out initializer);
    }

    private bool CanRunAbMerge()
    {
        ActiveSessionSnapshot? session = _authoringSessions.AbMerge.CurrentSnapshot;
        return IsAbCodeMergeModeSelected &&
            IsAbMergeSupported &&
            (!HasAbMergeTopologyChoices || GetSelectedAbMergeTopologyToken() is not null) &&
            session?.HasCurrentInputInspection == true &&
            StringComparer.Ordinal.Equals(session.SelectedIc, SelectedIc);
    }

    internal bool CanRunMerge()
    {
        return !_stateBindings.IsGlobalBuildBlocked() &&
            !_stateBindings.IsRunInProgress() &&
            !_stateBindings.IsFirmwareInspectionLoading() &&
            SelectedMergeMode switch
            {
                NormalMergeMode => CanRunStandardMerge(),
                AbCodeMergeMode => CanRunAbMerge(),
                GeneralMergeMode => CanRunGeneralMerge(),
                _ => false,
            };
    }

}

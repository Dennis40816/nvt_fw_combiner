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
            slot.UsesSharedSlotPresentation = true;
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
            session is not null &&
            StringComparer.Ordinal.Equals(session.SelectedIc, SelectedIc) &&
            session.CompilationFingerprint is not null &&
            session.DerivedPublications.Any(publication =>
                publication.Kind == AuthoringDerivedResultKind.Inspection &&
                StringComparer.Ordinal.Equals(
                    publication.CompilationFingerprint,
                    session.CompilationFingerprint)) &&
            session.Slots.Count > 0 &&
            session.Slots.All(static slot =>
                slot.SelectedPath is not null &&
                slot.FileStamp is not null &&
                slot.Lifecycle is
                    AuthoringSlotLifecycle.Verified or
                    AuthoringSlotLifecycle.Warning) &&
            session.InputSlotStatuses.Count == session.Slots.Count &&
            session.InputSlotStatuses.All(status =>
                status.IsTerminal &&
                StringComparer.Ordinal.Equals(
                    status.CompilationFingerprint,
                    session.CompilationFingerprint));
    }

    private bool CanRunGeneralMerge()
    {
        return IsGeneralMergeModeSelected &&
            TryResolveGeneralMergeOutputInitializer(out _) &&
            GeneralMergeMappings.Any(mapping => mapping.HasFile);
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
        return IsAbCodeMergeModeSelected &&
            IsAbMergeSupported &&
            (!HasAbMergeTopologyChoices || GetSelectedAbMergeTopologyToken() is not null) &&
            MergeSlots.Count > 0 &&
            MergeSlots.All(static slot =>
                slot.HasFile &&
                slot.InputInspectionSeverity is not null &&
                !slot.BlocksBuild &&
                !slot.IsInputInspectionPending);
    }

    internal bool CanRunMerge()
    {
        return !_stateBindings.IsRunInProgress() &&
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

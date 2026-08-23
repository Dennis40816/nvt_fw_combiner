using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MergePresentationViewModel
{
    internal void RefreshMergeSlotRequirements()
    {
        if (IsAbCodeMergeModeSelected)
        {
            RefreshAbMergeSlots();
            return;
        }

        IReadOnlyList<string> required =
            _compositionServices.StandardMergeAuthoring.GetRequiredAddressSpaces(SelectedIc);
        IReadOnlyList<string> available =
            _compositionServices.StandardMergeAuthoring.GetInputAddressSpaces(SelectedIc);
        foreach (FirmwareSlotViewModel slot in new[] { MergeDpSlot, MergeTpSlot, MergeLdcSlot })
        {
            slot.ApplyExperienceText(Text);
        }
        MergeDpSlot.IsOptional = !required.Contains(CompositionAddressSpaceIds.DpInput, StringComparer.Ordinal);
        MergeTpSlot.IsOptional = !required.Contains(CompositionAddressSpaceIds.TpInput, StringComparer.Ordinal);
        MergeLdcSlot.IsOptional = !required.Contains(CompositionAddressSpaceIds.LdcInput, StringComparer.Ordinal);
        MergeSlots.Clear();
        if (available.Contains(CompositionAddressSpaceIds.DpInput, StringComparer.Ordinal))
        {
            MergeSlots.Add(MergeDpSlot);
        }

        if (available.Contains(CompositionAddressSpaceIds.TpInput, StringComparer.Ordinal))
        {
            MergeSlots.Add(MergeTpSlot);
        }

        if (available.Contains(CompositionAddressSpaceIds.LdcInput, StringComparer.Ordinal))
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
        _abMergeBindingsByAddressSpace.Clear();
        CompiledAuthoringSelectionSnapshot projection = ResolveAbMergeAuthoringSnapshot();
        foreach (CompiledAuthoringInputBinding input in projection.InputBindings)
        {
            _abMergeBindingsByAddressSpace.Add(input.AddressSpaceId, input);
            if (!_abMergeSlotsByAddressSpace.TryGetValue(input.AddressSpaceId, out FirmwareSlotViewModel? slot))
            {
                slot = new FirmwareSlotViewModel(
                    input.SlotId,
                    ShellTextResources.GetAbSlotTitle(input.Role),
                    Text.GetAbSlotDescription(input),
                    input.Role == "dp-ab" ? FirmwareSlotKind.Dp : FirmwareSlotKind.Tp);
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

        ApplyAbSameTpPresentation();

        RefreshAbMergeAuthoringState(projection);
    }

    private void RefreshAbMergeTopologyChoices()
    {
        IReadOnlyList<CapabilityTopologyChoice> choices =
            _compositionServices.AbMergeAuthoring.GetTopologyChoices(SelectedIc);
        AbMergeTopologyChoices.Clear();
        _abMergeTopologyChoicesIcId = SelectedIc;
        foreach (CapabilityTopologyChoice choice in choices)
        {
            AbMergeTopologyChoices.Add(choice);
        }

        OnPropertyChanged(nameof(HasAbMergeTopologyChoices));
    }

    internal string? GetSelectedAbMergeTopologyToken()
    {
        return StringComparer.Ordinal.Equals(_abMergeTopologyChoicesIcId, SelectedIc) &&
            AbMergeTopologyChoices.Any(choice =>
            StringComparer.Ordinal.Equals(choice.Token, SelectedNumber))
            ? SelectedNumber
            : null;
    }

    private string GetRequiredStandardMergeSlotLabels()
    {
        IReadOnlyList<string> required =
            _compositionServices.StandardMergeAuthoring.GetRequiredAddressSpaces(SelectedIc);
        return required.Count == 0
            ? "none"
            : string.Join(", ", required.Select(AddressSpaceLabel));
    }

    private static string AddressSpaceLabel(string addressSpaceId)
    {
        return addressSpaceId switch
        {
            CompositionAddressSpaceIds.DpInput => "DP",
            CompositionAddressSpaceIds.TpInput => "TP",
            CompositionAddressSpaceIds.LdcInput => "LDC",
            _ => addressSpaceId,
        };
    }

    private bool CanRunStandardMerge()
    {
        ActiveSessionSnapshot? session = _standardMergeSession.CurrentSnapshot;
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
        out GeneralMergeInitializer? initializer)
    {
        return GeneralMergeAuthoringUseCase.TryResolveOutputInitializer(
            GeneralMergeOutputLength,
            GeneralMergeOutputFillByte,
            out initializer);
    }

    private bool CanRunAbMerge()
    {
        ActiveSessionSnapshot? session = _abMergeSession.CurrentSnapshot;
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
            !Inspection.IsRunning &&
            SelectedMergeMode switch
            {
                NormalMergeMode => CanRunStandardMerge(),
                AbCodeMergeMode => CanRunAbMerge(),
                GeneralMergeMode => CanRunGeneralMerge(),
                _ => false,
            };
    }

}

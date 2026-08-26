using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MergePresentationViewModel
{
    private string? _preparedStandardMergeIc;
    private IReadOnlyList<string>? _preparedStandardMergeRequired;
    private IReadOnlyList<string>? _preparedStandardMergeAvailable;
    private CompiledAuthoringSelectionSnapshot? _preparedStandardMergeSnapshot;
    private ReadOnlyCollection<string> _appliedStandardMergeRequired =
        Array.AsReadOnly(Array.Empty<string>());

    internal void ValidateContextRefresh(
        string icId,
        string number,
        string mode,
        CapabilitySelectorPublication publication,
        string? generalOutputLength,
        string? generalOutputFillByte)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentNullException.ThrowIfNull(publication);
        switch (mode)
        {
            case NormalMergeMode:
                _preparedStandardMergeIc = null;
                _preparedStandardMergeRequired = null;
                _preparedStandardMergeAvailable = null;
                _preparedStandardMergeSnapshot = null;
                IReadOnlyList<string> required =
                    _compositionServices.StandardMergeAuthoring.GetRequiredAddressSpaces(icId);
                IReadOnlyList<string> available =
                    _compositionServices.StandardMergeAuthoring.GetInputAddressSpaces(icId);
                CompiledAuthoringSelectionSnapshot snapshot =
                    ResolveStandardMergeAuthoringSnapshotCore(icId);
                _preparedStandardMergeIc = icId;
                _preparedStandardMergeRequired = required;
                _preparedStandardMergeAvailable = available;
                _preparedStandardMergeSnapshot = snapshot;
                break;
            case AbCodeMergeMode:
                _preparedAbMergeIc = null;
                _preparedAbMergeTopology = null;
                _hasPreparedAbMergeSnapshot = false;
                _preparedAbMergeSnapshot = null;
                IReadOnlyList<CapabilityTopologyChoice> choices =
                    publication.GetAbMergeTopologyChoices(icId);
                string? topologyToken = choices.Any(choice =>
                    StringComparer.Ordinal.Equals(choice.Token, number))
                        ? number
                        : null;
                CompiledAuthoringSelectionSnapshot abSnapshot =
                    ResolveAbMergeAuthoringSnapshotCore(icId, topologyToken);
                _preparedAbMergeIc = icId;
                _preparedAbMergeTopology = topologyToken;
                _hasPreparedAbMergeSnapshot = true;
                _preparedAbMergeSnapshot = abSnapshot;
                break;
            case GeneralMergeMode:
                ValidateGeneralMergeContextRefresh(
                    icId,
                    generalOutputLength ?? string.Empty,
                    generalOutputFillByte ?? string.Empty);
                break;
            default:
                throw new InvalidOperationException("Unknown Merge workflow mode.");
        }
    }

    internal void RefreshMergeSlotRequirements()
    {
        if (!HasSelectedIc)
        {
            _appliedStandardMergeRequired = Array.AsReadOnly(Array.Empty<string>());
            MergeSlots.Clear();
            AbMergeTopologyChoices.Clear();
            _abMergeTopologyChoicesIcId = null;
            return;
        }

        if (IsAbCodeMergeModeSelected)
        {
            RefreshAbMergeSlots();
            return;
        }

        if (IsGeneralMergeModeSelected)
        {
            MergeSlots.Clear();
            AbMergeTopologyChoices.Clear();
            _abMergeTopologyChoicesIcId = null;
            _abMergeAddressSpaceBySlotId.Clear();
            _abMergeBindingsByAddressSpace.Clear();
            RefreshGeneralMergeAuthoringState();
            return;
        }

        IReadOnlyList<string> required;
        IReadOnlyList<string> available;
        if (string.Equals(_preparedStandardMergeIc, SelectedIc, StringComparison.Ordinal) &&
            _preparedStandardMergeRequired is not null &&
            _preparedStandardMergeAvailable is not null)
        {
            required = _preparedStandardMergeRequired;
            available = _preparedStandardMergeAvailable;
            _preparedStandardMergeRequired = null;
            _preparedStandardMergeAvailable = null;
        }
        else
        {
            required = _compositionServices.StandardMergeAuthoring
                .GetRequiredAddressSpaces(SelectedIc);
            available = _compositionServices.StandardMergeAuthoring
                .GetInputAddressSpaces(SelectedIc);
        }
        _appliedStandardMergeRequired = Array.AsReadOnly([.. required]);
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
            _stateBindings.GetAbMergeTopologyChoices(SelectedIc);
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
        return !HasSelectedIc || _appliedStandardMergeRequired.Count == 0
            ? "none"
            : string.Join(", ", _appliedStandardMergeRequired.Select(AddressSpaceLabel));
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
            StringComparer.Ordinal.Equals(session.SelectedIc, SelectedIc) &&
            HasCurrentAbMergeActionReadiness(build: false);
    }

    internal bool CanRunMerge()
    {
        return HasSelectedIc &&
            !_stateBindings.IsGlobalBuildBlocked() &&
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

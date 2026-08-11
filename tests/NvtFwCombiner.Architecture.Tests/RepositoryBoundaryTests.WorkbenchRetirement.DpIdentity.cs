namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>DP Replace preserves compiler slot identity separately from its bound address space end to end.</summary>
    [Fact]
    public void DpReplaceSlotAndAddressSpaceIdentitiesStayExplicitEndToEnd()
    {
        string application = string.Concat(
            ReadText("src/NvtFwCombiner.Application/Authoring/CompiledAuthoringWorkflow.Contracts.cs"),
            ReadText("src/NvtFwCombiner.Application/Authoring/CompiledAuthoringWorkflow.Selection.cs"));
        string acceptedBinding = ReadText(
            "src/NvtFwCombiner.Application/Composition/AcceptedSessionCompositionExecution.cs");
        string slotViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotViewModel.cs");
        string replaceAuthoring = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Authoring.cs");
        string replaceExecution = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Execution.cs");
        string replaceProjection = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Replace.cs");

        Assert.Contains("public sealed record CompiledAuthoringInputBinding(", application, StringComparison.Ordinal);
        Assert.Contains("string SlotId,", application, StringComparison.Ordinal);
        Assert.Contains("string AddressSpaceId,", application, StringComparison.Ordinal);
        Assert.Contains("ProjectInputBindings(discovery)", application, StringComparison.Ordinal);
        Assert.Contains("ResolveSlotDefinitionId(", acceptedBinding, StringComparison.Ordinal);
        Assert.Contains(
            "compiledComposition.V2Details.InputContract.SpaceBindings",
            acceptedBinding,
            StringComparison.Ordinal);
        Assert.Contains("public string? CompiledSlotId { get; }", slotViewModel, StringComparison.Ordinal);
        Assert.Contains("ReplaceDefinitionId(slot, dpProjection)", replaceAuthoring, StringComparison.Ordinal);
        Assert.Contains("SelectedReplaceMode == CtrlRamReplaceMode", replaceAuthoring, StringComparison.Ordinal);
        Assert.Contains("CompositionAddressSpaceIds.ReferenceBase", replaceAuthoring, StringComparison.Ordinal);
        Assert.Contains("binding.AddressSpaceId", replaceAuthoring, StringComparison.Ordinal);
        Assert.Contains("slot.CompiledSlotId", replaceAuthoring, StringComparison.Ordinal);
        Assert.Contains(
            "candidate.SlotId, slot.CompiledSlotId",
            replaceExecution,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "candidate.SlotId, slot.AddressSpaceId",
            replaceExecution,
            StringComparison.Ordinal);
        Assert.Contains("compiledSlotId: slot.CompiledSlotId", replaceProjection, StringComparison.Ordinal);
    }
}

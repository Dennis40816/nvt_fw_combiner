namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Compiled authoring preserves compiler slot identity separately from its bound address space end to end.</summary>
    [Fact]
    public void CompiledSlotAndAddressSpaceIdentitiesStayExplicitEndToEnd()
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
        string replaceProjection = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Replace.cs");

        Assert.Contains("public sealed record CompiledAuthoringInputBinding(", application, StringComparison.Ordinal);
        Assert.Contains("string SlotId,", application, StringComparison.Ordinal);
        Assert.Contains("string AddressSpaceId,", application, StringComparison.Ordinal);
        Assert.Contains("ProjectInputBindings(discovery)", application, StringComparison.Ordinal);
        Assert.Contains("ResolveSlotDefinitionId(", acceptedBinding, StringComparison.Ordinal);
        Assert.Contains("StringComparer.Ordinal.Equals(binding.AddressSpaceId, addressSpaceId)).SlotId", acceptedBinding, StringComparison.Ordinal);
        Assert.Contains(
            "compiledComposition.V2Details.InputContract.SpaceBindings",
            acceptedBinding,
            StringComparison.Ordinal);
        Assert.Contains("public string? CompiledSlotId { get; }", slotViewModel, StringComparison.Ordinal);
        Assert.Contains("SelectedReplaceMode == CtrlRamReplaceMode", replaceAuthoring, StringComparison.Ordinal);
        Assert.Contains("_compositionServices.CtrlRamAuthoring.AdoptInspectedBatch", replaceAuthoring, StringComparison.Ordinal);
        Assert.Contains("compiledSlotId: slot.CompiledSlotId", replaceProjection, StringComparison.Ordinal);
    }
}

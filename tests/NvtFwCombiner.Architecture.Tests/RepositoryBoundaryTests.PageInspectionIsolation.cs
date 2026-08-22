namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Selected-file inspection captures one page owner instead of carrying both page modes.</summary>
    [Fact]
    public void SelectedFileInspectionIsPageOwned()
    {
        string inspection = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.FirmwareInspection.cs");
        string deviceContext = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.DeviceContext.cs");
        string inspectionSession = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareInspectionSession.cs");
        string workflowSession = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.cs");
        string workflowContext = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.WorkflowContext.cs");
        string mainContext = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Context.cs");
        string construction = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs");

        Assert.Contains("WorkflowInspectionContext context", inspection, StringComparison.Ordinal);
        Assert.Contains("if (ActiveInspectionContext is not { } context)", inspection, StringComparison.Ordinal);
        Assert.Contains("context.Mode == ExperienceIds.GeneralMerge", inspection, StringComparison.Ordinal);
        Assert.Contains("context.Mode == ExperienceIds.GeneralReplace", inspection, StringComparison.Ordinal);
        Assert.Contains(
            "{ IsStandardMerge: true } or { IsAbMerge: true } => MergeSlots",
            inspection,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "{ IsAbMerge: true } => AbMergeSlots",
            inspection,
            StringComparison.Ordinal);
        Assert.Contains("request.Context == currentContext", inspectionSession, StringComparison.Ordinal);
        Assert.Contains("WorkflowInspectionContext Context", inspectionSession, StringComparison.Ordinal);
        Assert.Contains("internal enum WorkflowInspectionOwner", inspectionSession, StringComparison.Ordinal);
        Assert.Contains(
            "ShellPage.Home or ShellPage.HexEditor => null",
            workflowSession,
            StringComparison.Ordinal);
        Assert.DoesNotContain("string MergeMode", inspectionSession, StringComparison.Ordinal);
        Assert.DoesNotContain("string ReplaceMode", inspectionSession, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RefreshAllSelectedFirmwareInspectionsAsync",
            inspection + deviceContext,
            StringComparison.Ordinal);
        Assert.Contains("ActiveWorkflowOwner", workflowContext, StringComparison.Ordinal);
        Assert.Contains("InitializeWorkflowPageContexts", workflowContext, StringComparison.Ordinal);
        Assert.Contains("ActivateWorkflowPageContext", workflowContext, StringComparison.Ordinal);
        Assert.Contains("StoreWorkflowPageContext(owner, value, SelectedNumber)", deviceContext, StringComparison.Ordinal);
        Assert.Matches(@"RefreshContextState\(\s*owner,", deviceContext);
        Assert.Contains("WorkflowSession.RememberCurrentWorkflowContext()", mainContext, StringComparison.Ordinal);
        Assert.Contains("WorkflowSession.ActivateWorkflowPageContext(page)", mainContext, StringComparison.Ordinal);
        Assert.Contains(
            "GetWorkflowPageIc(WorkflowInspectionOwner.Merge)",
            construction,
            StringComparison.Ordinal);
        Assert.Contains(
            "GetWorkflowPageIc(WorkflowInspectionOwner.Replace)",
            construction,
            StringComparison.Ordinal);
        Assert.DoesNotContain("RememberReplaceWorkflowContext", workflowContext + deviceContext, StringComparison.Ordinal);
    }
}

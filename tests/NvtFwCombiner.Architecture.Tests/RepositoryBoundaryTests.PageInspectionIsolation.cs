namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Selected-file inspection captures one page owner instead of carrying both page modes.</summary>
    [Fact]
    public void SelectedFileInspectionIsPageOwned()
    {
        string inspection = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.FirmwareInspection.cs");
        string inspectionRefresh = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.FirmwareInspectionRefresh.cs");
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
        string mainModeObservers = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.MergePresentation.cs") +
            ReadText(
                "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.ReplacePresentation.cs");
        string construction = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs");
        string mergeState = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.State.cs");
        string notifications = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.DeviceContextNotifications.cs");
        string mergeBindings = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergeStateBindings.cs");
        string replaceState = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.State.cs");
        string replaceMemory = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Memory.cs");
        static string Slice(string source, string startToken, string endToken)
        {
            int start = source.IndexOf(startToken, StringComparison.Ordinal);
            int end = start < 0
                ? -1
                : source.IndexOf(endToken, start + startToken.Length, StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start);
            return source[start..end];
        }

        string inspectionSlots = Slice(
            inspection,
            "private IEnumerable<FirmwareSlotViewModel> InspectionSlots(",
            "private IEnumerable<FirmwareSlotViewModel> AllInspectionSlots(");
        string findInspectionSlot = Slice(
            inspection,
            "private FirmwareSlotViewModel? FindInspectionSlot(",
            "private FirmwareInspectionItemRequest CreateFirmwareInspectionItem(");
        string refreshMerge = Slice(
            inspectionRefresh,
            "internal Task RefreshSelectedMergeFirmwareInspectionsAsync(",
            "internal Task RefreshSelectedReplaceFirmwareInspectionsAsync(");
        string refreshReplace = Slice(
            inspectionRefresh,
            "private Task RefreshSelectedReplaceFirmwareInspectionsAsync(",
            "private bool TryRefreshRetainedReplaceFirmwareInspectionsIfStale(");

        Assert.Contains("WorkflowInspectionContext context", inspection, StringComparison.Ordinal);
        Assert.Contains("if (ActiveInspectionContext is not { } context)", inspection, StringComparison.Ordinal);
        Assert.Contains(
            "{ IsGeneralMerge: true } =>",
            inspection,
            StringComparison.Ordinal);
        Assert.Contains(
            "{ IsGeneralReplace: true } => _replace.GeneralReplaceMappings",
            inspection,
            StringComparison.Ordinal);
        Assert.Contains(
            "{ IsStandardMerge: true } or { IsAbMerge: true } => MergeSlots",
            inspectionSlots,
            StringComparison.Ordinal);
        Assert.Contains(
            "{ IsDpReplace: true } or { IsCtrlRamReplace: true } =>",
            inspectionSlots,
            StringComparison.Ordinal);
        Assert.Contains(
            "ReplaceSlots.Append(ReplaceBaseSlot).Distinct()",
            inspectionSlots,
            StringComparison.Ordinal);
        Assert.Contains("{ IsGeneralMerge: true } => []", inspectionSlots, StringComparison.Ordinal);
        Assert.Contains(
            "{ IsGeneralReplace: true } => [ReplaceBaseSlot]",
            inspectionSlots,
            StringComparison.Ordinal);
        Assert.Contains("_ => []", inspectionSlots, StringComparison.Ordinal);
        Assert.DoesNotContain("Mode:", inspectionSlots, StringComparison.Ordinal);
        Assert.DoesNotContain("AbMergeSlots", inspectionSlots, StringComparison.Ordinal);
        Assert.Contains("InspectionSlots(context)", findInspectionSlot, StringComparison.Ordinal);
        Assert.Contains("InspectionSlots(context)", refreshMerge, StringComparison.Ordinal);
        Assert.Contains("InspectionSlots(context)", refreshReplace, StringComparison.Ordinal);
        Assert.Contains(
            "RefreshRetainedMergeFirmwareInspectionsIfStaleAsync",
            construction,
            StringComparison.Ordinal);
        Assert.Contains(
            "RequiresCurrentSelectorReinspection(context)",
            refreshMerge + refreshReplace,
            StringComparison.Ordinal);
        Assert.Contains(
            "projection.InputSlotStatus?.ResolutionToken",
            inspectionRefresh,
            StringComparison.Ordinal);
        Assert.Contains(
            "projection.InputSlotCatalog?.ResolutionToken",
            inspectionRefresh,
            StringComparison.Ordinal);
        Assert.Contains(
            "token is null",
            inspectionRefresh,
            StringComparison.Ordinal);
        Assert.Contains(
            "? !context.IsGeneralReplace",
            inspectionRefresh,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryRefreshRetainedReplaceFirmwareInspectionsIfStale()",
            deviceContext,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "canonicalCatalogInspectionRefreshPending",
            inspection + inspectionRefresh + mergeState + deviceContext,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "internal bool IsGeneralMerge => IsMerge && Mode == ExperienceIds.GeneralMerge",
            inspectionSession,
            StringComparison.Ordinal);
        Assert.Contains(
            "internal bool IsGeneralReplace => IsReplace && Mode == ExperienceIds.GeneralReplace",
            inspectionSession,
            StringComparison.Ordinal);
        Assert.DoesNotContain("context.Mode ==", inspection, StringComparison.Ordinal);
        Assert.DoesNotContain("Mode: ExperienceIds.", inspection, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GeneralMergeMappings.Concat",
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
        Assert.Contains("WorkflowSession.PublishActiveNavigationContext()", mainContext, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkflowSession.PublishFullWorkflowContext()", mainContext, StringComparison.Ordinal);
        Assert.Contains(
            "GetWorkflowPageIc(WorkflowInspectionOwner.Merge)",
            construction,
            StringComparison.Ordinal);
        Assert.Contains(
            "GetWorkflowPageIc(WorkflowInspectionOwner.Replace)",
            construction,
            StringComparison.Ordinal);
        Assert.DoesNotContain("RememberReplaceWorkflowContext", workflowContext + deviceContext, StringComparison.Ordinal);
        AssertContainsAll(
            notifications,
            "PublishAcceptedMergeSharedContext",
            "PublishRefreshedSharedContext");
        Assert.Contains("ApplyAcceptedReplaceModeContext", deviceContext, StringComparison.Ordinal);
        Assert.Contains("RecordAcceptedModeSelection", notifications + deviceContext, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemActivityCodes.ModeSelected", mainModeObservers, StringComparison.Ordinal);
        Assert.Contains("PublishAcceptedModeContext", mergeBindings, StringComparison.Ordinal);
        Assert.Contains("PublishAcceptedMergeSharedContext", construction, StringComparison.Ordinal);
        Assert.Contains("PublishAcceptedModeContext", mergeState, StringComparison.Ordinal);
        Assert.Contains("PublishAcceptedModeContext", replaceState, StringComparison.Ordinal);
        Assert.Contains("PrepareAcceptedModeContextState", replaceMemory, StringComparison.Ordinal);
        AssertDoesNotContainAny(
            notifications + deviceContext + construction + mergeState + mergeBindings + replaceState + replaceMemory,
            "notifyIcChoices",
            "notifyModeChoices",
            "NotifySharedContextChanged");
    }
}

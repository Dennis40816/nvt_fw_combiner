using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    private const int StandardMergeAuthoringPortIndex = 0;
    private const int AbMergeAuthoringPortIndex = 1;
    private const int DpReplaceAuthoringPortIndex = 2;
    private const int CtrlRamAuthoringPortIndex = 4;

    /// <summary>A withdrawn Merge mode is neither listed nor dispatchable through its setter.</summary>
    [Theory]
    [InlineData(ExperienceIds.StandardMerge)]
    [InlineData(ExperienceIds.AbMerge)]
    [InlineData(ExperienceIds.GeneralMerge)]
    public async Task WithdrawnMergeModeIsAbsentAndSetterDoesNotDispatch(string withdrawnMode)
    {
        var policy = new MutableAbCatalogPolicy();
        (
            PresentationHostServices services,
            MainWindowViewModel viewModel,
            AuthoringPortSentinel sentinel) = CreateCatalogRefreshSentinelViewModel(policy);
        CapabilitySelectorPublication original = services.Composition.Capabilities
            .GetSelectorPublication();
        string icId = original.IcIds.First(ic =>
            original.IsWorkflowAuthorable(ic, withdrawnMode) &&
            PageWorkflowIds(ShellPage.Merge).Any(other =>
                !StringComparer.Ordinal.Equals(other, withdrawnMode) &&
                original.IsWorkflowAuthorable(ic, other)));
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = icId;

        policy.DisableWorkflowFor(withdrawnMode, icId);
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        string retainedMode = viewModel.Merge.SelectedMergeMode;
        sentinel.Arm();

        viewModel.Merge.SelectedMergeMode = withdrawnMode;

        Assert.DoesNotContain(withdrawnMode, viewModel.Merge.MergeModeChoices);
        Assert.Equal(retainedMode, viewModel.Merge.SelectedMergeMode);
        Assert.Equal(0, sentinel.ArmedCallCount);
    }

    /// <summary>A withdrawn Replace mode is neither listed nor dispatchable through its setter.</summary>
    [Theory]
    [InlineData(ExperienceIds.DpReplace)]
    [InlineData(ExperienceIds.CtrlRamReplace)]
    [InlineData(ExperienceIds.GeneralReplace)]
    public async Task WithdrawnReplaceModeIsAbsentAndSetterDoesNotDispatch(string withdrawnMode)
    {
        var policy = new MutableAbCatalogPolicy();
        (
            PresentationHostServices services,
            MainWindowViewModel viewModel,
            AuthoringPortSentinel sentinel) = CreateCatalogRefreshSentinelViewModel(policy);
        CapabilitySelectorPublication original = services.Composition.Capabilities
            .GetSelectorPublication();
        string icId = original.IcIds.First(ic =>
            original.IsWorkflowAuthorable(ic, withdrawnMode) &&
            PageWorkflowIds(ShellPage.Replace).Any(other =>
                !StringComparer.Ordinal.Equals(other, withdrawnMode) &&
                original.IsWorkflowAuthorable(ic, other)));
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = icId;

        policy.DisableWorkflowFor(withdrawnMode, icId);
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        string retainedMode = viewModel.Replace.SelectedReplaceMode;
        sentinel.Arm();

        viewModel.Replace.SelectedReplaceMode = withdrawnMode;

        Assert.DoesNotContain(withdrawnMode, viewModel.Replace.ReplaceModeChoices);
        Assert.Equal(retainedMode, viewModel.Replace.SelectedReplaceMode);
        Assert.Equal(0, sentinel.ArmedCallCount);
    }

    /// <summary>An authorable AB route without a typed topology never borrows generic IC-number choices.</summary>
    [Fact]
    public void ActiveAbRouteWithEmptyTopologyFailsClosedWithoutGenericNumberFallback()
    {
        var policy = new MutableAbCatalogPolicy();
        (PresentationHostServices services, MainWindowViewModel viewModel) =
            CreateCatalogRefreshViewModel(policy);
        CapabilitySelectorPublication publication = services.Composition.Capabilities
            .GetSelectorPublication();
        string icId = publication.AbMergeIcIds.First(candidate =>
            publication.GetAbMergeTopologyChoices(candidate).Count == 0);
        Assert.True(publication.IsWorkflowAuthorable(icId, ExperienceIds.AbMerge));

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = icId;
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        Assert.Empty(viewModel.WorkflowSession.NumberSelectionChoices);
        Assert.Equal(string.Empty, viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal(
            string.Empty,
            viewModel.WorkflowSession.GetWorkflowPageNumber(WorkflowInspectionOwner.Merge));
    }

    /// <summary>A catalog can restore a page after every mode was withdrawn without indexing an empty lifecycle key.</summary>
    [Theory]
    [InlineData("Merge")]
    [InlineData("Replace")]
    public async Task RestoredPageWorkflowsRecoverFromEmptySelectedMode(string pageName)
    {
        ShellPage page = Enum.Parse<ShellPage>(pageName);
        var policy = new MutableAbCatalogPolicy();
        (_, MainWindowViewModel viewModel) = CreateCatalogRefreshViewModel(policy);
        ShowPage(viewModel, page);

        foreach (string workflowId in PageWorkflowIds(page))
        {
            policy.DisableEveryWorkflow(workflowId);
        }

        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);

        if (page == ShellPage.Merge)
        {
            Assert.Empty(viewModel.Merge.MergeModeChoices);
            Assert.Equal(string.Empty, viewModel.Merge.SelectedMergeMode);
        }
        else
        {
            Assert.Empty(viewModel.Replace.ReplaceModeChoices);
            Assert.Equal(string.Empty, viewModel.Replace.SelectedReplaceMode);
        }

        foreach (string workflowId in PageWorkflowIds(page))
        {
            policy.EnableEveryWorkflow(workflowId);
        }

        Exception? exception = await Record.ExceptionAsync(
            () => viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null));

        Assert.Null(exception);
        if (page == ShellPage.Merge)
        {
            Assert.Contains(viewModel.Merge.SelectedMergeMode, viewModel.Merge.MergeModeChoices);
        }
        else
        {
            Assert.Contains(viewModel.Replace.SelectedReplaceMode, viewModel.Replace.ReplaceModeChoices);
        }
    }

    /// <summary>A partial Replace withdrawal keeps the page IC and never queries the withdrawn authoring port.</summary>
    [Theory]
    [InlineData(ExperienceIds.DpReplace, ExperienceIds.CtrlRamReplace)]
    [InlineData(ExperienceIds.CtrlRamReplace, ExperienceIds.DpReplace)]
    public async Task PartialReplaceWithdrawalRepairsActiveModeWithoutQueryingWithdrawnPort(
        string withdrawnMode,
        string fallbackMode)
    {
        var policy = new MutableAbCatalogPolicy();
        (
            PresentationHostServices services,
            MainWindowViewModel viewModel,
            AuthoringPortSentinel sentinel) = CreateCatalogRefreshSentinelViewModel(policy);
        CapabilitySelectorPublication original = services.Composition.Capabilities
            .GetSelectorPublication();
        string retainedIc = original.IcIds.First(icId =>
            original.IsWorkflowAuthorable(icId, withdrawnMode) &&
            original.IsWorkflowAuthorable(icId, fallbackMode));
        viewModel.WorkflowSession.SelectedIc = retainedIc;
        OpenReplace(viewModel, withdrawnMode);
        Assert.Equal(withdrawnMode, viewModel.Replace.SelectedReplaceMode);

        policy.DisableWorkflowFor(withdrawnMode, retainedIc);
        sentinel.Arm();
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        CapabilitySelectorPublication refreshed = services.Composition.Capabilities
            .GetSelectorPublication();

        Assert.Equal(ShellPage.Replace, viewModel.SelectedPage);
        Assert.Equal(retainedIc, viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(
            retainedIc,
            viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Replace));
        Assert.False(refreshed.IsWorkflowAuthorable(retainedIc, withdrawnMode));
        Assert.True(refreshed.IsWorkflowAuthorable(retainedIc, fallbackMode));
        Assert.Equal(fallbackMode, viewModel.Replace.SelectedReplaceMode);
        Assert.Contains(fallbackMode, viewModel.Replace.ReplaceModeChoices);
        Assert.Equal(0, sentinel.ArmedCallCounts[ReplaceAuthoringPortIndex(withdrawnMode)]);
    }

    /// <summary>A hidden Replace withdrawal is staged without borrowing Merge or querying the withdrawn port.</summary>
    [Theory]
    [InlineData(ExperienceIds.DpReplace, ExperienceIds.CtrlRamReplace)]
    [InlineData(ExperienceIds.CtrlRamReplace, ExperienceIds.DpReplace)]
    public async Task PartialReplaceWithdrawalRepairsHiddenModeWithoutDisturbingActiveMerge(
        string withdrawnMode,
        string fallbackMode)
    {
        var policy = new MutableAbCatalogPolicy();
        (
            PresentationHostServices services,
            MainWindowViewModel viewModel,
            AuthoringPortSentinel sentinel) = CreateCatalogRefreshSentinelViewModel(policy);
        CapabilitySelectorPublication original = services.Composition.Capabilities
            .GetSelectorPublication();
        string retainedReplaceIc = original.IcIds.First(icId =>
            original.IsWorkflowAuthorable(icId, withdrawnMode) &&
            original.IsWorkflowAuthorable(icId, fallbackMode));
        OpenReplace(viewModel, withdrawnMode);
        viewModel.WorkflowSession.SelectedIc = retainedReplaceIc;
        viewModel.ShowMergeCommand.Execute(null);
        string activeMergeIc = viewModel.WorkflowSession.SelectedIc;
        string activeMergeNumber = viewModel.WorkflowSession.SelectedNumber;
        var mergeChanges = new List<string?>();
        var replaceChanges = new List<string?>();
        var workflowChanges = new List<string?>();
        viewModel.Merge.PropertyChanged += (_, args) => mergeChanges.Add(args.PropertyName);
        viewModel.Replace.PropertyChanged += (_, args) => replaceChanges.Add(args.PropertyName);
        viewModel.WorkflowSession.PropertyChanged += (_, args) => workflowChanges.Add(args.PropertyName);

        policy.DisableWorkflowFor(withdrawnMode, retainedReplaceIc);
        sentinel.Arm();
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(ShellPage.Merge, viewModel.SelectedPage);
        Assert.Equal(activeMergeIc, viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(activeMergeNumber, viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal(
            retainedReplaceIc,
            viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Replace));
        Assert.Equal(fallbackMode, viewModel.Replace.SelectedReplaceMode);
        Assert.Equal(0, sentinel.ArmedCallCounts[ReplaceAuthoringPortIndex(withdrawnMode)]);
        Assert.Equal(1, mergeChanges.Count(propertyName =>
            propertyName == nameof(MergePresentationViewModel.MergeModeChoices)));
        Assert.Equal(1, mergeChanges.Count(propertyName =>
            propertyName == nameof(MergePresentationViewModel.SelectedMergeMode)));
        Assert.Equal(1, mergeChanges.Count(propertyName =>
            propertyName == nameof(MergePresentationViewModel.IsNormalMergeModeSelected)));
        Assert.Equal(1, mergeChanges.Count(propertyName =>
            propertyName == nameof(MergePresentationViewModel.MergeOutputFileName)));
        Assert.Equal(1, mergeChanges.Count(propertyName =>
            propertyName == nameof(MergePresentationViewModel.MergeMemorySummary)));
        Assert.DoesNotContain(nameof(ReplacePresentationViewModel.ReplaceModeChoices), replaceChanges);
        Assert.DoesNotContain(nameof(ReplacePresentationViewModel.SelectedReplaceMode), replaceChanges);
        Assert.DoesNotContain(nameof(ReplacePresentationViewModel.Inspection), replaceChanges);
        Assert.Contains(nameof(WorkflowSessionPresentationViewModel.IcChoices), workflowChanges);

        viewModel.ShowReplaceCommand.Execute(null);

        Assert.Equal(retainedReplaceIc, viewModel.WorkflowSession.SelectedIc);
        Assert.Contains(fallbackMode, viewModel.Replace.ReplaceModeChoices);
        Assert.Equal(1, replaceChanges.Count(propertyName =>
            propertyName == nameof(ReplacePresentationViewModel.ReplaceModeChoices)));
        Assert.Equal(1, replaceChanges.Count(propertyName =>
            propertyName == nameof(ReplacePresentationViewModel.SelectedReplaceMode)));
        Assert.Equal(1, replaceChanges.Count(propertyName =>
            propertyName == nameof(ReplacePresentationViewModel.IsStructuredReplaceModeSelected)));
        Assert.Equal(1, replaceChanges.Count(propertyName =>
            propertyName == nameof(ReplacePresentationViewModel.ReplaceOutputFileName)));
        Assert.Equal(1, replaceChanges.Count(propertyName =>
            propertyName == nameof(ReplacePresentationViewModel.ReplaceMemorySummary)));
        Assert.Equal(0, sentinel.ArmedCallCounts[ReplaceAuthoringPortIndex(withdrawnMode)]);
    }

    /// <summary>Removing one page aggregate for an IC repairs only that page and retains the other page's draft.</summary>
    [Theory]
    [InlineData("Merge")]
    [InlineData("Replace")]
    public async Task PageAggregateWithdrawalPreservesOppositeHiddenPageContext(
        string activePageName)
    {
        ShellPage activePage = Enum.Parse<ShellPage>(activePageName);
        var policy = new MutableAbCatalogPolicy();
        (PresentationHostServices services, MainWindowViewModel viewModel) =
            CreateCatalogRefreshViewModel(policy);
        CapabilitySelectorPublication original = services.Composition.Capabilities
            .GetSelectorPublication();
        string isolatedIc = original.IcIds.First(icId =>
            !StringComparer.Ordinal.Equals(icId, original.DefaultIcId) &&
            IsPageAuthorable(original, icId, ShellPage.Merge) &&
            IsPageAuthorable(original, icId, ShellPage.Replace));
        ShellPage hiddenPage = activePage == ShellPage.Merge
            ? ShellPage.Replace
            : ShellPage.Merge;
        WorkflowInspectionOwner activeOwner = Owner(activePage);
        WorkflowInspectionOwner hiddenOwner = Owner(hiddenPage);

        ShowPage(viewModel, hiddenPage);
        viewModel.WorkflowSession.SelectedIc = isolatedIc;
        ShowPage(viewModel, activePage);
        viewModel.WorkflowSession.SelectedIc = isolatedIc;
        foreach (string workflowId in PageWorkflowIds(activePage))
        {
            policy.DisableWorkflowFor(workflowId, isolatedIc);
        }

        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(activePage, viewModel.SelectedPage);
        Assert.NotEqual(isolatedIc, viewModel.WorkflowSession.SelectedIc);
        Assert.NotEqual(
            isolatedIc,
            viewModel.WorkflowSession.GetWorkflowPageIc(activeOwner));
        Assert.DoesNotContain(isolatedIc, viewModel.WorkflowSession.IcChoices);
        Assert.Equal(
            isolatedIc,
            viewModel.WorkflowSession.GetWorkflowPageIc(hiddenOwner));

        ShowPage(viewModel, hiddenPage);

        Assert.Equal(isolatedIc, viewModel.WorkflowSession.SelectedIc);
        Assert.Contains(isolatedIc, viewModel.WorkflowSession.IcChoices);
    }

    /// <summary>Withdrawing AB and Standard together falls through to General without exposing either stale mode.</summary>
    [Fact]
    public async Task AbAndStandardCompoundWithdrawalFallsBackToGeneralMergeOnly()
    {
        const string icId = "NT51950";
        var policy = new MutableAbCatalogPolicy();
        (
            PresentationHostServices services,
            MainWindowViewModel viewModel,
            AuthoringPortSentinel sentinel) = CreateCatalogRefreshSentinelViewModel(policy);
        CapabilitySelectorPublication original = services.Composition.Capabilities
            .GetSelectorPublication();
        Assert.True(original.IsWorkflowAuthorable(icId, ExperienceIds.StandardMerge));
        Assert.True(original.IsWorkflowAuthorable(icId, ExperienceIds.AbMerge));
        Assert.True(original.IsWorkflowAuthorable(icId, ExperienceIds.GeneralMerge));
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = icId;
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        policy.DisableWorkflowFor(ExperienceIds.AbMerge, icId);
        policy.DisableWorkflowFor(ExperienceIds.StandardMerge, icId);
        sentinel.Arm();
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        CapabilitySelectorPublication refreshed = services.Composition.Capabilities
            .GetSelectorPublication();

        Assert.Equal(icId, viewModel.WorkflowSession.SelectedIc);
        Assert.False(refreshed.IsWorkflowAuthorable(icId, ExperienceIds.AbMerge));
        Assert.False(refreshed.IsWorkflowAuthorable(icId, ExperienceIds.StandardMerge));
        Assert.True(refreshed.IsWorkflowAuthorable(icId, ExperienceIds.GeneralMerge));
        Assert.Equal(ExperienceIds.GeneralMerge, viewModel.Merge.SelectedMergeMode);
        Assert.Contains(ExperienceIds.GeneralMerge, viewModel.Merge.MergeModeChoices);
        Assert.Equal(0, sentinel.ArmedCallCounts[StandardMergeAuthoringPortIndex]);
        Assert.Equal(0, sentinel.ArmedCallCounts[AbMergeAuthoringPortIndex]);
    }

    /// <summary>A fresh token preserves a valid, uncommitted Home modal draft exactly.</summary>
    [Fact]
    public async Task FreshTokenPreservesValidOpenHomeModalDraft()
    {
        var policy = new MutableAbCatalogPolicy();
        (PresentationHostServices services, MainWindowViewModel viewModel) =
            CreateCatalogRefreshViewModel(policy);
        CapabilitySelectorPublication original = services.Composition.Capabilities
            .GetSelectorPublication();
        string committedIc = viewModel.WorkflowSession.GetWorkflowPageIc(
            WorkflowInspectionOwner.Merge);
        string committedNumber = viewModel.WorkflowSession.GetWorkflowPageNumber(
            WorkflowInspectionOwner.Merge);
        string draftIc = original.IcIds.First(icId =>
            original.IsWorkflowAuthorable(icId, ExperienceIds.AbMerge) &&
            original.GetAbMergeTopologyChoices(icId).Any(choice =>
                StringComparer.Ordinal.Equals(
                    choice.Token,
                    IcNumberSelectionTokens.Cascade)));
        ResolutionToken originalToken = original.ResolutionToken;
        viewModel.BeginAbMergeFromHomeCommand.Execute(null);
        viewModel.WorkflowSession.WorkflowContextSetup.SelectedIc = draftIc;
        viewModel.WorkflowSession.WorkflowContextSetup.SelectedNumber =
            IcNumberSelectionTokens.Cascade;

        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        CapabilitySelectorPublication refreshed = services.Composition.Capabilities
            .GetSelectorPublication();

        Assert.NotEqual(originalToken, refreshed.ResolutionToken);
        Assert.True(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.Equal(draftIc, viewModel.WorkflowSession.WorkflowContextSetup.SelectedIc);
        Assert.Equal(
            IcNumberSelectionTokens.Cascade,
            viewModel.WorkflowSession.WorkflowContextSetup.SelectedNumber);
        Assert.Contains(draftIc, viewModel.WorkflowSession.WorkflowContextSetup.IcChoices);
        Assert.Contains(
            viewModel.WorkflowSession.WorkflowContextSetup.NumberChoices,
            choice => StringComparer.Ordinal.Equals(
                choice.Token,
                IcNumberSelectionTokens.Cascade));
        Assert.Equal(
            committedIc,
            viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Merge));
        Assert.Equal(
            committedNumber,
            viewModel.WorkflowSession.GetWorkflowPageNumber(WorkflowInspectionOwner.Merge));
    }

    private static bool IsPageAuthorable(
        CapabilitySelectorPublication publication,
        string icId,
        ShellPage page)
    {
        return PageWorkflowIds(page).Any(workflowId =>
            publication.IsWorkflowAuthorable(icId, workflowId));
    }

    private static (
        PresentationHostServices Services,
        MainWindowViewModel ViewModel,
        AuthoringPortSentinel Sentinel) CreateCatalogRefreshSentinelViewModel(
            MutableAbCatalogPolicy policy)
    {
        PresentationHostServices services = PresentationTestHost.CreateServicesWithCatalogPolicy(
            "0.10.6-catalog-refresh-reconciliation-matrix-test",
            policy.Load);
        var sentinel = AuthoringPortSentinel.Create(services.Composition);
        services = WithAuthoringPorts(services, sentinel);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(
            services,
            ShellLanguage.English);
        _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
        return (services, viewModel, sentinel);
    }

    private static int ReplaceAuthoringPortIndex(string mode)
    {
        return mode switch
        {
            ExperienceIds.DpReplace => DpReplaceAuthoringPortIndex,
            ExperienceIds.CtrlRamReplace => CtrlRamAuthoringPortIndex,
            _ => throw new ArgumentException("Structured Replace mode expected.", nameof(mode)),
        };
    }

    private static IReadOnlyList<string> PageWorkflowIds(ShellPage page)
    {
        return page switch
        {
            ShellPage.Merge =>
            [
                ExperienceIds.StandardMerge,
                ExperienceIds.AbMerge,
                ExperienceIds.GeneralMerge,
            ],
            ShellPage.Replace =>
            [
                ExperienceIds.DpReplace,
                ExperienceIds.CtrlRamReplace,
                ExperienceIds.GeneralReplace,
            ],
            ShellPage.Home or ShellPage.HexEditor => throw new ArgumentException(
                "Workflow page expected.",
                nameof(page)),
            _ => throw new InvalidOperationException("Unknown shell page."),
        };
    }

    private static WorkflowInspectionOwner Owner(ShellPage page)
    {
        return page switch
        {
            ShellPage.Merge => WorkflowInspectionOwner.Merge,
            ShellPage.Replace => WorkflowInspectionOwner.Replace,
            ShellPage.Home or ShellPage.HexEditor => throw new ArgumentException(
                "Workflow page expected.",
                nameof(page)),
            _ => throw new InvalidOperationException("Unknown shell page."),
        };
    }

    private static void ShowPage(MainWindowViewModel viewModel, ShellPage page)
    {
        switch (page)
        {
            case ShellPage.Merge:
                viewModel.ShowMergeCommand.Execute(null);
                break;
            case ShellPage.Replace:
                viewModel.ShowReplaceCommand.Execute(null);
                break;
            case ShellPage.Home:
            case ShellPage.HexEditor:
                throw new ArgumentException("Workflow page expected.", nameof(page));
            default:
                throw new InvalidOperationException("Unknown shell page.");
        }
    }
}

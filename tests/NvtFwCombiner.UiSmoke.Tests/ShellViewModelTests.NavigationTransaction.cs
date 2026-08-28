using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>A failed forward or back activation keeps source inputs and leaves history retryable.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedActivationKeepsSourceInputsAndRestoresRetryableHistory(bool isBack)
    {
        ShellPage selectedPage = ShellPage.Home;
        bool hasSelectedInputs = false;
        bool failActivation = false;
        int clearCount = 0;
        ShellPage? clearedPage = null;
        ShellNavigationViewModel? navigation = null;
        navigation = new ShellNavigationViewModel(new ShellNavigationBindings(
            () => selectedPage,
            () => ShellTextResources.For(ShellLanguage.English),
            page => hasSelectedInputs && page == selectedPage,
            static () => { },
            page =>
            {
                clearCount++;
                clearedPage = page;
                hasSelectedInputs = false;
            },
            page =>
            {
                if (failActivation)
                {
                    throw new InvalidOperationException("Injected destination activation failure.");
                }

                selectedPage = page;
                navigation!.UpdateState();
            },
            static page => page.ToString(),
            static () => { }));

        navigation.NavigateToPage(ShellPage.Merge);
        if (isBack)
        {
            navigation.NavigateToPage(ShellPage.Replace);
        }

        ShellPage source = selectedPage;
        ShellPage target = isBack ? ShellPage.Merge : ShellPage.Replace;
        hasSelectedInputs = true;
        failActivation = true;
        RequestNavigation(navigation, target, isBack);
        Assert.True(navigation.IsNavigationClearConfirmationOpen);

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => navigation.ConfirmNavigationAndClearCommand.Execute(null));

        Assert.Equal("Injected destination activation failure.", failure.Message);
        Assert.Equal(source, selectedPage);
        Assert.Equal(0, clearCount);
        Assert.Null(clearedPage);
        Assert.False(navigation.IsNavigationClearConfirmationOpen);
        Assert.Equal($"Home > {source}", navigation.NavigationPath);

        failActivation = false;
        RequestNavigation(navigation, target, isBack);
        Assert.True(navigation.IsNavigationClearConfirmationOpen);
        navigation.ConfirmNavigationAndClearCommand.Execute(null);

        Assert.Equal(target, selectedPage);
        Assert.Equal(1, clearCount);
        Assert.Equal(source, clearedPage);
        Assert.False(hasSelectedInputs);

        navigation.GoBackCommand.Execute(null);
        Assert.Equal(isBack ? ShellPage.Home : ShellPage.Merge, selectedPage);
    }

    /// <summary>A source-clear failure after destination activation restores source page and retryable history.</summary>
    [Fact]
    public void PostActivationSourceClearFailureRollsBackDestinationAndHistory()
    {
        ShellPage selectedPage = ShellPage.Home;
        bool hasSelectedInputs = false;
        bool failClear = true;
        int successfulClearCount = 0;
        ShellNavigationViewModel? navigation = null;
        navigation = new ShellNavigationViewModel(new ShellNavigationBindings(
            () => selectedPage,
            () => ShellTextResources.For(ShellLanguage.English),
            page => hasSelectedInputs && page == selectedPage,
            static () => { },
            _ =>
            {
                if (failClear)
                {
                    throw new InvalidOperationException("Injected source-clear failure.");
                }

                successfulClearCount++;
                hasSelectedInputs = false;
            },
            page =>
            {
                selectedPage = page;
                navigation!.UpdateState();
            },
            static page => page.ToString(),
            static () => { }));
        navigation.NavigateToPage(ShellPage.Merge);
        hasSelectedInputs = true;

        navigation.NavigateToPage(ShellPage.Replace);
        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => navigation.ConfirmNavigationAndClearCommand.Execute(null));

        Assert.Equal("Injected source-clear failure.", failure.Message);
        Assert.Equal(ShellPage.Merge, selectedPage);
        Assert.True(hasSelectedInputs);
        Assert.Equal(0, successfulClearCount);
        Assert.Equal("Home > Merge", navigation.NavigationPath);

        failClear = false;
        navigation.NavigateToPage(ShellPage.Replace);
        navigation.ConfirmNavigationAndClearCommand.Execute(null);

        Assert.Equal(ShellPage.Replace, selectedPage);
        Assert.False(hasSelectedInputs);
        Assert.Equal(1, successfulClearCount);
        Assert.Equal("Home > Replace", navigation.NavigationPath);
    }

    /// <summary>Both General defaults are acquired before a Merge page activation can commit.</summary>
    [Fact]
    public void GeneralDefaultsFailureLeavesHomeAndPublishedSelectorUnchanged()
    {
        var policy = new MutableAbCatalogPolicy();
        (
            _,
            MainWindowViewModel viewModel,
            AuthoringPortSentinel sentinel) = CreateCatalogRefreshSentinelViewModel(policy);
        ResolutionToken? retainedToken = viewModel.WorkflowSession.SelectorResolutionToken;
        string retainedMergeIc = viewModel.WorkflowSession.GetWorkflowPageIc(
            WorkflowInspectionOwner.Merge);
        string retainedLength = viewModel.Merge.GeneralMergeOutputLength;
        string retainedFill = viewModel.Merge.GeneralMergeOutputFillByte;
        sentinel.ArmGeneralFailure(nameof(IGeneralAuthoring.GetDefaultOutputFillByte));

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => viewModel.ShowMergeCommand.Execute(null));

        Assert.Equal("Injected GetDefaultOutputFillByte failure.", failure.Message);
        Assert.Equal(ShellPage.Home, viewModel.SelectedPage);
        Assert.False(viewModel.WorkflowSession.IsWorkflowLoaded);
        Assert.Equal(retainedToken, viewModel.WorkflowSession.SelectorResolutionToken);
        Assert.Equal(
            retainedMergeIc,
            viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Merge));
        Assert.Equal(retainedLength, viewModel.Merge.GeneralMergeOutputLength);
        Assert.Equal(retainedFill, viewModel.Merge.GeneralMergeOutputFillByte);
    }

    /// <summary>A failed active rebuild cannot publish the new selector or invalidate the retained inspection.</summary>
    [Fact]
    public async Task CatalogActiveRebuildFailureKeepsSelectorAndInspectionGeneration()
    {
        var policy = new MutableAbCatalogPolicy();
        (
            PresentationHostServices services,
            MainWindowViewModel viewModel,
            AuthoringPortSentinel sentinel) = CreateCatalogRefreshSentinelViewModel(policy);
        CapabilitySelectorPublication original = services.Composition.Capabilities
            .GetSelectorPublication();
        string icId = original.IcIds.First(ic =>
            original.IsWorkflowAuthorable(ic, ExperienceIds.StandardMerge));
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = icId;
        viewModel.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
        ResolutionToken? retainedToken = viewModel.WorkflowSession.SelectorResolutionToken;
        WorkflowInspectionLifecycle retainedInspection = viewModel.Merge.Inspection;
        WorkflowInspectionAttemptState retainedState = retainedInspection.State;
        policy.DisableAbFor(original.AbMergeIcIds[0]);
        sentinel.ArmStandardFailure(nameof(IStandardMergeAuthoring.GetAuthoringSnapshot));

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null));
        CapabilitySelectorPublication refreshed = services.Composition.Capabilities
            .GetSelectorPublication();

        Assert.Equal("Injected GetAuthoringSnapshot failure.", failure.Message);
        Assert.NotEqual(retainedToken, refreshed.ResolutionToken);
        Assert.Equal(retainedToken, viewModel.WorkflowSession.SelectorResolutionToken);
        Assert.Equal(icId, viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(ExperienceIds.StandardMerge, viewModel.Merge.SelectedMergeMode);
        Assert.Same(retainedInspection, viewModel.Merge.Inspection);
        Assert.Equal(retainedState, retainedInspection.State);
    }

    /// <summary>A failed first DP snapshot read cannot publish any part of the refreshed catalog state.</summary>
    [Fact]
    public async Task CatalogDpSnapshotFirstReadFailureKeepsCompleteReplaceState()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-dp-refresh-first-read");
        var policy = new MutableAbCatalogPolicy();
        (
            PresentationHostServices services,
            MainWindowViewModel viewModel,
            AuthoringPortSentinel sentinel) = CreateCatalogRefreshSentinelViewModel(policy);
        CapabilitySelectorPublication original = services.Composition.Capabilities
            .GetSelectorPublication();
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51928";
        viewModel.Replace.SelectedReplaceMode = ExperienceIds.DpReplace;
        viewModel.SetSlotFile(
            CompositionSlotIds.ReplaceBase,
            workspace.Write("reference.bin", new byte[0x40000]));
        viewModel.SetSlotFile(
            CompositionSlotIds.ReplaceDp,
            workspace.Write("initial-code.bin", CreatePattern(0x40000, 0x41)));
        await viewModel.Replace.Inspection.ActiveTask.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        ResolutionToken? retainedToken = viewModel.WorkflowSession.SelectorResolutionToken;
        string[] retainedPaths = [.. viewModel.Replace.ReplaceSlots
            .Where(static slot => slot.HasFile)
            .Select(static slot => slot.FilePath!)];
        bool retainedCanBuild = viewModel.Replace.CanBuildReplace;
        policy.DisableAbFor(original.AbMergeIcIds[0]);
        sentinel.ArmDpFailure(nameof(IDpReplaceAuthoring.GetAuthoringSnapshot));

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null));

        Assert.Equal("Injected GetAuthoringSnapshot failure.", failure.Message);
        Assert.Equal(retainedToken, viewModel.WorkflowSession.SelectorResolutionToken);
        Assert.Equal("NT51928", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(ExperienceIds.DpReplace, viewModel.Replace.SelectedReplaceMode);
        Assert.Equal(retainedPaths, viewModel.Replace.ReplaceSlots
            .Where(static slot => slot.HasFile)
            .Select(static slot => slot.FilePath));
        Assert.Equal(retainedCanBuild, viewModel.Replace.CanBuildReplace);
        Assert.Equal(1, sentinel.ArmedCallCounts[DpReplaceAuthoringPortIndex]);
    }

    /// <summary>Slot construction and readiness consume one staged DP snapshot; a second read is forbidden.</summary>
    [Fact]
    public async Task CatalogDpRefreshUsesExactlyOneSnapshotForRetainedSelections()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-dp-refresh-single-read");
        var policy = new MutableAbCatalogPolicy();
        (
            PresentationHostServices services,
            MainWindowViewModel viewModel,
            AuthoringPortSentinel sentinel) = CreateCatalogRefreshSentinelViewModel(policy);
        CapabilitySelectorPublication original = services.Composition.Capabilities
            .GetSelectorPublication();
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51928";
        viewModel.Replace.SelectedReplaceMode = ExperienceIds.DpReplace;
        viewModel.SetSlotFile(
            CompositionSlotIds.ReplaceBase,
            workspace.Write("reference.bin", new byte[0x40000]));
        viewModel.SetSlotFile(
            CompositionSlotIds.ReplaceDp,
            workspace.Write("initial-code.bin", CreatePattern(0x40000, 0x51)));
        await viewModel.Replace.Inspection.ActiveTask.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        policy.DisableAbFor(original.AbMergeIcIds[0]);
        sentinel.ArmDpFailure(nameof(IDpReplaceAuthoring.GetAuthoringSnapshot), invocation: 2);

        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(1, sentinel.ArmedCallCounts[DpReplaceAuthoringPortIndex]);
        Assert.Equal("NT51928", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(ExperienceIds.DpReplace, viewModel.Replace.SelectedReplaceMode);
        Assert.Contains(viewModel.Replace.ReplaceSlots, static slot =>
            slot.SlotId == CompositionSlotIds.ReplaceBase && slot.HasFile);
        Assert.Contains(viewModel.Replace.ReplaceSlots, static slot =>
            slot.SlotId == CompositionSlotIds.ReplaceDp && slot.HasFile);
    }

    /// <summary>Clearing a hidden General Merge page performs no authoring call with the destination IC.</summary>
    [Fact]
    public async Task HiddenGeneralMergeBulkClearDoesNotRedispatchAuthoring()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-navigation-hidden-general-clear");
        PresentationHostServices original = PresentationTestHost.CreateServices(
            "0.10.6-navigation-hidden-general-clear-test");
        var sentinel = AuthoringPortSentinel.Create(original.Composition);
        PresentationHostServices services = WithAuthoringPorts(original, sentinel);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(
            services,
            ShellLanguage.English);
        _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
        string expectedReplaceIc = viewModel.WorkflowSession.SelectedIc;

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        GeneralMergeMappingViewModel first = Assert.Single(viewModel.Merge.GeneralMergeMappings);
        first.SourceStartAddress = "0x0";
        first.TargetStartAddress = "0x0";
        first.Length = "0x1";
        viewModel.SetSlotFile(first.MappingId, workspace.Write("first.bin", [0x11, 0x12]));
        viewModel.Merge.AddGeneralMergeMappingCommand.Execute(null);
        GeneralMergeMappingViewModel second = viewModel.Merge.GeneralMergeMappings[1];
        second.SourceStartAddress = "0x0";
        second.TargetStartAddress = "0x2";
        second.Length = "0x1";
        viewModel.SetSlotFile(second.MappingId, workspace.Write("second.bin", [0x21, 0x22]));
        await viewModel.Merge.Inspection.ActiveTask.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        viewModel.ShowReplaceCommand.Execute(null);
        Assert.True(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        sentinel.Arm();
        viewModel.Navigation.ConfirmNavigationAndClearCommand.Execute(null);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.Equal(expectedReplaceIc, viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(0, sentinel.ArmedCallCounts[3]);
        Assert.All(viewModel.Merge.GeneralMergeMappings, static mapping => Assert.False(mapping.HasFile));
    }

    private static void RequestNavigation(
        ShellNavigationViewModel navigation,
        ShellPage target,
        bool isBack)
    {
        if (isBack)
        {
            navigation.GoBackCommand.Execute(null);
            return;
        }

        navigation.NavigateToPage(target);
    }
}

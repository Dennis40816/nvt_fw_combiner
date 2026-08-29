using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>A same-token refresh is a strict no-op for a verified Standard Merge session.</summary>
    [Fact]
    public async Task DirectSameTokenRefreshDoesNotReinspectOrRebuildVerifiedStandardMerge()
    {
        (PresentationHostServices services, MainWindowViewModel viewModel, BlockingInspectionReader reader) =
            CreateCatalogInspectionViewModel("0.10.6-same-token-no-op-test");
        await LoadVerifiedStandardMergeAsync(viewModel);
        ResolutionToken token = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;
        int batchCount = reader.BatchCount;
        string?[] retainedPaths = [.. viewModel.Merge.MergeSlots.Select(static slot => slot.FilePath)];
        object?[] retainedProjections =
            [.. viewModel.Merge.MergeSlots.Select(static slot => slot.CurrentInspectionProjection)];
        WorkflowInspectionLifecycle lifecycle = viewModel.Merge.Inspection;
        Task activeTask = lifecycle.ActiveTask;
        WorkflowInspectionAttemptState state = lifecycle.State;
        int selectorNotifications = 0;
        viewModel.WorkflowSession.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(WorkflowSessionPresentationViewModel.IcChoices) or
                nameof(WorkflowSessionPresentationViewModel.SelectedIc))
            {
                selectorNotifications++;
            }
        };

        viewModel.WorkflowSession.RefreshCanonicalCatalogState();

        Assert.Equal(
            token,
            services.Composition.Capabilities.GetSelectorPublication().ResolutionToken);
        Assert.Equal(batchCount, reader.BatchCount);
        Assert.Equal(retainedPaths, viewModel.Merge.MergeSlots.Select(static slot => slot.FilePath));
        Assert.Same(lifecycle, viewModel.Merge.Inspection);
        Assert.Same(activeTask, lifecycle.ActiveTask);
        Assert.Equal(state, lifecycle.State);
        Assert.Equal(WorkflowInspectionAttemptState.Succeeded, lifecycle.State);
        Assert.Equal(0, selectorNotifications);
        Assert.Equal(retainedProjections.Length, viewModel.Merge.MergeSlots.Count);
        for (int index = 0; index < retainedProjections.Length; index++)
        {
            Assert.Same(
                retainedProjections[index],
                viewModel.Merge.MergeSlots[index].CurrentInspectionProjection);
        }
        Assert.True(viewModel.Merge.CanBuildMerge, viewModel.Merge.MergeReadinessStatus);
    }

    /// <summary>A fresh token fail-closes active Standard Merge until retained files are reinspected.</summary>
    [Fact]
    public async Task FreshTokenReloadReinspectsActiveStandardMergeBeforeBuildReturns()
    {
        (PresentationHostServices services, MainWindowViewModel viewModel, BlockingInspectionReader reader) =
            CreateCatalogInspectionViewModel("0.10.6-hidden-standard-rebind-test");
        await LoadVerifiedStandardMergeAsync(viewModel);
        int verifiedBatchCount = reader.BatchCount;
        reader.BlockNextBatch();

        ResolutionToken originalToken = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        ResolutionToken refreshedToken = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;

        Assert.NotEqual(originalToken, refreshedToken);
        try
        {
            await reader.InspectionEntered.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            Assert.False(viewModel.Merge.CanBuildMerge);
            Assert.True(viewModel.Merge.Inspection.IsRunning);
        }
        finally
        {
            _ = reader.ReleaseInspection.TrySetResult();
        }

        await viewModel.Merge.Inspection.ActiveTask;
        Assert.Equal(verifiedBatchCount + 1, reader.BatchCount);
        Assert.True(viewModel.Merge.CanBuildMerge, viewModel.Merge.MergeReadinessStatus);
        Assert.All(
            viewModel.Merge.MergeSlots.Where(static slot => !slot.IsOptional),
            slot => Assert.Equal(
                refreshedToken,
                Assert.IsType<AuthoringInputSlotStatus>(
                    slot.CurrentInspectionProjection?.InputSlotStatus).ResolutionToken));
    }

    /// <summary>A fresh token leaves hidden Standard files dormant until that mode is selected again.</summary>
    [Fact]
    public async Task FreshTokenReloadReinspectsHiddenStandardModeWhenReactivated()
    {
        (PresentationHostServices services, MainWindowViewModel viewModel, BlockingInspectionReader reader) =
            CreateCatalogInspectionViewModel("0.10.6-hidden-standard-mode-rebind-test");
        await LoadVerifiedStandardMergeAsync(viewModel);
        FirmwareSlotViewModel[] standardSlots =
            [.. viewModel.Merge.StandardMergeSlots.Where(static slot => slot.HasFile)];
        string?[] retainedPaths = [.. standardSlots.Select(static slot => slot.FilePath)];
        int verifiedBatchCount = reader.BatchCount;
        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        reader.BlockNextBatch();

        ResolutionToken originalToken = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        ResolutionToken refreshedToken = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;

        Assert.NotEqual(originalToken, refreshedToken);
        Assert.Equal(ExperienceIds.GeneralMerge, viewModel.Merge.SelectedMergeMode);
        Assert.Equal(verifiedBatchCount, reader.BatchCount);
        Assert.Equal(retainedPaths, standardSlots.Select(static slot => slot.FilePath));
        Assert.False(reader.InspectionEntered.Task.IsCompleted);
        viewModel.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
        try
        {
            await reader.InspectionEntered.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            Assert.False(viewModel.Merge.CanBuildMerge);
            Assert.True(viewModel.Merge.Inspection.IsRunning);
        }
        finally
        {
            _ = reader.ReleaseInspection.TrySetResult();
        }

        await viewModel.Merge.Inspection.ActiveTask;
        Assert.Equal(verifiedBatchCount + 1, reader.BatchCount);
        Assert.True(viewModel.Merge.CanBuildMerge, viewModel.Merge.MergeReadinessStatus);
        Assert.All(
            standardSlots.Where(static slot => !slot.IsOptional),
            slot => Assert.Equal(
                refreshedToken,
                Assert.IsType<AuthoringInputSlotStatus>(
                    slot.CurrentInspectionProjection?.InputSlotStatus).ResolutionToken));
    }

    /// <summary>An ordinary same-token mode round trip reuses the accepted Standard projections.</summary>
    [Fact]
    public async Task OrdinaryStandardModeRoundTripKeepsAcceptedProjectionWithoutReinspection()
    {
        (PresentationHostServices services, MainWindowViewModel viewModel, BlockingInspectionReader reader) =
            CreateCatalogInspectionViewModel("0.10.6-standard-round-trip-test");
        await LoadVerifiedStandardMergeAsync(viewModel);
        ResolutionToken token = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;
        int verifiedBatchCount = reader.BatchCount;
        FirmwareSlotViewModel[] standardSlots = [.. viewModel.Merge.StandardMergeSlots];
        object?[] projections =
            [.. standardSlots.Select(static slot => slot.CurrentInspectionProjection)];

        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        viewModel.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
        await AwaitStableInspectionAsync(viewModel.Merge.Inspection);

        Assert.Equal(verifiedBatchCount, reader.BatchCount);
        Assert.Equal(
            token,
            services.Composition.Capabilities.GetSelectorPublication().ResolutionToken);
        Assert.True(viewModel.Merge.CanBuildMerge, viewModel.Merge.MergeReadinessStatus);
        for (int index = 0; index < standardSlots.Length; index++)
        {
            Assert.Same(projections[index], standardSlots[index].CurrentInspectionProjection);
        }
    }

    /// <summary>A catalog-bound typed projection without a token is stale and is reinspected on activation.</summary>
    [Fact]
    public async Task TokenlessStandardProjectionIsReinspectedWhenStandardIsReactivated()
    {
        (PresentationHostServices services, MainWindowViewModel viewModel, BlockingInspectionReader reader) =
            CreateCatalogInspectionViewModel("0.10.6-tokenless-standard-rebind-test");
        await LoadVerifiedStandardMergeAsync(viewModel);
        ResolutionToken token = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;
        FirmwareSlotViewModel tokenlessSlot = viewModel.Merge.StandardMergeSlots
            .First(static slot => slot.HasFile && !slot.IsOptional);
        FirmwareInspectionSnapshot accepted = Assert.IsType<FirmwareInspectionSnapshot>(
            tokenlessSlot.CurrentInspectionProjection);
        tokenlessSlot.SetCurrentInspectionProjection(accepted with
        {
            InputSlotStatus = null,
            InputSlotCatalog = null,
        });
        int verifiedBatchCount = reader.BatchCount;
        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        reader.BlockNextBatch();

        viewModel.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
        try
        {
            await reader.InspectionEntered.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            Assert.False(viewModel.Merge.CanBuildMerge);
        }
        finally
        {
            _ = reader.ReleaseInspection.TrySetResult();
        }

        await AwaitStableInspectionAsync(viewModel.Merge.Inspection);
        Assert.Equal(verifiedBatchCount + 1, reader.BatchCount);
        Assert.Equal(token, ProjectionToken(tokenlessSlot.CurrentInspectionProjection));
        Assert.True(viewModel.Merge.CanBuildMerge, viewModel.Merge.MergeReadinessStatus);
    }

    /// <summary>An active AB refresh cannot consume the hidden Standard mode's fresh-token rebind.</summary>
    [Fact]
    public async Task FreshTokenWhileAbActiveReinspectsHiddenStandardWhenReactivated()
    {
        (PresentationHostServices services, MainWindowViewModel viewModel, BlockingInspectionReader reader) =
            CreateCatalogInspectionViewModel("0.10.6-ab-active-standard-rebind-test");
        await LoadVerifiedStandardMergeAsync(viewModel, "51950");
        FirmwareSlotViewModel[] standardSlots = [.. viewModel.Merge.StandardMergeSlots];
        await LoadVerifiedAbMergeAsync(viewModel);
        int verifiedBatchCount = reader.BatchCount;
        ResolutionToken originalToken = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;

        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        await AwaitStableInspectionAsync(viewModel.Merge.Inspection);
        ResolutionToken refreshedToken = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;

        Assert.NotEqual(originalToken, refreshedToken);
        Assert.Equal(verifiedBatchCount + 1, reader.BatchCount);
        Assert.All(
            viewModel.Merge.AbMergeSlots.Where(static slot => slot.HasFile),
            slot => Assert.Equal(
                refreshedToken,
                Assert.IsType<AuthoringInputSlotStatus>(
                    slot.CurrentInspectionProjection?.InputSlotStatus).ResolutionToken));
        int refreshedAbBatchCount = reader.BatchCount;
        reader.BlockNextBatch();

        viewModel.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
        try
        {
            await reader.InspectionEntered.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            Assert.Equal(
                standardSlots.Where(static slot => slot.HasFile).Select(static slot => slot.SlotId),
                reader.LastInspectionIds);
            Assert.False(viewModel.Merge.CanBuildMerge);
        }
        finally
        {
            _ = reader.ReleaseInspection.TrySetResult();
        }

        await AwaitStableInspectionAsync(viewModel.Merge.Inspection);
        Assert.Equal(refreshedAbBatchCount + 1, reader.BatchCount);
        Assert.True(viewModel.Merge.CanBuildMerge, viewModel.Merge.MergeReadinessStatus);
        Assert.All(
            standardSlots.Where(static slot => !slot.IsOptional),
            slot => Assert.Equal(
                refreshedToken,
                Assert.IsType<AuthoringInputSlotStatus>(
                    slot.CurrentInspectionProjection?.InputSlotStatus).ResolutionToken));
    }

    /// <summary>A cancelled fresh-token Standard batch is retried when Standard is selected again.</summary>
    [Fact]
    public async Task CancelledFreshStandardReinspectionRetriesOnReactivation()
    {
        (PresentationHostServices services, MainWindowViewModel viewModel, BlockingInspectionReader reader) =
            CreateCatalogInspectionViewModel("0.10.6-cancelled-standard-rebind-test");
        await LoadVerifiedStandardMergeAsync(viewModel);
        int verifiedBatchCount = reader.BatchCount;
        reader.BlockNextBatch();

        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        ResolutionToken refreshedToken = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;
        await reader.InspectionEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        WorkflowInspectionLifecycle standardLifecycle = viewModel.Merge.Inspection;
        Task cancelledAttempt = standardLifecycle.ActiveTask;

        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        _ = reader.ReleaseInspection.TrySetResult();
        await cancelledAttempt.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, standardLifecycle.State);

        viewModel.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
        await AwaitStableInspectionAsync(viewModel.Merge.Inspection);

        Assert.Equal(verifiedBatchCount + 2, reader.BatchCount);
        Assert.True(viewModel.Merge.CanBuildMerge, viewModel.Merge.MergeReadinessStatus);
        Assert.All(
            viewModel.Merge.StandardMergeSlots.Where(static slot => !slot.IsOptional),
            slot => Assert.Equal(
                refreshedToken,
                Assert.IsType<AuthoringInputSlotStatus>(
                    slot.CurrentInspectionProjection?.InputSlotStatus).ResolutionToken));
    }

    /// <summary>A cancelled General Replace Base refresh recovers through the next Replace context.</summary>
    [Fact]
    public async Task CancelledFreshGeneralReplaceBaseReinspectionRecoversWithoutLosingMapping()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-cancelled-general-replace");
        (PresentationHostServices services, MainWindowViewModel viewModel, BlockingInspectionReader reader) =
            CreateCatalogInspectionViewModel("0.10.6-cancelled-general-replace-rebind-test");
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, ExperienceIds.GeneralReplace);
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x26));
        string replacementPath = workspace.Write("replacement.bin", [0xA5, 0x5A]);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            basePath,
            TestContext.Current.CancellationToken);
        GeneralReplaceMappingViewModel mapping =
            Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "0x3E020";
        mapping.Length = "0x2";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            mapping.MappingId,
            replacementPath,
            TestContext.Current.CancellationToken);
        await AwaitStableInspectionAsync(viewModel.Replace.Inspection);
        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
        int verifiedBatchCount = reader.BatchCount;
        ResolutionToken originalToken = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;
        reader.BlockNextBatch();

        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        ResolutionToken refreshedToken = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;
        Assert.NotEqual(originalToken, refreshedToken);
        await reader.InspectionEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        WorkflowInspectionLifecycle generalLifecycle = viewModel.Replace.Inspection;
        Task cancelledAttempt = generalLifecycle.ActiveTask;

        viewModel.Replace.SelectedReplaceMode = ExperienceIds.DpReplace;
        _ = reader.ReleaseInspection.TrySetResult();
        await cancelledAttempt.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        await AwaitStableInspectionAsync(viewModel.Replace.Inspection);
        Assert.Equal(WorkflowInspectionAttemptState.Cancelled, generalLifecycle.State);
        Assert.Equal(verifiedBatchCount + 2, reader.BatchCount);
        Assert.Equal(
            refreshedToken,
            ProjectionToken(viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection));

        int recoveredBatchCount = reader.BatchCount;
        viewModel.Replace.SelectedReplaceMode = ExperienceIds.GeneralReplace;
        await AwaitStableInspectionAsync(viewModel.Replace.Inspection);

        Assert.Equal(recoveredBatchCount, reader.BatchCount);
        Assert.Equal(basePath, viewModel.Replace.ReplaceBaseSlot.FilePath);
        Assert.Equal(replacementPath, mapping.FilePath);
        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
    }

    /// <summary>Confirmed page navigation clears DP state and a later token cannot revive it.</summary>
    [Fact]
    public async Task ConfirmedNavigationClearsDpAndFreshTokenDoesNotReviveStaleReplaceSession()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-hidden-dp-rebind");
        (PresentationHostServices services, MainWindowViewModel viewModel, BlockingInspectionReader reader) =
            CreateCatalogInspectionViewModel("0.10.6-hidden-dp-rebind-test");
        viewModel.WorkflowSession.SelectedIc = "NT51928";
        OpenReplace(viewModel, ExperienceIds.DpReplace);
        viewModel.SetSlotFile(
            CompositionSlotIds.ReplaceBase,
            workspace.Write("reference.bin", new byte[0x40000]));
        viewModel.SetSlotFile(
            CompositionSlotIds.ReplaceDp,
            workspace.Write("initial-code.bin", CreatePattern(0x40000, 0x41)));
        await viewModel.Replace.Inspection.ActiveTask;
        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
        viewModel.ShowMergeCommand.Execute(null);
        Assert.True(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        viewModel.Navigation.ConfirmNavigationAndClearCommand.Execute(null);
        Assert.Equal(ShellPage.Merge, viewModel.SelectedPage);
        Assert.False(viewModel.Replace.ReplaceBaseSlot.HasFile);
        Assert.All(viewModel.Replace.ReplaceSlots, static slot => Assert.False(slot.HasFile));
        int clearedBatchCount = reader.BatchCount;

        ResolutionToken originalToken = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        ResolutionToken refreshedToken = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;

        Assert.NotEqual(originalToken, refreshedToken);
        Assert.Equal(clearedBatchCount, reader.BatchCount);
        viewModel.ShowReplaceCommand.Execute(null);
        Assert.Equal(ShellPage.Replace, viewModel.SelectedPage);
        await AwaitStableInspectionAsync(viewModel.Replace.Inspection);
        Assert.Equal(clearedBatchCount, reader.BatchCount);
        Assert.False(viewModel.Replace.Inspection.IsRunning);
        Assert.False(viewModel.Replace.CanBuildReplace);
        Assert.Null(viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection);
        Assert.All(
            viewModel.Replace.ReplaceSlots,
            static slot => Assert.Null(slot.CurrentInspectionProjection));
    }

    /// <summary>A zero-authorable publication revokes an active build-ready General Merge session.</summary>
    [Fact]
    public async Task ZeroAuthorableRefreshRevokesReadyActiveGeneralMergeSession()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-merge-catalog-revoke");
        var policy = new MutableAbCatalogPolicy();
        (_, MainWindowViewModel viewModel) = CreateCatalogRefreshViewModel(policy);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        viewModel.Merge.GeneralMergeOutputLength = "0x10";
        GeneralMergeMappingViewModel mergeMapping =
            Assert.Single(viewModel.Merge.GeneralMergeMappings);
        mergeMapping.SourceStartAddress = "0x0";
        mergeMapping.TargetStartAddress = "0x4";
        mergeMapping.Length = "0x4";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            mergeMapping.MappingId,
            workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]),
            TestContext.Current.CancellationToken);
        await AwaitStableInspectionAsync(viewModel.Merge.Inspection);
        Assert.True(viewModel.Merge.CanBuildMerge, viewModel.Merge.MergeReadinessStatus);

        policy.DisableEveryRoute();
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.WorkflowSession.IcChoices);
        Assert.False(viewModel.Merge.CanBuildMerge);
        Assert.False(viewModel.Merge.PreviewMergeCommand.CanExecute(null));
    }

    /// <summary>A zero-authorable publication revokes an active build-ready General Replace session.</summary>
    [Fact]
    public async Task ZeroAuthorableRefreshRevokesReadyActiveGeneralReplaceSession()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace-catalog-revoke");
        var policy = new MutableAbCatalogPolicy();
        (_, MainWindowViewModel viewModel) = CreateCatalogRefreshViewModel(policy);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, ExperienceIds.GeneralReplace);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            workspace.Write("base.bin", CreatePattern(0x40000, 0x26)),
            TestContext.Current.CancellationToken);
        GeneralReplaceMappingViewModel mapping =
            Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "0x3E020";
        mapping.Length = "0x2";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            mapping.MappingId,
            workspace.Write("replacement.bin", [0xA5, 0x5A]),
            TestContext.Current.CancellationToken);
        await AwaitStableInspectionAsync(viewModel.Replace.Inspection);
        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);

        policy.DisableEveryRoute();
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.WorkflowSession.IcChoices);
        Assert.False(viewModel.Replace.CanBuildReplace);
        Assert.False(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
    }

    /// <summary>A fresh token reinspects only the active General Replace Base before rebuilding.</summary>
    [Fact]
    public async Task FreshTokenReloadReinspectsReadyActiveGeneralReplaceBaseBeforeBuildReturns()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace-rebind");
        (PresentationHostServices services, MainWindowViewModel viewModel, BlockingInspectionReader reader) =
            CreateCatalogInspectionViewModel("0.10.6-general-replace-rebind-test");
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, ExperienceIds.GeneralReplace);
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x26));
        string replacementPath = workspace.Write("replacement.bin", [0xA5, 0x5A]);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            basePath,
            TestContext.Current.CancellationToken);
        GeneralReplaceMappingViewModel mapping =
            Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "0x3E020";
        mapping.Length = "0x2";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            mapping.MappingId,
            replacementPath,
            TestContext.Current.CancellationToken);
        await AwaitStableInspectionAsync(viewModel.Replace.Inspection);
        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
        object acceptedProjection = Assert.IsType<FirmwareInspectionSnapshot>(
            viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection);
        int verifiedBatchCount = reader.BatchCount;
        ResolutionToken originalToken = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;
        reader.BlockNextBatch();

        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        ResolutionToken refreshedToken = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;

        Assert.NotEqual(originalToken, refreshedToken);
        try
        {
            await reader.InspectionEntered.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            Assert.Equal([CompositionSlotIds.ReplaceBase], reader.LastInspectionIds);
            Assert.Equal(basePath, viewModel.Replace.ReplaceBaseSlot.FilePath);
            Assert.Equal(replacementPath, mapping.FilePath);
            Assert.Null(viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection);
            Assert.False(viewModel.Replace.CanBuildReplace);
        }
        finally
        {
            _ = reader.ReleaseInspection.TrySetResult();
        }

        await AwaitStableInspectionAsync(viewModel.Replace.Inspection);
        Assert.Equal(verifiedBatchCount + 1, reader.BatchCount);
        Assert.NotSame(
            acceptedProjection,
            Assert.IsType<FirmwareInspectionSnapshot>(
                viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection));
        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
    }

    /// <summary>General Replace discovery owns a legitimate tokenless Base projection and reuses it.</summary>
    [Fact]
    public async Task GeneralReplaceReactivationKeepsTokenlessBaseProjectionWithoutReinspection()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace-tokenless");
        (_, MainWindowViewModel viewModel, BlockingInspectionReader reader) =
            CreateCatalogInspectionViewModel("0.10.6-general-replace-tokenless-test");
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, ExperienceIds.GeneralReplace);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            workspace.Write("base.bin", CreatePattern(0x40000, 0x26)),
            TestContext.Current.CancellationToken);
        await AwaitStableInspectionAsync(viewModel.Replace.Inspection);
        FirmwareInspectionSnapshot accepted = Assert.IsType<FirmwareInspectionSnapshot>(
            viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection);
        Assert.Null(accepted.InputSlotStatus?.ResolutionToken ??
            accepted.InputSlotCatalog?.ResolutionToken);
        int verifiedBatchCount = reader.BatchCount;

        viewModel.WorkflowSession.ApplyAcceptedReplaceModeContext();
        await AwaitStableInspectionAsync(viewModel.Replace.Inspection);

        Assert.Equal(verifiedBatchCount, reader.BatchCount);
        Assert.Same(accepted, viewModel.Replace.ReplaceBaseSlot.CurrentInspectionProjection);
    }

    private static (
        PresentationHostServices Services,
        MainWindowViewModel ViewModel,
        BlockingInspectionReader Reader) CreateCatalogInspectionViewModel(string version)
    {
        PresentationHostServices services = PresentationTestHost.CreateServices(version);
        var reader = new BlockingInspectionReader(
            (BuiltInFirmwareInspection)services.Composition.FirmwareInspection);
        var viewModel = new MainWindowViewModel(
            "test",
            version,
            ShellLanguage.English,
            services,
            new DelegatingFirmwareInspection(
                services.Composition.FirmwareInspection,
                batchReader: reader.Read));
        _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
        return (services, viewModel, reader);
    }

    private static async Task LoadVerifiedStandardMergeAsync(
        MainWindowViewModel viewModel,
        string icDigits = "51926")
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc(icDigits);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = $"NT{icDigits}";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input")),
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeTp,
            golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("tp-input")),
            TestContext.Current.CancellationToken);
        await viewModel.Merge.Inspection.ActiveTask;
        Assert.True(viewModel.Merge.CanBuildMerge, viewModel.Merge.MergeReadinessStatus);
    }

    private static async Task LoadVerifiedAbMergeAsync(MainWindowViewModel viewModel)
    {
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase(
            "ab-merge",
            "nt51950-ab-boe-d82t80");
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        foreach (JsonElement artifact in goldenCase.GetProperty("artifacts")
                     .EnumerateArray()
                     .Where(static artifact => artifact.GetProperty("role").GetString() == "input"))
        {
            string slotId = artifact.GetProperty("artifactId").GetString()!;
            await viewModel.WorkflowSession.SetSlotFileAsync(
                slotId,
                CanonicalGoldenTestData.ArtifactPath(artifact),
                TestContext.Current.CancellationToken);
        }

        await AwaitStableInspectionAsync(viewModel.Merge.Inspection);
        Assert.True(viewModel.Merge.CanBuildMerge, viewModel.Merge.MergeReadinessStatus);
    }

    private static ResolutionToken ProjectionToken(FirmwareInspectionSnapshot? projection)
    {
        return projection?.InputSlotStatus?.ResolutionToken ??
            projection?.InputSlotCatalog?.ResolutionToken ??
            throw new Xunit.Sdk.XunitException("Expected a token-bound inspection projection.");
    }

    private static async Task AwaitStableInspectionAsync(WorkflowInspectionLifecycle lifecycle)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            Task current = lifecycle.ActiveTask;
            await current.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);
            if (ReferenceEquals(current, lifecycle.ActiveTask))
            {
                return;
            }
        }

        throw new TimeoutException("Inspection lifecycle did not settle after a bounded number of transitions.");
    }
}

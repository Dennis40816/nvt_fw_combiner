using System.Reflection;
using System.Runtime.ExceptionServices;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>A valid publication with no authorable route disables every workflow entry without querying a stale IC.</summary>
    [Fact]
    public async Task ZeroGlobalAuthoringPublicationFailsClosedWithoutOpeningAWorkflow()
    {
        var policy = new MutableAbCatalogPolicy();
        (_, MainWindowViewModel viewModel) = CreateCatalogRefreshViewModel(policy);
        string[] removedIcs = [.. viewModel.WorkflowSession.IcChoices];
        Assert.NotEmpty(removedIcs);

        policy.DisableAllRoutesFor(removedIcs);
        Exception? exception = await Record.ExceptionAsync(
            () => viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null));

        Assert.Null(exception);
        Assert.Empty(viewModel.WorkflowSession.IcChoices);
        Assert.False(viewModel.ShowMergeCommand.CanExecute(null));
        Assert.False(viewModel.ShowReplaceCommand.CanExecute(null));
        Assert.False(viewModel.BeginNormalMergeFromHomeCommand.CanExecute(null));
        Assert.False(viewModel.BeginAbMergeFromHomeCommand.CanExecute(null));
        Assert.False(viewModel.BeginCtrlRamReplaceFromHomeCommand.CanExecute(null));
        Assert.False(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.False(viewModel.WorkflowSession.IsWorkflowLoaded);
    }

    /// <summary>Every Home entry and draft uses the target workflow's current canonical IC projection.</summary>
    [Fact]
    public async Task NonAbWorkflowWithdrawalFiltersItsHomeDraftAndAggregatePageEntry()
    {
        var policy = new MutableAbCatalogPolicy();
        (PresentationHostServices services, MainWindowViewModel viewModel) =
            CreateCatalogRefreshViewModel(policy);
        CapabilitySelectorPublication original = services.Composition.Capabilities
            .GetSelectorPublication();
        string[] standardIcs =
        [
            .. original.IcIds.Where(icId =>
                original.IsWorkflowAuthorable(icId, ExperienceIds.StandardMerge)),
        ];
        Assert.True(standardIcs.Length > 1);
        string withdrawnIc = standardIcs[0];

        policy.DisableWorkflowFor(ExperienceIds.StandardMerge, withdrawnIc);
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        CapabilitySelectorPublication partial = services.Composition.Capabilities
            .GetSelectorPublication();

        Assert.True(viewModel.BeginNormalMergeFromHomeCommand.CanExecute(null));
        viewModel.BeginNormalMergeFromHomeCommand.Execute(null);
        Assert.True(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.DoesNotContain(withdrawnIc, viewModel.WorkflowSession.WorkflowContextSetup.IcChoices);
        Assert.All(
            viewModel.WorkflowSession.WorkflowContextSetup.IcChoices,
            icId => Assert.True(partial.IsWorkflowAuthorable(icId, ExperienceIds.StandardMerge)));
        viewModel.WorkflowSession.CancelWorkflowContextCommand.Execute(null);

        policy.DisableEveryWorkflow(ExperienceIds.StandardMerge);
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        Assert.False(viewModel.BeginNormalMergeFromHomeCommand.CanExecute(null));
        Assert.True(viewModel.ShowMergeCommand.CanExecute(null));

        policy.DisableEveryWorkflow(ExperienceIds.AbMerge);
        policy.DisableEveryWorkflow(ExperienceIds.GeneralMerge);
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        Assert.False(viewModel.ShowMergeCommand.CanExecute(null));
        Assert.True(viewModel.ShowReplaceCommand.CanExecute(null));
    }

    /// <summary>General Merge keeps its typed default when Standard authoring policy is withdrawn.</summary>
    [Fact]
    public async Task StandardWithdrawalDoesNotRemoveGeneralMergeDefaultDefinition()
    {
        var policy = new MutableAbCatalogPolicy();
        (PresentationHostServices services, MainWindowViewModel viewModel) =
            CreateCatalogRefreshViewModel(policy);
        const string icId = "NT51927";
        CapabilitySelectorPublication original = services.Composition.Capabilities
            .GetSelectorPublication();
        Assert.True(original.IsWorkflowAuthorable(icId, ExperienceIds.StandardMerge));
        Assert.True(original.IsWorkflowAuthorable(icId, ExperienceIds.GeneralMerge));

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = icId;
        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        string expectedLength = viewModel.Merge.GeneralMergeOutputLength;
        string expectedFill = viewModel.Merge.GeneralMergeOutputFillByte;
        viewModel.ShowReplaceCommand.Execute(null);

        policy.DisableWorkflowFor(ExperienceIds.StandardMerge, icId);
        Exception? refresh = await Record.ExceptionAsync(
            () => viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null));
        CapabilitySelectorPublication partial = services.Composition.Capabilities
            .GetSelectorPublication();

        Assert.Null(refresh);
        Assert.False(partial.IsWorkflowAuthorable(icId, ExperienceIds.StandardMerge));
        Assert.True(partial.IsWorkflowAuthorable(icId, ExperienceIds.GeneralMerge));
        Assert.Contains(icId, viewModel.WorkflowSession.GetPublishedWorkflowIcChoices(
            ExperienceIds.GeneralMerge));

        viewModel.ShowMergeCommand.Execute(null);
        Assert.Equal(icId, viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(ExperienceIds.GeneralMerge, viewModel.Merge.SelectedMergeMode);
        Assert.Equal(expectedLength, viewModel.Merge.GeneralMergeOutputLength);
        Assert.Equal(expectedFill, viewModel.Merge.GeneralMergeOutputFillByte);
    }

    /// <summary>CtrlRAM Home authoring exposes only the current CtrlRAM publication and closes when that workflow is withdrawn.</summary>
    [Fact]
    public async Task CtrlRamWorkflowWithdrawalFiltersItsHomeDraftAndDisablesItsEntry()
    {
        var policy = new MutableAbCatalogPolicy();
        (PresentationHostServices services, MainWindowViewModel viewModel) =
            CreateCatalogRefreshViewModel(policy);
        CapabilitySelectorPublication original = services.Composition.Capabilities
            .GetSelectorPublication();
        string[] ctrlRamIcs =
        [
            .. original.IcIds.Where(icId =>
                original.IsWorkflowAuthorable(icId, ExperienceIds.CtrlRamReplace)),
        ];
        Assert.True(ctrlRamIcs.Length > 1);
        string withdrawnIc = ctrlRamIcs[0];

        policy.DisableWorkflowFor(ExperienceIds.CtrlRamReplace, withdrawnIc);
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        CapabilitySelectorPublication partial = services.Composition.Capabilities
            .GetSelectorPublication();

        Assert.True(viewModel.BeginCtrlRamReplaceFromHomeCommand.CanExecute(null));
        viewModel.BeginCtrlRamReplaceFromHomeCommand.Execute(null);
        Assert.True(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.DoesNotContain(
            withdrawnIc,
            viewModel.WorkflowSession.WorkflowContextSetup.IcChoices);
        Assert.All(
            viewModel.WorkflowSession.WorkflowContextSetup.IcChoices,
            icId => Assert.True(
                partial.IsWorkflowAuthorable(icId, ExperienceIds.CtrlRamReplace)));

        policy.DisableEveryWorkflow(ExperienceIds.CtrlRamReplace);
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);

        Assert.False(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.False(viewModel.BeginCtrlRamReplaceFromHomeCommand.CanExecute(null));
        Assert.True(viewModel.ShowReplaceCommand.CanExecute(null));
        Assert.True(viewModel.ShowMergeCommand.CanExecute(null));
    }

    /// <summary>Withdrawing every Replace workflow disables only the Replace aggregate while Merge stays available.</summary>
    [Fact]
    public async Task WithdrawingAllReplaceWorkflowsDisablesReplaceAggregateOnly()
    {
        var policy = new MutableAbCatalogPolicy();
        (_, MainWindowViewModel viewModel) = CreateCatalogRefreshViewModel(policy);
        Assert.True(viewModel.ShowReplaceCommand.CanExecute(null));
        Assert.True(viewModel.ShowMergeCommand.CanExecute(null));

        policy.DisableEveryWorkflow(ExperienceIds.DpReplace);
        policy.DisableEveryWorkflow(ExperienceIds.CtrlRamReplace);
        policy.DisableEveryWorkflow(ExperienceIds.GeneralReplace);
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);

        Assert.False(viewModel.BeginDpReplaceFromHomeCommand.CanExecute(null));
        Assert.False(viewModel.BeginCtrlRamReplaceFromHomeCommand.CanExecute(null));
        Assert.False(viewModel.BeginGeneralReplaceFromHomeCommand.CanExecute(null));
        Assert.False(viewModel.ShowReplaceCommand.CanExecute(null));
        Assert.True(viewModel.BeginNormalMergeFromHomeCommand.CanExecute(null));
        Assert.True(viewModel.BeginAbMergeFromHomeCommand.CanExecute(null));
        Assert.True(viewModel.BeginGeneralMergeFromHomeCommand.CanExecute(null));
        Assert.True(viewModel.ShowMergeCommand.CanExecute(null));
        Assert.False(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
    }

    /// <summary>A terminal zero-authorable publication performs no profile-dependent authoring query.</summary>
    [Fact]
    public async Task ZeroGlobalAuthoringPublicationDispatchesExactlyZeroAuthoringPortCalls()
    {
        var policy = new MutableAbCatalogPolicy();
        PresentationHostServices original = PresentationTestHost.CreateServicesWithCatalogPolicy(
            "0.10.6-zero-authoring-query-test",
            policy.Load);
        var sentinel = AuthoringPortSentinel.Create(original.Composition);
        PresentationHostServices services = WithAuthoringPorts(original, sentinel);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(
            services,
            ShellLanguage.English);
        _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);

        CapabilitySelectorPublication publication = services.Composition.Capabilities
            .GetSelectorPublication();
        sentinel.Arm();
        string AuthorableIc(string workflowId)
        {
            return publication.IcIds.First(icId =>
                publication.IsWorkflowAuthorable(icId, workflowId));
        }
        _ = sentinel.StandardMerge.IsSupported(AuthorableIc(ExperienceIds.StandardMerge));
        _ = sentinel.AbMerge.IsAvailable(AuthorableIc(ExperienceIds.AbMerge));
        _ = sentinel.DpReplace.GetAuthoringSnapshot(
            AuthorableIc(ExperienceIds.DpReplace),
            [],
            new Dictionary<string, FileStamp>(StringComparer.Ordinal),
            new AuthoringRevision(1));
        _ = sentinel.General.GetDefaultOutputLength(AuthorableIc(ExperienceIds.GeneralMerge));
        _ = sentinel.CtrlRam.GetDiscoveryDisplay(
            AuthorableIc(ExperienceIds.CtrlRamReplace),
            IcNumberSelectionTokens.SingleChip);
        Assert.Equal([1, 1, 1, 1, 1], sentinel.ArmedCallCounts);

        policy.DisableEveryRoute();
        sentinel.Arm();
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        AssertZeroAuthorableBindingState(viewModel);

        Assert.Equal(0, sentinel.ArmedCallCount);
    }

    /// <summary>A valid cold-start publication with no route keeps every workflow binding query-safe.</summary>
    [Fact]
    public void ColdStartZeroGlobalPublicationKeepsWorkflowBindingsQuerySafe()
    {
        var policy = new MutableAbCatalogPolicy();
        policy.DisableEveryRoute();
        (_, MainWindowViewModel viewModel) = CreateCatalogRefreshViewModel(policy);

        Exception? exception = Record.Exception(() => AssertZeroAuthorableBindingState(viewModel));

        Assert.Null(exception);
        Assert.False(viewModel.WorkflowSession.IsWorkflowLoaded);
    }

    /// <summary>A loaded workflow becomes unavailable without retaining a callable IC-specific binding path.</summary>
    [Theory]
    [InlineData("Merge", "standard-merge")]
    [InlineData("Merge", "ab-merge")]
    [InlineData("Replace", "ctrlram-replace")]
    [InlineData("Replace", "dp-replace")]
    public async Task LoadedWorkflowZeroGlobalRefreshKeepsBindingsQuerySafe(
        string page,
        string experienceId)
    {
        var policy = new MutableAbCatalogPolicy();
        (PresentationHostServices services, MainWindowViewModel viewModel) =
            CreateCatalogRefreshViewModel(policy);
        CapabilitySelectorPublication publication =
            services.Composition.Capabilities.GetSelectorPublication();
        string icId = publication.IcIds.First(ic =>
            publication.IsWorkflowAuthorable(ic, experienceId));
        if (StringComparer.Ordinal.Equals(page, "Merge"))
        {
            viewModel.ShowMergeCommand.Execute(null);
            viewModel.WorkflowSession.SelectedIc = icId;
            viewModel.Merge.SelectedMergeMode = experienceId;
        }
        else
        {
            viewModel.ShowReplaceCommand.Execute(null);
            viewModel.WorkflowSession.SelectedIc = icId;
            viewModel.Replace.SelectedReplaceMode = experienceId;
        }
        Assert.True(viewModel.WorkflowSession.IsWorkflowLoaded);

        policy.DisableEveryRoute();
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        Exception? exception = Record.Exception(() => AssertZeroAuthorableBindingState(viewModel));

        Assert.Null(exception);
        Assert.True(viewModel.WorkflowSession.IsWorkflowLoaded);
    }

    /// <summary>A delayed inspection from an older catalog token cannot publish after selector reconciliation.</summary>
    [Fact]
    public async Task CatalogRefreshRejectsDelayedAbInspectionFromPriorPublication()
    {
        var policy = new MutableAbCatalogPolicy();
        PresentationHostServices services =
            PresentationTestHost.CreateServicesWithCatalogPolicy(
                "0.10.6-catalog-inspection-token-test",
                policy.Load);
        var inspection = new DelayedCatalogFirmwareInspection(
            services.Composition.FirmwareInspection);
        var viewModel = new MainWindowViewModel(
            "ui-smoke",
            "ui-smoke",
            ShellLanguage.English,
            services,
            inspection);
        _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
        ResolutionToken originalToken = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;
        string tpAPath = CanonicalGoldenTestData.ArtifactPath(
            CanonicalGoldenTestData.Artifact(
                CanonicalGoldenTestData.LoadDirectCase(
                    "ab-merge",
                    "nt51950-ab-boe-d82t80"),
                CompositionAddressSpaceIds.TpAInput));
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        FirmwareSlotViewModel tpASlot = viewModel.Merge.AbMergeSlotsByAddressSpace[
            CompositionAddressSpaceIds.TpAInput];

        Task selection = viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            tpAPath,
            TestContext.Current.CancellationToken);
        await inspection.OriginalEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        ResolutionToken refreshedToken = services.Composition.Capabilities
            .GetSelectorPublication().ResolutionToken;
        Assert.NotEqual(originalToken, refreshedToken);
        inspection.ReleaseOriginal();
        await inspection.FreshEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        Assert.Equal(2, inspection.BatchCount);
        Assert.Null(tpASlot.CurrentInspectionProjection);
        inspection.ReleaseFresh();
        Exception? completion = await Record.ExceptionAsync(() => selection);
        await viewModel.Merge.Inspection.ActiveTask.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        Assert.Null(completion);
        Assert.Equal(ExperienceIds.AbMerge, viewModel.Merge.SelectedMergeMode);
        Assert.Equal(2, inspection.BatchCount);
        AuthoringInputSlotStatus status = Assert.IsType<AuthoringInputSlotStatus>(
            tpASlot.CurrentInspectionProjection?.InputSlotStatus);
        Assert.Equal(refreshedToken, status.ResolutionToken);
        Assert.Equal(tpAPath, tpASlot.FilePath);
        Assert.Equal(WorkflowInspectionAttemptState.Succeeded, viewModel.Merge.Inspection.State);
    }

    private static void AssertZeroAuthorableBindingState(MainWindowViewModel viewModel)
    {
        Assert.Empty(viewModel.WorkflowSession.IcChoices);
        Assert.False(viewModel.WorkflowSession.HasWorkflowAuthoringChoices);
        Assert.Equal(string.Empty, viewModel.WorkflowSession.SelectedIc);
        Assert.False(viewModel.ShowMergeCommand.CanExecute(null));
        Assert.False(viewModel.ShowReplaceCommand.CanExecute(null));

        _ = viewModel.Merge.MergeModeChoices;
        _ = viewModel.Merge.StandardMergeSupportSummary;
        Assert.Equal(
            ShellTextResources.For(ShellLanguage.English).NotAvailableLabel,
            viewModel.Merge.MergeReadinessStatus);
        Assert.Equal(string.Empty, viewModel.Merge.StandardMergeOutputFileName);
        Assert.Equal(string.Empty, viewModel.Merge.GeneralMergeOutputFileName);
        Assert.Equal(string.Empty, viewModel.Merge.AbMergeOutputFileName);
        Assert.Equal(string.Empty, viewModel.Merge.MergeOutputFileName);
        _ = viewModel.Merge.PrimaryBuildBlocker;
        Assert.False(viewModel.Merge.IsStandardMergeSupported);
        Assert.False(viewModel.Merge.IsAbMergeSupported);
        Assert.False(viewModel.Merge.CanBuildMerge);

        Assert.Empty(viewModel.Replace.ReplaceModeChoices);
        Assert.Null(viewModel.Replace.SelectedReplaceWorkflowReadiness);
        Assert.Equal(string.Empty, viewModel.Replace.SelectedReplaceModeEvidenceLabel);
        Assert.Equal(string.Empty, viewModel.Replace.SelectedReplaceModeEvidenceTooltip);
        Assert.Equal(
            ShellTextResources.For(ShellLanguage.English).NotAvailableLabel,
            viewModel.Replace.ReplaceReadinessStatus);
        Assert.Equal(string.Empty, viewModel.Replace.ReplaceOutputFileName);
        _ = viewModel.Replace.PrimaryBuildBlocker;
        Assert.False(viewModel.Replace.IsSelectedReplaceModeGoldenVerified);
        Assert.False(viewModel.Replace.IsSelectedReplaceModeEvidenceGated);
        Assert.False(viewModel.Replace.IsSelectedReplaceModeUnavailable);
        Assert.False(viewModel.Replace.CanBuildReplace);

        viewModel.WorkflowSession.RefreshContextState(WorkflowInspectionOwner.Merge);
        viewModel.WorkflowSession.RefreshContextState(WorkflowInspectionOwner.Replace);
        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        Assert.Equal(string.Empty, viewModel.Merge.MergeOutputFileName);
        viewModel.SelectedLanguage = "Traditional Chinese";
        Assert.Equal(
            ShellTextResources.For(ShellLanguage.ChineseTraditional).NotAvailableLabel,
            viewModel.Merge.MergeReadinessStatus);
        Assert.Equal(
            ShellTextResources.For(ShellLanguage.ChineseTraditional).NotAvailableLabel,
            viewModel.Replace.ReplaceReadinessStatus);
        viewModel.SelectedLanguage = "English";
        Assert.False(viewModel.Merge.CanBuildMerge);
        Assert.False(viewModel.Replace.CanBuildReplace);
    }

    private static PresentationHostServices WithAuthoringPorts(
        PresentationHostServices services,
        AuthoringPortSentinel sentinel)
    {
        PresentationCompositionServices current = services.Composition;
        var composition = new PresentationCompositionServices(
            current.Capabilities,
            sentinel.StandardMerge,
            sentinel.AbMerge,
            sentinel.DpReplace,
            sentinel.General,
            sentinel.CtrlRam,
            current.FirmwareInspection,
            current.OutputNaming,
            current.Execution);
        return new PresentationHostServices(
            composition,
            services.FileReveal,
            services.SupportMatrix,
            services.SystemInformation,
            services.SystemDiagnosticsExporter,
            services.RawBinaryEditorFileSessions,
            services.CanonicalCatalogLoader,
            services.ExternalEnvironmentLoader,
            services.LocalFiles,
            services.VersionManagement,
            services.ManagedApplicationStartup,
            services.StableLauncherHandoff);
    }

    private sealed class AuthoringPortSentinel
    {
        private readonly CountingAuthoringProxy<IStandardMergeAuthoring> _standardMerge;
        private readonly CountingAuthoringProxy<IAbMergeAuthoring> _abMerge;
        private readonly CountingAuthoringProxy<IDpReplaceAuthoring> _dpReplace;
        private readonly CountingAuthoringProxy<IGeneralAuthoring> _general;
        private readonly CountingAuthoringProxy<ICtrlRamAuthoring> _ctrlRam;

        private AuthoringPortSentinel(PresentationCompositionServices services)
        {
            (StandardMerge, _standardMerge) =
                CountingAuthoringProxy<IStandardMergeAuthoring>.Wrap(services.StandardMergeAuthoring);
            (AbMerge, _abMerge) =
                CountingAuthoringProxy<IAbMergeAuthoring>.Wrap(services.AbMergeAuthoring);
            (DpReplace, _dpReplace) =
                CountingAuthoringProxy<IDpReplaceAuthoring>.Wrap(services.DpReplaceAuthoring);
            (General, _general) =
                CountingAuthoringProxy<IGeneralAuthoring>.Wrap(services.GeneralAuthoring);
            (CtrlRam, _ctrlRam) =
                CountingAuthoringProxy<ICtrlRamAuthoring>.Wrap(services.CtrlRamAuthoring);
        }

        internal IStandardMergeAuthoring StandardMerge { get; }

        internal IAbMergeAuthoring AbMerge { get; }

        internal IDpReplaceAuthoring DpReplace { get; }

        internal IGeneralAuthoring General { get; }

        internal ICtrlRamAuthoring CtrlRam { get; }

        internal int ArmedCallCount => _standardMerge.ArmedCallCount +
            _abMerge.ArmedCallCount +
            _dpReplace.ArmedCallCount +
            _general.ArmedCallCount +
            _ctrlRam.ArmedCallCount;

        internal IReadOnlyList<int> ArmedCallCounts =>
        [
            _standardMerge.ArmedCallCount,
            _abMerge.ArmedCallCount,
            _dpReplace.ArmedCallCount,
            _general.ArmedCallCount,
            _ctrlRam.ArmedCallCount,
        ];

        internal static AuthoringPortSentinel Create(PresentationCompositionServices services)
        {
            return new AuthoringPortSentinel(services);
        }

        internal void Arm()
        {
            _standardMerge.Arm();
            _abMerge.Arm();
            _dpReplace.Arm();
            _general.Arm();
            _ctrlRam.Arm();
        }

        internal void ArmStandardFailure(string methodName)
        {
            Arm();
            _standardMerge.ThrowOn(methodName);
        }

        internal void ArmGeneralFailure(string methodName)
        {
            Arm();
            _general.ThrowOn(methodName);
        }

        internal void ArmDpFailure(string methodName, int invocation = 1)
        {
            Arm();
            _dpReplace.ThrowOn(methodName, invocation);
        }
    }

    /// <summary>Test-only forwarding proxy that counts calls after an explicit arm point.</summary>
    public class CountingAuthoringProxy<TPort> : DispatchProxy
        where TPort : class
    {
        private TPort? _inner;
        private int _armed;
        private int _armedCallCount;
        private string? _throwingMethodName;
        private int _throwingMethodInvocation = 1;
        private int _throwingMethodCallCount;

        /// <summary>Creates an unconfigured proxy base for <see cref="DispatchProxy"/>.</summary>
        public CountingAuthoringProxy()
        {
        }

        internal int ArmedCallCount => Volatile.Read(ref _armedCallCount);

        internal static (TPort Port, CountingAuthoringProxy<TPort> Proxy) Wrap(TPort inner)
        {
            TPort port = Create<TPort, CountingAuthoringProxy<TPort>>();
            var proxy = (CountingAuthoringProxy<TPort>)(object)port;
            proxy._inner = inner;
            return (port, proxy);
        }

        internal void Arm()
        {
            _ = Interlocked.Exchange(ref _armedCallCount, 0);
            _ = Interlocked.Exchange(ref _throwingMethodCallCount, 0);
            _throwingMethodName = null;
            _throwingMethodInvocation = 1;
            Volatile.Write(ref _armed, 1);
        }

        internal void ThrowOn(string methodName, int invocation = 1)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
            ArgumentOutOfRangeException.ThrowIfLessThan(invocation, 1);
            _throwingMethodName = methodName;
            _throwingMethodInvocation = invocation;
        }

        /// <summary>Forwards one interface call and records it only after the proxy is armed.</summary>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (Volatile.Read(ref _armed) == 1)
            {
                _ = Interlocked.Increment(ref _armedCallCount);
            }

            bool isThrowingMethod = Volatile.Read(ref _armed) == 1 &&
                StringComparer.Ordinal.Equals(_throwingMethodName, targetMethod.Name);
            int throwingMethodCall = isThrowingMethod
                ? Interlocked.Increment(ref _throwingMethodCallCount)
                : 0;
            if (isThrowingMethod && throwingMethodCall == _throwingMethodInvocation)
            {
                throw new InvalidOperationException(
                    $"Injected {targetMethod.Name} failure.");
            }

            try
            {
                return targetMethod.Invoke(_inner, args);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                return null;
            }
        }
    }

    private sealed class DelayedCatalogFirmwareInspection(IFirmwareInspection inner)
        : IFirmwareInspection
    {
        private int _batchCount;
        private readonly TaskCompletionSource _releaseOriginal =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFresh =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource OriginalEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource FreshEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int BatchCount => Volatile.Read(ref _batchCount);

        internal void ReleaseOriginal()
        {
            _ = _releaseOriginal.TrySetResult();
        }

        internal void ReleaseFresh()
        {
            _ = _releaseFresh.TrySetResult();
        }

        public async ValueTask<FirmwareInspectionBatchResult> InspectFirmwareBatchAsync(
            string icId,
            IReadOnlyList<FirmwareInspectionSnapshotInput> inputs,
            CancellationToken cancellationToken,
            IProgress<AuthoringInspectionProgress>? progress = null)
        {
            int batch = Interlocked.Increment(ref _batchCount);
            FirmwareInspectionBatchResult result = await inner.InspectFirmwareBatchAsync(
                icId,
                inputs,
                cancellationToken,
                progress);
            if (batch == 1)
            {
                _ = OriginalEntered.TrySetResult();
                await _releaseOriginal.Task.ConfigureAwait(false);
            }
            else if (batch == 2)
            {
                _ = FreshEntered.TrySetResult();
                await _releaseFresh.Task.ConfigureAwait(false);
            }
            return result;
        }

        public CtrlRamInspectionDisplay ProjectCtrlRamInspectionDisplay(
            string icId,
            string numberToken,
            FirmwareConfigMetadataSnapshot? baseFirmware)
        {
            return inner.ProjectCtrlRamInspectionDisplay(icId, numberToken, baseFirmware);
        }
    }
}

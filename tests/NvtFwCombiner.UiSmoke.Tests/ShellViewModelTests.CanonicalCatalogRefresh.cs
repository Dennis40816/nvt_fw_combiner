using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>A fresh catalog preserves the same globally valid IC and leaves unavailable AB mode safely.</summary>
    [AvaloniaFact]
    public async Task CanonicalCatalogRefreshRepairsActiveRemovedAbIcBeforeFallingBackToStandard()
    {
        var policy = new MutableAbCatalogPolicy();
        (PresentationHostServices services, MainWindowViewModel viewModel) =
            CreateCatalogRefreshViewModel(policy);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        Assert.True(viewModel.Merge.IsAbCodeMergeModeSelected);
        var selector = new ComboBox { DataContext = viewModel };
        _ = selector.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("WorkflowSession.IcChoices"));
        _ = selector.Bind(
            ComboBox.SelectedItemProperty,
            new Binding("WorkflowSession.SelectedIc") { Mode = BindingMode.TwoWay });
        Dispatcher.UIThread.RunJobs();
        var changes = new List<string?>();
        viewModel.WorkflowSession.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        policy.DisableAllRoutesFor("NT51950");
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        string fallbackIc = services.Composition.Capabilities.DefaultIcId;
        Assert.Equal(fallbackIc, viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(
            fallbackIc,
            viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Merge));
        Assert.Equal(ExperienceIds.StandardMerge, viewModel.Merge.SelectedMergeMode);
        Assert.Equal(fallbackIc, selector.SelectedItem);
        Assert.Contains(fallbackIc, selector.Items.Cast<string>());
        Assert.DoesNotContain("NT51950", viewModel.WorkflowSession.IcChoices);
        Assert.DoesNotContain(
            "NT51950",
            services.Composition.Capabilities.GetAbMergeProfileSummaries()
                .Select(static profile => profile.IcId));
        Assert.True(
            changes.IndexOf(nameof(WorkflowSessionPresentationViewModel.IcChoices)) <
            changes.IndexOf(nameof(WorkflowSessionPresentationViewModel.SelectedIc)));
    }

    /// <summary>A bound AB selector replaces its items from the fresh publication without clearing a valid selection.</summary>
    [AvaloniaFact]
    public async Task CanonicalCatalogRefreshUpdatesLiveAbSelectorWithoutClearingSelection()
    {
        var policy = new MutableAbCatalogPolicy();
        (_, MainWindowViewModel viewModel) = CreateCatalogRefreshViewModel(policy);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        var selector = new ComboBox { DataContext = viewModel };
        _ = selector.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("WorkflowSession.IcChoices"));
        _ = selector.Bind(
            ComboBox.SelectedItemProperty,
            new Binding("WorkflowSession.SelectedIc") { Mode = BindingMode.TwoWay });
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("NT51929", selector.Items.Cast<string>());

        policy.DisableAbFor("NT51929");
        var changes = new List<string?>();
        viewModel.WorkflowSession.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("NT51950", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal("NT51950", selector.SelectedItem);
        Assert.DoesNotContain("NT51929", selector.Items.Cast<string>());
        Assert.True(viewModel.Merge.IsAbCodeMergeModeSelected);
        Assert.True(
            changes.IndexOf(nameof(WorkflowSessionPresentationViewModel.IcChoices)) <
            changes.IndexOf(nameof(WorkflowSessionPresentationViewModel.SelectedIc)));
    }

    /// <summary>The global list is visible before a selected IC loses only AB authorability.</summary>
    [AvaloniaFact]
    public async Task CanonicalCatalogRefreshPublishesGlobalChoicesBeforeSelectedAbFallback()
    {
        var policy = new MutableAbCatalogPolicy();
        (_, MainWindowViewModel viewModel) = CreateCatalogRefreshViewModel(policy);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        var selector = new ComboBox { DataContext = viewModel };
        _ = selector.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("WorkflowSession.IcChoices"));
        _ = selector.Bind(
            ComboBox.SelectedItemProperty,
            new Binding("WorkflowSession.SelectedIc") { Mode = BindingMode.TwoWay });
        Dispatcher.UIThread.RunJobs();
        var changes = new List<string?>();
        viewModel.WorkflowSession.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        policy.DisableAbFor("NT51950");
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ExperienceIds.StandardMerge, viewModel.Merge.SelectedMergeMode);
        Assert.Equal("NT51950", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal("NT51950", selector.SelectedItem);
        Assert.Contains("NT51950", selector.Items.Cast<string>());
        Assert.True(
            changes.IndexOf(nameof(WorkflowSessionPresentationViewModel.IcChoices)) <
            changes.IndexOf(nameof(WorkflowSessionPresentationViewModel.SelectedIc)));
    }

    /// <summary>AB fallback stages one Standard rebuild before publishing the destination selector.</summary>
    [Fact]
    public async Task CatalogRefreshFallbackPublishesDestinationBeforeOneStandardRebuild()
    {
        var policy = new MutableAbCatalogPolicy();
        PresentationHostServices services = PresentationTestHost.CreateServicesWithCatalogPolicy(
            "0.10.6-catalog-fallback-order-test",
            policy.Load);
        var authoring = new RecordingStandardMergeAuthoring(
            services.Composition.StandardMergeAuthoring);
        services = WithStandardMergeAuthoring(services, authoring);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(
            services,
            ShellLanguage.English);
        _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
        const string removedIc = "NT51950";
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = removedIc;
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        bool destinationPublished = false;
        var selectorChanges = new List<string?>();
        var timeline = new List<string>();
        int modeChangeCount = 0;
        authoring.Reset(() => destinationPublished, timeline.Add);
        viewModel.WorkflowSession.PropertyChanged += (_, args) =>
        {
            selectorChanges.Add(args.PropertyName);
            if (args.PropertyName is nameof(WorkflowSessionPresentationViewModel.IcChoices) or
                nameof(WorkflowSessionPresentationViewModel.SelectedIc))
            {
                timeline.Add(args.PropertyName);
            }
            if (args.PropertyName == nameof(WorkflowSessionPresentationViewModel.SelectedIc) &&
                !StringComparer.Ordinal.Equals(viewModel.WorkflowSession.SelectedIc, removedIc))
            {
                destinationPublished = true;
            }
        };
        viewModel.Merge.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MergePresentationViewModel.SelectedMergeMode))
            {
                modeChangeCount++;
                timeline.Add(args.PropertyName);
            }
        };

        policy.DisableAllRoutesFor(removedIc);
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null).WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        string finalIc = services.Composition.Capabilities.DefaultIcId;
        Assert.Equal(ExperienceIds.StandardMerge, viewModel.Merge.SelectedMergeMode);
        Assert.Equal(finalIc, viewModel.WorkflowSession.SelectedIc);
        Assert.NotEqual(removedIc, finalIc);
        Assert.True(destinationPublished);
        Assert.True(
            selectorChanges.IndexOf(nameof(WorkflowSessionPresentationViewModel.IcChoices)) <
            selectorChanges.IndexOf(nameof(WorkflowSessionPresentationViewModel.SelectedIc)));
        Assert.NotEmpty(authoring.Calls);
        Assert.All(authoring.Calls, call =>
        {
            Assert.False(call.DestinationPublished);
            Assert.Equal(finalIc, call.IcId);
        });
        Assert.Equal(1, authoring.GetInputAddressSpacesCalls);
        Assert.Equal(1, authoring.GetAuthoringSnapshotCalls);
        Assert.Equal(3, authoring.Calls.Count);
        _ = Assert.Single(authoring.Calls, call =>
            call.Method == nameof(IStandardMergeAuthoring.GetRequiredAddressSpaces));
        Assert.Equal(
            [
                nameof(IStandardMergeAuthoring.GetRequiredAddressSpaces),
                nameof(IStandardMergeAuthoring.GetInputAddressSpaces),
                nameof(IStandardMergeAuthoring.GetAuthoringSnapshot),
                nameof(WorkflowSessionPresentationViewModel.IcChoices),
                nameof(WorkflowSessionPresentationViewModel.SelectedIc),
                nameof(MergePresentationViewModel.SelectedMergeMode),
            ],
            timeline);
        Assert.Equal(1, modeChangeCount);
    }

    /// <summary>An inactive retained AB context is reconciled without borrowing or replacing the active Replace context.</summary>
    [Fact]
    public async Task CanonicalCatalogRefreshReconcilesInactiveAbContextWhileReplaceStaysIndependent()
    {
        var policy = new MutableAbCatalogPolicy();
        (PresentationHostServices services, MainWindowViewModel viewModel) =
            CreateCatalogRefreshViewModel(policy);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        string replaceNumber = viewModel.WorkflowSession.SelectedNumber;

        policy.DisableAllRoutesFor("NT51950");
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.Equal("NT51926", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(
            "NT51926",
            viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Replace));
        Assert.Equal(
            replaceNumber,
            viewModel.WorkflowSession.GetWorkflowPageNumber(WorkflowInspectionOwner.Replace));
        Assert.Equal(
            services.Composition.Capabilities.DefaultIcId,
            viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Merge));
        Assert.Equal(ExperienceIds.StandardMerge, viewModel.Merge.SelectedMergeMode);
    }

    /// <summary>A publication with no AB-authorable route disables Home AB and never opens an empty draft.</summary>
    [Fact]
    public async Task CanonicalCatalogRefreshWithZeroAbChoicesDisablesHomeEntry()
    {
        var policy = new MutableAbCatalogPolicy();
        (_, MainWindowViewModel viewModel) = CreateCatalogRefreshViewModel(policy);
        Assert.False(viewModel.WorkflowSession.IsWorkflowLoaded);
        Assert.True(viewModel.BeginAbMergeFromHomeCommand.CanExecute(null));

        policy.DisableEveryAbRoute();
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);

        Assert.False(viewModel.BeginAbMergeFromHomeCommand.CanExecute(null));
        Assert.True(viewModel.BeginNormalMergeFromHomeCommand.CanExecute(null));
        Assert.True(viewModel.ShowMergeCommand.CanExecute(null));
        Assert.True(viewModel.ShowReplaceCommand.CanExecute(null));
        viewModel.BeginAbMergeFromHomeCommand.Execute(null);
        Assert.False(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.False(viewModel.WorkflowSession.IsWorkflowLoaded);
        Assert.Empty(
            viewModel.WorkflowSession.WorkflowContextSetup.IcChoices);
    }

    /// <summary>A refresh before lazy workflow construction is consumed by the first AB context selector.</summary>
    [Fact]
    public async Task CanonicalCatalogRefreshBeforeFirstWorkflowLoadUsesFreshAbChoices()
    {
        var policy = new MutableAbCatalogPolicy();
        (_, MainWindowViewModel viewModel) = CreateCatalogRefreshViewModel(policy);
        Assert.False(viewModel.WorkflowSession.IsWorkflowLoaded);

        policy.DisableAbFor("NT51929");
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        Assert.False(viewModel.WorkflowSession.IsWorkflowLoaded);
        viewModel.BeginAbMergeFromHomeCommand.Execute(null);

        Assert.True(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.DoesNotContain(
            "NT51929",
            viewModel.WorkflowSession.WorkflowContextSetup.IcChoices);
        Assert.Contains(
            "NT51950",
            viewModel.WorkflowSession.WorkflowContextSetup.IcChoices);
    }

    /// <summary>A direct first page open repairs a default IC removed by the latest publication before querying workflow state.</summary>
    [Theory]
    [InlineData("Merge")]
    [InlineData("Replace")]
    public async Task CanonicalCatalogRefreshBeforeFirstWorkflowLoadRepairsRemovedDefaultBeforeDirectPageOpen(
        string page)
    {
        var policy = new MutableAbCatalogPolicy();
        (PresentationHostServices services, MainWindowViewModel viewModel) =
            CreateCatalogRefreshViewModel(policy);
        string removedDefault = services.Composition.Capabilities.DefaultIcId;
        Assert.False(viewModel.WorkflowSession.IsWorkflowLoaded);

        policy.DisableAllRoutesFor(removedDefault);
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        Assert.False(viewModel.WorkflowSession.IsWorkflowLoaded);

        Exception? exception = Record.Exception(() =>
        {
            if (StringComparer.Ordinal.Equals(page, "Merge"))
            {
                viewModel.ShowMergeCommand.Execute(null);
            }
            else
            {
                viewModel.ShowReplaceCommand.Execute(null);
            }
        });

        Assert.Null(exception);
        Assert.True(viewModel.WorkflowSession.IsWorkflowLoaded);
        Assert.Equal(
            services.Composition.Capabilities.DefaultIcId,
            viewModel.WorkflowSession.SelectedIc);
        Assert.NotEqual(removedDefault, viewModel.WorkflowSession.SelectedIc);
        Assert.Contains(
            viewModel.WorkflowSession.SelectedIc,
            viewModel.WorkflowSession.IcChoices);
        Assert.Equal(
            viewModel.WorkflowSession.SelectedIc,
            viewModel.WorkflowSession.GetWorkflowPageIc(
                StringComparer.Ordinal.Equals(page, "Merge")
                    ? WorkflowInspectionOwner.Merge
                    : WorkflowInspectionOwner.Replace));
    }

    /// <summary>Removing only the selected AB topology atomically repairs the live Number selector while preserving the IC.</summary>
    [AvaloniaFact]
    public async Task CanonicalCatalogRefreshRepairsRemovedActiveAbTopologyAndLiveNumberComboBox()
    {
        var policy = new MutableAbCatalogPolicy();
        (_, MainWindowViewModel viewModel) = CreateCatalogRefreshViewModel(policy);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        var selector = new ComboBox { DataContext = viewModel };
        _ = selector.Bind(
            ItemsControl.ItemsSourceProperty,
            new Binding("WorkflowSession.NumberSelectionChoices"));
        _ = selector.Bind(
            ComboBox.SelectedItemProperty,
            new Binding("WorkflowSession.SelectedNumberChoice") { Mode = BindingMode.TwoWay });
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(
            IcNumberSelectionTokens.Cascade,
            Assert.IsType<IcNumberChoiceViewModel>(selector.SelectedItem).Token);

        policy.DisableAbVariant("NT51950", "2-plus-ic");
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.Merge.IsAbCodeMergeModeSelected);
        Assert.Equal("NT51950", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(
            [IcNumberSelectionTokens.SingleChip],
            viewModel.WorkflowSession.NumberSelectionChoices.Select(static choice => choice.Token));
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal(
            IcNumberSelectionTokens.SingleChip,
            Assert.IsType<IcNumberChoiceViewModel>(selector.SelectedItem).Token);
        Assert.False(viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen);
    }

    /// <summary>A removed active Replace IC is repaired without overwriting the independent Merge draft.</summary>
    [Fact]
    public async Task CanonicalCatalogRefreshRepairsInvalidReplaceDraftIndependently()
    {
        var policy = new MutableAbCatalogPolicy();
        (PresentationHostServices services, MainWindowViewModel viewModel) =
            CreateCatalogRefreshViewModel(policy);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";

        policy.DisableAllRoutesFor("NT51926");
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);

        string fallbackIc = services.Composition.Capabilities.DefaultIcId;
        Assert.True(viewModel.IsReplaceVisible);
        Assert.Equal(fallbackIc, viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(
            fallbackIc,
            viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Replace));
        Assert.Equal(
            "NT51950",
            viewModel.WorkflowSession.GetWorkflowPageIc(WorkflowInspectionOwner.Merge));
        Assert.Equal(
            IcNumberSelectionTokens.Cascade,
            viewModel.WorkflowSession.GetWorkflowPageNumber(WorkflowInspectionOwner.Merge));
        Assert.Equal(ExperienceIds.AbMerge, viewModel.Merge.SelectedMergeMode);
    }

    /// <summary>Zero AB routes invalidate an open Home draft, and a later publication restores entry.</summary>
    [Fact]
    public async Task ZeroAbRefreshInvalidatesOpenHomeDraftAndLaterPublicationReenablesEntry()
    {
        var policy = new MutableAbCatalogPolicy();
        (_, MainWindowViewModel viewModel) = CreateCatalogRefreshViewModel(policy);
        viewModel.BeginAbMergeFromHomeCommand.Execute(null);
        Assert.True(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.NotEmpty(viewModel.WorkflowSession.WorkflowContextSetup.IcChoices);

        policy.DisableEveryAbRoute();
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);

        Assert.False(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.Empty(viewModel.WorkflowSession.WorkflowContextSetup.IcChoices);
        Assert.False(viewModel.BeginAbMergeFromHomeCommand.CanExecute(null));

        policy.EnableEveryAbRoute();
        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);

        Assert.True(viewModel.BeginAbMergeFromHomeCommand.CanExecute(null));
        viewModel.BeginAbMergeFromHomeCommand.Execute(null);
        Assert.True(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.NotEmpty(viewModel.WorkflowSession.WorkflowContextSetup.IcChoices);
    }

    private static (PresentationHostServices Services, MainWindowViewModel ViewModel)
        CreateCatalogRefreshViewModel(MutableAbCatalogPolicy policy)
    {
        PresentationHostServices services =
            PresentationTestHost.CreateServicesWithCatalogPolicy(
                "0.10.6-catalog-refresh-test",
                policy.Load);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(
            services,
            ShellLanguage.English);
        _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
        return (services, viewModel);
    }

    private static PresentationHostServices WithStandardMergeAuthoring(
        PresentationHostServices services,
        IStandardMergeAuthoring standardMergeAuthoring)
    {
        PresentationCompositionServices current = services.Composition;
        var composition = new PresentationCompositionServices(
            current.Capabilities,
            standardMergeAuthoring,
            current.AbMergeAuthoring,
            current.DpReplaceAuthoring,
            current.GeneralAuthoring,
            current.CtrlRamAuthoring,
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

    private sealed class MutableAbCatalogPolicy
    {
        private readonly HashSet<string> _disabledAbIcs = new(StringComparer.Ordinal);
        private readonly HashSet<string> _disabledAllRouteIcs = new(StringComparer.Ordinal);
        private readonly HashSet<string> _disabledAbVariants = new(StringComparer.Ordinal);
        private readonly HashSet<string> _disabledWorkflowIcs = new(StringComparer.Ordinal);
        private readonly HashSet<string> _disabledWorkflows = new(StringComparer.Ordinal);
        private bool _disableEveryAbRoute;
        private bool _disableEveryRoute;

        internal void DisableAbFor(params string[] icIds)
        {
            foreach (string icId in icIds)
            {
                _ = _disabledAbIcs.Add(icId);
            }
        }

        internal void DisableAllRoutesFor(params string[] icIds)
        {
            foreach (string icId in icIds)
            {
                _ = _disabledAllRouteIcs.Add(icId);
            }
        }

        internal void DisableAbVariant(string icId, string icCountVariant)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(icId);
            ArgumentException.ThrowIfNullOrWhiteSpace(icCountVariant);
            _ = _disabledAbVariants.Add($"{icId}|{icCountVariant}");
        }

        internal void DisableEveryAbRoute()
        {
            _disableEveryAbRoute = true;
        }

        internal void DisableEveryRoute()
        {
            _disableEveryRoute = true;
        }

        internal void DisableWorkflowFor(string workflowId, params string[] icIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
            foreach (string icId in icIds)
            {
                _ = _disabledWorkflowIcs.Add($"{workflowId}|{icId}");
            }
        }

        internal void DisableEveryWorkflow(string workflowId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
            _ = _disabledWorkflows.Add(workflowId);
        }

        internal void EnableEveryAbRoute()
        {
            _disableEveryAbRoute = false;
            _disabledAbIcs.Clear();
            _disabledAbVariants.Clear();
        }

        internal CanonicalCapabilityPolicySnapshot Load()
        {
            CanonicalCapabilityPolicySnapshot policy =
                RetainedDpReplaceRegressionPolicy.Load();
            CanonicalCapabilityPolicyRoute[] routes =
            [
                .. policy.Routes.Select(route => IsDisabledRoute(route)
                    ? route with
                    {
                        Authoring = new PinnedCapabilityDecision<CapabilityAuthoringAvailability>(
                            $"{route.Authoring.DecisionId}-catalog-refresh-test",
                            route.Identity.RouteId,
                            route.CapabilityFingerprint,
                            CapabilityAuthoringAvailability.Unavailable,
                            "test-only:catalog-refresh-unavailable-ab"),
                    }
                    : route),
            ];
            return policy with
            {
                SourceSha256 = _disabledAbIcs.Count == 0 &&
                    _disabledAllRouteIcs.Count == 0 &&
                    _disabledAbVariants.Count == 0 &&
                    _disabledWorkflowIcs.Count == 0 &&
                    _disabledWorkflows.Count == 0 &&
                    !_disableEveryAbRoute &&
                    !_disableEveryRoute
                    ? policy.SourceSha256
                    : new string('e', 64),
                Routes = Array.AsReadOnly(routes),
            };
        }

        private bool IsDisabledRoute(CanonicalCapabilityPolicyRoute route)
        {
            return _disableEveryRoute ||
                _disabledAllRouteIcs.Contains(route.Identity.IcId) ||
                _disabledWorkflows.Contains(route.Identity.WorkflowId) ||
                _disabledWorkflowIcs.Contains(
                    $"{route.Identity.WorkflowId}|{route.Identity.IcId}") ||
                (StringComparer.Ordinal.Equals(
                        route.Identity.WorkflowId,
                        ExperienceIds.AbMerge) &&
                    (_disableEveryAbRoute ||
                        _disabledAbIcs.Contains(route.Identity.IcId) ||
                        _disabledAbVariants.Contains(
                            $"{route.Identity.IcId}|{route.Identity.IcCountVariant}")));
        }
    }

    private sealed class RecordingStandardMergeAuthoring(IStandardMergeAuthoring inner)
        : IStandardMergeAuthoring
    {
        private Func<bool> _destinationPublished = static () => false;
        private Action<string> _onCall = static _ => { };
        internal List<(string Method, string IcId, bool DestinationPublished)> Calls { get; } = [];

        internal int GetInputAddressSpacesCalls { get; private set; }

        internal int GetAuthoringSnapshotCalls { get; private set; }

        internal void Reset(
            Func<bool> destinationPublished,
            Action<string>? onCall = null)
        {
            Calls.Clear();
            GetInputAddressSpacesCalls = 0;
            GetAuthoringSnapshotCalls = 0;
            _destinationPublished = destinationPublished;
            _onCall = onCall ?? (static _ => { });
        }

        public bool IsSupported(string icId)
        {
            Record(nameof(IsSupported), icId);
            return inner.IsSupported(icId);
        }

        public string? GetProfileId(string icId)
        {
            Record(nameof(GetProfileId), icId);
            return inner.GetProfileId(icId);
        }

        public IReadOnlyList<string> GetRequiredAddressSpaces(string icId)
        {
            Record(nameof(GetRequiredAddressSpaces), icId);
            return inner.GetRequiredAddressSpaces(icId);
        }

        public IReadOnlyList<string> GetInputAddressSpaces(string icId)
        {
            GetInputAddressSpacesCalls++;
            Record(nameof(GetInputAddressSpaces), icId);
            return inner.GetInputAddressSpaces(icId);
        }

        public CompiledAuthoringSelectionSnapshot GetAuthoringSnapshot(
            string icId,
            IReadOnlyCollection<string> selectedSlotIds,
            IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
            AuthoringRevision authoringRevision,
            ActiveSessionSnapshot? retainedSession = null)
        {
            GetAuthoringSnapshotCalls++;
            Record(nameof(GetAuthoringSnapshot), icId);
            return inner.GetAuthoringSnapshot(
                icId,
                selectedSlotIds,
                acceptedFileStamps,
                authoringRevision,
                retainedSession);
        }

        public CompiledAuthoringSessionPreparation PrepareSession(
            AuthoringSessionState session,
            string icId,
            IReadOnlyCollection<CompiledAuthoringSelectedInput> inputs)
        {
            Record(nameof(PrepareSession), icId);
            return inner.PrepareSession(session, icId, inputs);
        }

        private void Record(string method, string icId)
        {
            Calls.Add((method, icId, _destinationPublished()));
            _onCall(method);
        }
    }

}

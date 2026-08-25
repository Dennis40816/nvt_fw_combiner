namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>The Application catalog owns reload, lazy publication, and typed queries without a Bootstrap lifecycle host.</summary>
    [Fact]
    public void CanonicalCapabilityCatalogOwnsItsPublicationLifecycle()
    {
        string catalog = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCatalog.cs");

        Assert.Contains("ICanonicalCapabilityCatalogReloader", catalog, StringComparison.Ordinal);
        Assert.Contains("ICanonicalCapabilityQuery", catalog, StringComparison.Ordinal);
        Assert.Contains("Volatile.Read(ref _latestReload)", catalog, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CanonicalCapabilityCatalogHost.cs")));
    }

    /// <summary>Bootstrap creates one instance graph and never publishes the catalog through a static locator.</summary>
    [Fact]
    public void CanonicalCapabilityCatalogIsInjectedWithoutAStaticBootstrapLocator()
    {
        string wiring = ReadText(
            "src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs");
        string source = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCatalogSource.cs");
        string catalog = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCatalog.cs");
        string reloadPort = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/ICanonicalCapabilityCatalogReloader.cs");
        string bootstrap = ReadProductionSources();

        Assert.Contains("public sealed class CompositionHostServices", wiring, StringComparison.Ordinal);
        Assert.Contains("public static CompositionHostServices Create()", wiring, StringComparison.Ordinal);
        Assert.Contains("ICanonicalCapabilityCatalogReloader", catalog, StringComparison.Ordinal);
        Assert.Contains("ICanonicalCapabilityCatalogLoader", catalog, StringComparison.Ordinal);
        Assert.Contains("void Reload(", reloadPort, StringComparison.Ordinal);
        Assert.Contains("void ICanonicalCapabilityCatalogReloader.Reload", catalog, StringComparison.Ordinal);
        Assert.Contains("LoadAsync(", catalog, StringComparison.Ordinal);
        Assert.Contains(
            "Channel.CreateUnbounded<CanonicalCapabilityCatalogLoadUpdate>",
            catalog,
            StringComparison.Ordinal);
        Assert.Contains(
            "updates.Reader.ReadAllAsync(CancellationToken.None)",
            catalog,
            StringComparison.Ordinal);
        Assert.Contains("ICanonicalCapabilityCatalogSource.Load(", source, StringComparison.Ordinal);
        Assert.Contains(
            "ChannelWriter<CanonicalCapabilityCatalogLoadUpdate>? progress",
            source,
            StringComparison.Ordinal);
        Assert.Contains("completedRoutes / (totalRoutes + 1)", source, StringComparison.Ordinal);
        Assert.Contains("progress?.TryWrite", source, StringComparison.Ordinal);
        Assert.Contains("result.Succeeded ? 1 : null", catalog, StringComparison.Ordinal);
        Assert.Contains("updates.TryComplete(failure)", catalog, StringComparison.Ordinal);
        Assert.Contains(
            "public ICanonicalCapabilityCatalogLoader CanonicalCatalogLoader => Catalog;",
            wiring,
            StringComparison.Ordinal);
        Assert.DoesNotContain("IAsyncEnumerable<double>", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain(" Start(", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("Channel", wiring, StringComparison.Ordinal);
        Assert.DoesNotContain("AsyncLocal", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Action<double>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IProgress<double>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WarmCanonicalCapabilities", wiring, StringComparison.Ordinal);
        Assert.DoesNotContain("StartCanonicalCatalogLoad", wiring, StringComparison.Ordinal);
        Assert.DoesNotContain("static CanonicalCapabilityCatalog", wiring, StringComparison.Ordinal);
        Assert.DoesNotContain("static CanonicalCapabilityCompilerAdapter", wiring, StringComparison.Ordinal);
        Assert.DoesNotContain("static CanonicalCapabilityProjection", wiring, StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalCapabilityQuery", wiring, StringComparison.Ordinal);
        Assert.DoesNotContain("Projection", wiring, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionHostServices.Canonical", bootstrap, StringComparison.Ordinal);
        AssertBootstrapTestsDoNotMutateTheSharedCatalogPublication();
    }

    /// <summary>The injected execution port is the execution owner, not a second forwarding shell.</summary>
    [Fact]
    public void CompositionExecutionExperienceDirectlyOwnsTheInjectedPort()
    {
        string execution = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");

        Assert.Contains(
            "internal sealed class CompositionExecutionExperience : ICompositionExecution",
            execution,
            StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CompositionExecutionPort.cs")));
    }

    /// <summary>The catalog is an Application read model and never a second firmware compiler.</summary>
    [Fact]
    public void CanonicalCapabilityCatalogStaysApplicationOwnedAndReferenceOnly()
    {
        string catalog = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCatalog.cs");
        string models = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityModels.cs");
        string applicationCapabilitySources = string.Join(Environment.NewLine, catalog, models);

        Assert.Contains("CompiledComposition", models, StringComparison.Ordinal);
        Assert.Contains("ICanonicalCapabilityCatalogSource", catalog, StringComparison.Ordinal);
        Assert.Contains("ResolutionToken", models, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Infrastructure", applicationCapabilitySources, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Bootstrap", applicationCapabilitySources, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Text.Json", applicationCapabilitySources, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", applicationCapabilitySources, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.", applicationCapabilitySources, StringComparison.Ordinal);
        Assert.DoesNotContain("new ByteRange(", applicationCapabilitySources, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionOperation.", applicationCapabilitySources, StringComparison.Ordinal);
    }

    /// <summary>Selector disclosure is one token-bound Application child and never a second firmware model.</summary>
    [Fact]
    public void CapabilitySelectorPublicationIsApplicationOwnedImmutableAndFirmwareNeutral()
    {
        string selector = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/CapabilitySelectorPublication.cs");
        string models = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityModels.cs");
        string experience = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityExperience.cs");
        string abCompiler = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCompiler.AbMerge.cs");
        string deviceContext = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.DeviceContext.cs");
        string workflowSetup = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowContextSetupViewModel.cs");
        string mergeState = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.State.cs");
        string mergeRequirements = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.Requirements.cs");
        string replaceState = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.State.cs");
        string replaceGeneral = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.General.cs");
        string selectorConsumers = string.Join(
            Environment.NewLine,
            deviceContext,
            mergeState,
            mergeRequirements,
            replaceState,
            replaceGeneral);
        string presentation = ReadPresentationSources();
        string production = ReadProductionSources();

        Assert.Contains("public sealed class CapabilitySelectorPublication", selector, StringComparison.Ordinal);
        Assert.Contains("ResolutionToken", selector, StringComparison.Ordinal);
        Assert.Contains("string? DefaultIcId", selector, StringComparison.Ordinal);
        Assert.Contains("SelectorPublication = CapabilitySelectorPublication.Create", models, StringComparison.Ordinal);
        Assert.Contains(
            "public CapabilitySelectorPublication SelectorPublication { get; }",
            models,
            StringComparison.Ordinal);
        Assert.Contains("GetSelectorPublication()", experience, StringComparison.Ordinal);
        Assert.Contains("AbMergeTopologyChoiceProjection.Project", selector, StringComparison.Ordinal);
        Assert.Contains(".SelectorPublication", abCompiler, StringComparison.Ordinal);
        Assert.Contains(".GetAbMergeTopologyChoices(icId)", abCompiler, StringComparison.Ordinal);
        Assert.DoesNotContain("CompiledComposition", selector, StringComparison.Ordinal);
        Assert.DoesNotContain("ByteRange", selector, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionOperation", selector, StringComparison.Ordinal);
        Assert.DoesNotContain("NT519", selector, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lazy<CapabilitySelectorPublication", selector, StringComparison.Ordinal);
        Assert.DoesNotContain("Lazy<CapabilitySelectorPublication", deviceContext, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAbMergeProfileSummaries", deviceContext, StringComparison.Ordinal);
        Assert.DoesNotContain("AbMergeAuthoring.GetTopologyChoices", deviceContext, StringComparison.Ordinal);
        Assert.DoesNotContain("AbMergeAuthoring.IsAvailable", deviceContext, StringComparison.Ordinal);
        Assert.DoesNotContain("Capabilities.GetIcIds", workflowSetup, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(production, "CapabilitySelectorPublication.Create("));
        Assert.Equal(1, CountOccurrences(production, "AbMergeTopologyChoiceProjection.Project("));
        Assert.DoesNotContain("AbMergeAuthoring.IsAvailable", selectorConsumers, StringComparison.Ordinal);
        Assert.DoesNotContain("AbMergeAuthoring.GetTopologyChoices", selectorConsumers, StringComparison.Ordinal);
        Assert.DoesNotContain("StandardMergeAuthoring.IsSupported", selectorConsumers, StringComparison.Ordinal);
        Assert.DoesNotContain("Capabilities.IsReplaceWorkflowAvailable", selectorConsumers, StringComparison.Ordinal);
        Assert.DoesNotContain("new CapabilityWorkflowReadiness", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("CapabilityEvidenceStatus.", replaceState, StringComparison.Ordinal);
        Assert.Contains(
            "CapabilityWorkflowReadiness? SelectedReplaceWorkflowReadiness",
            replaceState,
            StringComparison.Ordinal);
        int stageStart = mergeState.IndexOf(
            "internal bool StageStandardMergeForCatalogReconciliation()",
            StringComparison.Ordinal);
        int publishStart = mergeState.IndexOf(
            "internal void PublishCatalogReconciledMergeMode()",
            StringComparison.Ordinal);
        Assert.True(stageStart >= 0 && publishStart > stageStart);
        string stagedFallback = mergeState[stageStart..publishStart];
        Assert.DoesNotContain("RefreshMergeSlotRequirements", stagedFallback, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshMergeMemoryMapState", stagedFallback, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshSelectedFirmwareInspections", stagedFallback, StringComparison.Ordinal);
        Assert.DoesNotContain("_compositionServices", stagedFallback, StringComparison.Ordinal);
        Assert.DoesNotContain("NotifyContextChanged", stagedFallback, StringComparison.Ordinal);
        Assert.True(
            deviceContext.IndexOf(
                "PublishActiveSelectorState(activeIc, activeNumber);",
                StringComparison.Ordinal) <
            deviceContext.IndexOf(
                "_merge.PublishCatalogReconciledMergeMode();",
                StringComparison.Ordinal));
        Assert.Contains(
            "_selectorPublication.ResolutionToken == publication.ResolutionToken",
            deviceContext,
            StringComparison.Ordinal);
        int zeroStart = deviceContext.IndexOf(
            "if (publication.IcIds.Count == 0)",
            StringComparison.Ordinal);
        int nonZeroStart = deviceContext.IndexOf(
            "string mergeIc = ResolveWorkflowContextIc",
            zeroStart,
            StringComparison.Ordinal);
        Assert.True(zeroStart >= 0 && nonZeroStart > zeroStart);
        string zeroAuthorableBranch = deviceContext[zeroStart..nonZeroStart];
        Assert.Contains("InvalidateWorkflowContextDraft();", zeroAuthorableBranch, StringComparison.Ordinal);
        Assert.Contains("PublishActiveSelectorState(string.Empty", zeroAuthorableBranch, StringComparison.Ordinal);
        Assert.Contains("return;", zeroAuthorableBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshContextState", zeroAuthorableBranch, StringComparison.Ordinal);
    }

    /// <summary>Application joins reviewed policy to compiler results without a Bootstrap materialization source.</summary>
    [Fact]
    public void CanonicalRouteUsesIcNeutralOneWayInventoryAdapter()
    {
        string source = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCatalogSource.cs");
        string wiring = ReadText(
            "src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs");
        string routing = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCompiler.StandardMerge.Routing.cs");
        string canonicalRouting = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCompiler.cs");
        string cli = ReadText(
            "src/NvtFwCombiner.Cli/CliApplication.StandardMerge.cs");
        string presentation = ReadPresentationSources();

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CanonicalCapabilityCatalogMigrationSource.cs")));
        Assert.Contains("resolveCompiledRoute", source, StringComparison.Ordinal);
        Assert.Contains("resolveDynamicRoute", source, StringComparison.Ordinal);
        Assert.Contains("BindCapabilityFingerprint", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInV2RegistrationRegistry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("registration.TryCompile", source, StringComparison.Ordinal);
        Assert.Contains("CanonicalCompiledRouteInventory.Resolve", wiring, StringComparison.Ordinal);
        Assert.Contains("CanonicalDynamicRouteInventory.Resolve", wiring, StringComparison.Ordinal);
        Assert.Contains("TryCompilePublishedDynamicCapability", routing, StringComparison.Ordinal);
        Assert.Contains("TryCompilePublishedStandardMergeCapability", routing, StringComparison.Ordinal);
        Assert.DoesNotContain("TryCompileStandardMergeThroughMigrationAdapter", routing, StringComparison.Ordinal);
        Assert.Contains("CapabilityCatalogIssueCodes.RouteUnavailable", canonicalRouting, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51929", routing, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51929", canonicalRouting, StringComparison.Ordinal);
        Assert.DoesNotContain("IsCanonicalStandardMergePilot", routing, StringComparison.Ordinal);
        Assert.DoesNotContain("IsCanonicalStandardMergePilot", canonicalRouting, StringComparison.Ordinal);
        Assert.Contains("services.StandardMergeAuthoring.PrepareSession(", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("_compositionServices.StandardMergeAuthoring.IsSupported", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalAuthoringAdapter", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("new ByteRange(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionOperation.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("0x", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NvtFwCombiner.Bootstrap", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Infrastructure", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new CanonicalCapabilityCatalog(", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalCapabilityCatalog catalog", presentation, StringComparison.Ordinal);
    }

    /// <summary>DP Replace has one accepted session path and no path-backed planning reconstruction.</summary>
    [Fact]
    public void DpReplaceExecutesOnlyTheAcceptedApplicationSession()
    {
        string execution = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");
        string sharedExecution = execution;
        string cli = ReadText(
            "src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Dp.cs");
        string presentation = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Authoring.cs");

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CompositionPlanningAdapter.Replace.Dp.Context.cs")));
        Assert.Contains("ExecuteAcceptedCompositionAsync(", execution, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(
            sharedExecution,
            "AcceptedSessionExecutionInputs.CreateBindings("));
        Assert.DoesNotContain("TryCreateBuiltInV2DpReplaceRunContext", execution, StringComparison.Ordinal);
        Assert.DoesNotContain("new AuthoringRevision(", execution, StringComparison.Ordinal);
        Assert.Contains("services.DpReplaceAuthoring.PrepareSession", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionExecutionAdapter.RunReplaceAsync", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("_dpReplaceSelection", presentation, StringComparison.Ordinal);
    }
}

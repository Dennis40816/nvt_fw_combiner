using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.Infrastructure.Composition;
using NvtFwCombiner.Infrastructure.Diagnostics;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Shell;
using NvtFwCombiner.Infrastructure.Time;

namespace NvtFwCombiner.Bootstrap;

/// <summary>One explicitly constructed Bootstrap dependency graph.</summary>
public sealed class CompositionHostServices
{
    private CompositionHostServices(
        CanonicalCapabilityCatalog catalog,
        CanonicalCapabilityCompilerAdapter compiler,
        CanonicalCapabilityExperience projection)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        CompositionCapabilityExperience = projection ??
            throw new ArgumentNullException(nameof(projection));
        SavedRuleAuthoring = new BuiltInSavedRuleAuthoring();
        var standardMergeAuthoring = new StandardMergeAuthoringExperience(
            compiler,
            catalog,
            projection);
        StandardMergeAuthoring = standardMergeAuthoring;
        var abMergeAuthoring = new AbMergeAuthoringExperience(compiler, catalog);
        AbMergeAuthoring = abMergeAuthoring;
        var dpReplaceAuthoring = new DpReplaceAuthoringExperience(compiler, catalog);
        DpReplaceAuthoring = dpReplaceAuthoring;
        var runtimeLeases = new RuntimeDependencyReadinessLeaseProvider();
        GeneralAuthoring = new GeneralAuthoringExperience(
            new BuiltInGeneralAuthoringPlanner(catalog, compiler, projection),
            new BuiltInGeneralSelectedFileContentInspector(),
            runtimeLeases,
            new SystemClock());
        var ctrlRamAuthoring = new CtrlRamAuthoringExperience(
            new BuiltInCtrlRamAuthoringAdapter(catalog, projection),
            runtimeLeases);
        CtrlRamAuthoring = ctrlRamAuthoring;
        FirmwareInspectionExperience = new BuiltInFirmwareInspection(
            catalog,
            projection,
            standardMergeAuthoring,
            abMergeAuthoring,
            dpReplaceAuthoring,
            ctrlRamAuthoring);
        CompositionOutputNaming = new CompositionOutputNamingExperience(
            catalog,
            new SystemClock());
        CompositionExecution = new CompositionExecutionExperience(
            catalog,
            new ProtectedCompositionDestinationProvider(),
            AcquireExternalProcessorLease,
            ExternalProcessorFactory.IsCurrent,
            new SystemClock());
        RawBinaryEditorFileSessions = new RawBinaryEditorFileSessionFactory();
    }

    internal CanonicalCapabilityCatalog Catalog { get; }

    internal CanonicalCapabilityCompilerAdapter Compiler { get; }

    internal static ICanonicalCapabilityCatalogSource
        CreateCanonicalCapabilityCatalogSource(
            Func<CanonicalCapabilityPolicySnapshot>? loadPolicy = null)
    {
        return new CanonicalCapabilityCatalogSource(
            loadPolicy ?? BuiltInCanonicalCapabilityPolicy.Load,
            CanonicalDynamicRouteInventory.IsDynamic,
            CanonicalCompiledRouteInventory.Resolve,
            CanonicalDynamicRouteInventory.Resolve,
            CanonicalCapabilityDisclosureInventory.Create);
    }

    /// <summary>Creates one isolated host graph at an executable composition root.</summary>
    public static CompositionHostServices Create()
    {
        var catalog = new CanonicalCapabilityCatalog(
            CreateCanonicalCapabilityCatalogSource());
        var compiler = new CanonicalCapabilityCompilerAdapter(
            catalog,
            new BuiltInV2DynamicCompilationAdapter());
        return new CompositionHostServices(
            catalog,
            compiler,
            new CanonicalCapabilityExperience(catalog, catalog));
    }

    private static CompositionExternalProcessorLease AcquireExternalProcessorLease()
    {
        ExternalProcessorGenerationLease lease = ExternalProcessorFactory.AcquireCurrent();
        return new CompositionExternalProcessorLease(
            lease.Generation,
            lease.Processor);
    }

    /// <summary>Gets the focused query over the host's single canonical catalog publication.</summary>
    public ICanonicalSupportMatrixQuery CanonicalSupportMatrixQuery =>
        Catalog;

    /// <summary>Gets the focused capability experience port.</summary>
    public ICompositionCapabilityExperience CompositionCapabilityExperience { get; }

    /// <summary>Gets the focused Saved Rule v2 authoring owner.</summary>
    public ISavedRuleAuthoring SavedRuleAuthoring { get; }

    /// <summary>Gets the focused Standard Merge authoring owner.</summary>
    public IStandardMergeAuthoring StandardMergeAuthoring { get; }

    /// <summary>Gets the focused AB Merge authoring owner.</summary>
    public IAbMergeAuthoring AbMergeAuthoring { get; }

    /// <summary>Gets the focused DP Replace authoring owner.</summary>
    public IDpReplaceAuthoring DpReplaceAuthoring { get; }

    /// <summary>Gets the focused General Merge and Replace authoring owner.</summary>
    public IGeneralAuthoring GeneralAuthoring { get; }

    /// <summary>Gets the focused CtrlRAM Replace authoring owner.</summary>
    public ICtrlRamAuthoring CtrlRamAuthoring { get; }

    /// <summary>Gets the focused immutable firmware-inspection port.</summary>
    public IFirmwareInspection FirmwareInspectionExperience { get; }

    /// <summary>Gets the focused output-naming port.</summary>
    public ICompositionOutputNaming CompositionOutputNaming { get; }

    /// <summary>Gets the focused accepted-session execution port.</summary>
    public ICompositionExecution CompositionExecution { get; }

    /// <summary>Gets the platform-backed raw-BIN file-session factory.</summary>
    public IRawBinaryEditorFileSessionFactory RawBinaryEditorFileSessions { get; }

    /// <summary>Warms the canonical catalog on the caller-owned background worker.</summary>
    public void WarmCanonicalCapabilities(CancellationToken cancellationToken)
    {
        Catalog.Warm(cancellationToken);
    }

    /// <summary>Creates a focused current-session System Information lifecycle.</summary>
    public ISystemInformationService CreateSystemInformationService(
        string applicationVersion)
    {
        return new SystemInformationService(
            applicationVersion,
            CanonicalSupportMatrixQuery,
            Catalog,
            new SystemRuntimeProbe(),
            new SystemClock());
    }

    /// <summary>Creates the privacy-filtered local diagnostic JSON exporter.</summary>
    public static ISystemDiagnosticsExporter CreateSystemDiagnosticsExporter()
    {
        return new JsonSystemDiagnosticsExporter();
    }

    /// <summary>Creates the constrained Windows file-reveal adapter.</summary>
    public static IFileRevealService CreateFileRevealService()
    {
        return new WindowsExplorerFileRevealService();
    }
}

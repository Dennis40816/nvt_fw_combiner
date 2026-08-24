using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.Infrastructure.Composition;
using NvtFwCombiner.Infrastructure.Diagnostics;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Shell;
using NvtFwCombiner.Infrastructure.Time;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Bootstrap;

/// <summary>One explicitly constructed Bootstrap dependency graph.</summary>
public sealed class CompositionHostServices
{
    private CompositionHostServices(
        CanonicalCapabilityCatalog catalog,
        CanonicalCapabilityCompilerAdapter compiler,
        CanonicalCapabilityExperience projection,
        ExternalProcessorEnvironmentLoader externalEnvironment)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Compiler = compiler;
        CompositionCapabilityExperience = projection;
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
        ExternalEnvironment = externalEnvironment ??
            throw new ArgumentNullException(nameof(externalEnvironment));
        GeneralAuthoring = new GeneralAuthoringExperience(
            new BuiltInGeneralAuthoringPlanner(catalog, compiler, projection),
            new FileContentSnapshotInspector(),
            externalEnvironment,
            new SystemClock());
        var ctrlRamAuthoring = new CtrlRamAuthoringExperience(
            new BuiltInCtrlRamAuthoringAdapter(catalog, projection),
            externalEnvironment);
        CtrlRamAuthoring = ctrlRamAuthoring;
        FirmwareInspectionExperience = new BuiltInFirmwareInspection(
            catalog,
            projection,
            standardMergeAuthoring,
            abMergeAuthoring,
            dpReplaceAuthoring,
            ctrlRamAuthoring);
        var artifactIdentityPolicy = new FileSystemCompositionArtifactIdentityPolicy();
        var bundleDestinationValidator =
            new FileSystemCompositionOutputBundleDestinationValidator();
        CompositionOutputNaming = new CompositionOutputNamingExperience(
            catalog,
            new SystemClock(),
            artifactIdentityPolicy,
            bundleDestinationValidator);
        CompositionExecution = new CompositionExecutionExperience(
            catalog,
            new ProtectedCompositionDestinationProvider(),
            () =>
            {
                ExternalProcessorEnvironmentLease lease = externalEnvironment.AcquireCurrent();
                return new(lease.Generation, lease.Processor);
            },
            externalEnvironment.IsCurrent,
            new SystemClock());
        RawBinaryEditorFileSessions = new RawBinaryEditorFileSessionFactory();
        LocalFiles = new LocalFileStore();
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
        return Create(BuiltInCanonicalCapabilityPolicy.Load);
    }

    internal static CompositionHostServices Create(
        ExternalProcessorEnvironmentLoader externalEnvironment)
    {
        return Create(externalEnvironment, loadPolicy: null);
    }

    internal static CompositionHostServices Create(Func<CanonicalCapabilityPolicySnapshot> loadPolicy)
    {
        ArgumentNullException.ThrowIfNull(loadPolicy);
        return Create(new(), loadPolicy);
    }

    internal static CompositionHostServices Create(
        ExternalProcessorEnvironmentLoader externalEnvironment,
        Func<CanonicalCapabilityPolicySnapshot>? loadPolicy)
    {
        var catalog = new CanonicalCapabilityCatalog(
            CreateCanonicalCapabilityCatalogSource(loadPolicy));
        var compiler = new CanonicalCapabilityCompilerAdapter(
            catalog,
            new BuiltInV2DynamicCompilationAdapter());
        return new CompositionHostServices(
            catalog,
            compiler,
            new CanonicalCapabilityExperience(catalog, catalog),
            externalEnvironment);
    }

    /// <summary>Gets the focused query over the host's single canonical catalog publication.</summary>
    public ICanonicalSupportMatrixQuery CanonicalSupportMatrixQuery => Catalog;

    /// <summary>Gets the focused Application-owned catalog loading port.</summary>
    public ICanonicalCapabilityCatalogLoader CanonicalCatalogLoader => Catalog;

    /// <summary>Gets the one bounded external environment discovery and refresh owner.</summary>
    public IExternalProcessorEnvironmentLoader ExternalEnvironmentLoader => ExternalEnvironment;

    internal ExternalProcessorEnvironmentLoader ExternalEnvironment { get; }

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

    /// <summary>Gets the bounded local-file adapter.</summary>
    public ILocalFileStore LocalFiles { get; }

    /// <summary>Creates a stateless adapter for startup work that precedes host composition.</summary>
    public static ILocalFileStore CreateLocalFileStore()
    {
        return new LocalFileStore();
    }

    /// <summary>Creates the typed desktop managed-version use-case graph.</summary>
    /// <param name="applicationVersion">Running canonical stable version.</param>
    /// <param name="managedRoot">Optional stable managed root override.</param>
    /// <param name="statePath">Optional launcher-state path override.</param>
    /// <returns>One session-scoped version-management experience.</returns>
    public static IVersionManagementExperience CreateVersionManagementExperience(
        string applicationVersion,
        string? managedRoot = null,
        string? statePath = null)
    {
        ManagedAppVersion version = ManagedAppVersion.Parse(applicationVersion);
        string root = managedRoot ?? ManagedInstallationLayout.ResolveManagedRoot(AppContext.BaseDirectory);
        return new VersionManagementExperience(
            version,
            root,
            new JsonVersionManagerStateStore(statePath ?? JsonVersionManagerStateStore.GetDefaultPath()),
            new FileSystemUpdateCatalogSource(),
            new FileSystemManagedVersionRepository());
    }

    /// <summary>Creates the inherited one-use app-side ready signal.</summary>
    /// <returns>The platform ready-signal adapter.</returns>
    public static IApplicationReadySignal CreateApplicationReadySignal()
    {
        return new InheritedPipeApplicationReadySignal();
    }

    /// <summary>Creates the exact stable-launcher shutdown handoff.</summary>
    /// <param name="managedRoot">Optional stable managed root override.</param>
    /// <returns>The constrained launcher handoff.</returns>
    public static IStableLauncherHandoff CreateStableLauncherHandoff(string? managedRoot = null)
    {
        return new StableLauncherHandoff(
            managedRoot ?? ManagedInstallationLayout.ResolveManagedRoot(AppContext.BaseDirectory));
    }

    /// <summary>Creates a focused current-session System Information lifecycle.</summary>
    public ISystemInformationService CreateSystemInformationService(
        string applicationVersion)
    {
        return new SystemInformationService(
            applicationVersion,
            CanonicalSupportMatrixQuery,
            Catalog,
            ExternalEnvironment,
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

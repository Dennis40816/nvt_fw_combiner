using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.Infrastructure.Composition;
using NvtFwCombiner.Infrastructure.Configuration;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Diagnostics;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Shell;
using NvtFwCombiner.Infrastructure.Time;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Bootstrap;

/// <summary>One explicitly constructed Bootstrap dependency graph.</summary>
public sealed partial class CompositionHostServices
{
    /// <summary>
    /// Names the runtime switch a process sets to forbid the current user's local-state folder. Every test project
    /// sets it, so the default resolvers of preferences, report history, toolchain runtime, Event Buffer format
    /// and version-manager state refuse before local-state IO. Explicit paths and child product processes are outside
    /// this guard.
    /// </summary>
    internal const string CurrentUserLocalStateForbiddenSwitch =
        JsonVersionManagerStateStore.CurrentUserLocalStateForbiddenSwitch;

    internal const string LocalStateFolderName = "NvtFwCombiner";
    private const string ToolchainRuntimeFileName = "toolchain-runtime.v1.json";
    private const string EventBufferFormatFileName = "event-buffer-format.v1.json";
    private readonly Lock _configurationGate = new();
    private readonly Func<FirmwareFamilyResolutionDefinition> _loadConfigurationFamily;
    private readonly string? _configurationPath;
    private Task<IEventBufferFormatConfigurationSession>? _configuration;
    private readonly IToolchainRuntimeConfigurationSession? _toolchainConfiguration;
    private Task<IToolchainRuntimeConfigurationSession>? _toolchainConfigurationLoad;

    private CompositionHostServices(
        CanonicalCapabilityCatalog catalog,
        CanonicalCapabilityCompilerAdapter compiler,
        CanonicalCapabilityExperience projection,
        ExternalProcessorEnvironmentLoader externalEnvironment,
        string localStateDirectory,
        Func<FirmwareFamilyResolutionDefinition>? loadConfigurationFamily,
        string? configurationPath,
        ILocalFileStore? localFiles = null,
        IToolchainRuntimeConfigurationSession? toolchainConfiguration = null)
    {
        _loadConfigurationFamily = loadConfigurationFamily ??
            (() => (BuiltInV2RegistrationRegistry.FindAbMergeRegistration("NT51950", "nt51950-ab-merge-maps") ??
                throw new InvalidDataException("The declared Event Buffer configuration family is unavailable.")).GetFirmwareFamily());
        LocalStateDirectory = localStateDirectory;
        _configurationPath = configurationPath;
        _toolchainConfiguration = toolchainConfiguration;
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Compiler = compiler;
        CompositionCapabilityExperience = projection;
        SavedRuleAuthoring = new BuiltInSavedRuleAuthoring();
        var standardMergeAuthoring = new StandardMergeAuthoringExperience(
            compiler,
            catalog,
            projection);
        StandardMergeAuthoring = standardMergeAuthoring;
        var abMergeAuthoring = new AbMergeAuthoringExperience(
            compiler,
            catalog,
            externalEnvironment,
            GetEventBufferFormatConfigurationAsync);
        AbMergeAuthoring = abMergeAuthoring;
        ExternalEnvironment = externalEnvironment ??
            throw new ArgumentNullException(nameof(externalEnvironment));
        GeneralAuthoring = new GeneralAuthoringExperience(
            new BuiltInGeneralAuthoringPlanner(catalog, compiler, projection),
            new FileContentSnapshotInspector(),
            externalEnvironment,
            new SystemClock());
        var artifactClassification = new FirmwareArtifactClassificationResolver(catalog, compiler);
        var ctrlRamAuthoring = new CtrlRamAuthoringExperience(
            new BuiltInCtrlRamAuthoringAdapter(catalog, projection),
            externalEnvironment,
            artifactClassification);
        CtrlRamAuthoring = ctrlRamAuthoring;
        FirmwareInspectionExperience = new BuiltInFirmwareInspection(
            new FirmwareMetadataPlanAuthorityResolver(catalog, compiler),
            projection,
            standardMergeAuthoring,
            abMergeAuthoring,
            ctrlRamAuthoring,
            artifactClassification);
        var artifactIdentityPolicy = new FileSystemCompositionArtifactIdentityPolicy();
        var bundleDestinationValidator =
            new FileSystemCompositionOutputBundleDestinationValidator();
        CompositionOutputNaming = new CompositionOutputNamingExperience(
            catalog,
            new SystemClock(),
            artifactIdentityPolicy,
            bundleDestinationValidator,
            abMergeAuthoring.AssessAcceptedFormatAsync);
        CompositionExecution = new CompositionExecutionExperience(
            catalog,
            new ProtectedCompositionDestinationProvider(),
            () =>
            {
                ExternalProcessorEnvironmentLease lease = externalEnvironment.AcquireCurrent();
                return new(lease.Generation, lease.Processor);
            },
              externalEnvironment.IsCurrent,
              new SystemClock(),
              abMergeAuthoring);
        RawBinaryEditorFileSessions = new RawBinaryEditorFileSessionFactory();
        LocalFiles = localFiles ?? new LocalFileStore();
    }

    /// <summary>Gets the one directory this host graph reads and writes its per-user local-state files under.</summary>
    public string LocalStateDirectory { get; }

    internal CanonicalCapabilityCatalog Catalog { get; }

    internal CanonicalCapabilityCompilerAdapter Compiler { get; }

    /// <summary>
    /// Resolves the current user's local-state folder, the production default for preferences, report history,
    /// toolchain runtime and Event Buffer format files. Executable composition roots call it once; a process that
    /// sets the <c>NvtFwCombiner.LocalState.CurrentUserFolderForbidden</c> runtime switch fails closed instead.
    /// </summary>
    public static string ResolveCurrentUserLocalStateDirectory()
    {
        return AppContext.TryGetSwitch(CurrentUserLocalStateForbiddenSwitch, out bool forbidden) && forbidden
            ? throw new InvalidOperationException(
                "This process forbids the current user's local-state folder; " +
                "compose the host with an isolated local-state directory.")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                LocalStateFolderName);
    }

    internal static ICanonicalCapabilityCatalogSource
        CreateCanonicalCapabilityCatalogSource(
            Func<CanonicalCapabilityPolicySnapshot>? loadPolicy = null)
    {
        return new CanonicalCapabilityCatalogSource(
            loadPolicy ?? BuiltInCanonicalCapabilityPolicy.Load,
            CanonicalDynamicRouteInventory.IsDynamic,
            CanonicalCompiledRouteInventory.Resolve,
            CanonicalDynamicRouteInventory.CreateResolver,
            CanonicalCapabilityDisclosureInventory.Create,
            CanonicalFullImageMetadataInventory.Create,
            BuiltInV2BundlePreload.Run);
    }

    /// <summary>Creates one isolated host graph over the current user's local-state folder.</summary>
    public static CompositionHostServices Create()
    {
        return Create(ResolveCurrentUserLocalStateDirectory());
    }

    /// <summary>Creates the executable host graph over one explicitly composed local-state directory.</summary>
    /// <param name="localStateDirectory">Fully qualified directory for this host's per-user local-state files.</param>
    public static CompositionHostServices Create(string localStateDirectory)
    {
        return Create(BuiltInCanonicalCapabilityPolicy.Load, localStateDirectory);
    }

    internal static CompositionHostServices Create(
        ExternalProcessorEnvironmentLoader externalEnvironment,
        string localStateDirectory)
    {
        return Create(externalEnvironment, loadPolicy: null, localStateDirectory);
    }

    internal static CompositionHostServices Create(
        Func<CanonicalCapabilityPolicySnapshot> loadPolicy,
        string localStateDirectory)
    {
        ArgumentNullException.ThrowIfNull(loadPolicy);
        ArgumentException.ThrowIfNullOrWhiteSpace(localStateDirectory);
        var files = new LocalFileStore();
        var session = new ToolchainRuntimeConfigurationSession(
            new ToolchainRuntimeConfigurationStorage(files, Path.Combine(localStateDirectory, ToolchainRuntimeFileName)),
            new ToolchainRuntimeCandidateInspector(files, RuntimeTrustProbeProcess.CreateDefault()));
        return Create(new ExternalProcessorEnvironmentLoader(session), loadPolicy, localStateDirectory,
            localFiles: files, toolchainConfiguration: session);
    }

    internal static CompositionHostServices Create(
        ExternalProcessorEnvironmentLoader externalEnvironment,
        Func<CanonicalCapabilityPolicySnapshot>? loadPolicy,
        string localStateDirectory,
        Func<FirmwareFamilyResolutionDefinition>? loadConfigurationFamily = null,
        string? configurationPath = null,
        ILocalFileStore? localFiles = null,
        IToolchainRuntimeConfigurationSession? toolchainConfiguration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localStateDirectory);
        var catalog = new CanonicalCapabilityCatalog(
            CreateCanonicalCapabilityCatalogSource(loadPolicy));
        var compiler = new CanonicalCapabilityCompilerAdapter(
            catalog,
            new BuiltInV2DynamicCompilationAdapter());
        return new CompositionHostServices(
            catalog,
            compiler,
            new CanonicalCapabilityExperience(catalog, catalog),
            externalEnvironment,
            localStateDirectory,
            loadConfigurationFamily,
            configurationPath,
            localFiles,
            toolchainConfiguration);
    }

    /// <summary>
    /// Lazily gets this host's single configuration session. Missing/invalid persisted data remains
    /// explicit; failed host tasks can be retried. Existing trusted-catalog initialization failures may
    /// require repairing the installed files and restarting. Host construction performs no configuration IO.
    /// </summary>
    public Task<IEventBufferFormatConfigurationSession> GetEventBufferFormatConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_configurationGate)
        {
            if (_configuration is null || _configuration.IsFaulted || _configuration.IsCanceled)
            {
                _configuration = CreateEventBufferFormatConfigurationAsync();
            }

            // A caller may stop waiting without cancelling initialization used by other consumers.
            return _configuration.WaitAsync(cancellationToken);
        }
    }

    private async Task<IEventBufferFormatConfigurationSession> CreateEventBufferFormatConfigurationAsync()
    {
        FirmwareFamilyResolutionDefinition family = await Task.Run(_loadConfigurationFamily).ConfigureAwait(false);
        string path = _configurationPath ?? Path.Combine(LocalStateDirectory, EventBufferFormatFileName);
        var session = new EventBufferFormatConfigurationSession(family,
            new EventBufferFormatConfigurationStorage(LocalFiles, path));
        try
        {
            _ = await session.ReloadAsync(CancellationToken.None).ConfigureAwait(false);
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
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
    /// <param name="updateSourceRegistryPath">Optional absolute filesystem or HTTPS Registry locator.</param>
    /// <returns>One session-scoped version-management experience.</returns>
    public static IVersionManagementExperience CreateVersionManagementExperience(
        string applicationVersion,
        string? managedRoot = null,
        string? statePath = null,
        string? updateSourceRegistryPath = null)
    {
        return CreateVersionManagementExperience(
            applicationVersion,
            managedRoot,
            statePath,
            string.IsNullOrWhiteSpace(updateSourceRegistryPath)
                ? []
                : [updateSourceRegistryPath]);
    }

    /// <summary>Creates the typed desktop managed-version graph over ordered Registry replicas.</summary>
    public static IVersionManagementExperience CreateVersionManagementExperience(
        string applicationVersion,
        string? managedRoot,
        string? statePath,
        IReadOnlyList<string> updateSourceRegistryPaths)
    {
        ArgumentNullException.ThrowIfNull(updateSourceRegistryPaths);
        ManagedAppVersion version = ManagedAppVersion.Parse(applicationVersion);
        string root = managedRoot ?? ManagedInstallationLayout.ResolveManagedRoot(AppContext.BaseDirectory);
        string exactStatePath = statePath ?? JsonVersionManagerStateStore.GetDefaultPath();
        IUpdateSourceRegistry[] registries =
        [.. updateSourceRegistryPaths.Select(static path => string.IsNullOrWhiteSpace(path)
            ? new FileSystemUpdateSourceRegistry(path)
            : UpdateSourceRegistryAdapterFactory.Create(path))];
        IUpdateSourceRegistry? sourceRegistry = registries.Length switch
        {
            0 => null,
            _ => new ReplicatedUpdateSourceRegistry(registries),
        };
        return new VersionManagementExperience(
            version,
            root,
            new JsonVersionManagerStateStore(exactStatePath),
            new FileSystemUpdateCatalogSource(),
            new FileSystemManagedVersionRepository(),
            new JsonLauncherMutationFence(exactStatePath),
            sourceRegistry);
    }

    /// <summary>Creates the inherited one-use app-side ready signal.</summary>
    /// <returns>The platform ready-signal adapter.</returns>
    public static IApplicationReadySignal CreateApplicationReadySignal()
    {
        return new InheritedPipeApplicationReadySignal();
    }

    /// <summary>Captures the inherited application READY pipe at Desktop process entry.</summary>
    public static InheritedPipeApplicationReadySignal CaptureInheritedApplicationReadySignal()
    {
        return new InheritedPipeApplicationReadySignal();
    }

    /// <summary>Creates the Application-owned managed-child startup seam.</summary>
    /// <param name="applicationVersion">Running canonical stable version.</param>
    /// <param name="versionManagement">Session-scoped version-management owner.</param>
    /// <returns>One READY-qualified startup coordinator.</returns>
    public static IManagedApplicationStartupCoordinator CreateManagedApplicationStartupCoordinator(
        string applicationVersion,
        IVersionManagementExperience versionManagement)
    {
        return CreateManagedApplicationStartupCoordinator(
            applicationVersion,
            versionManagement,
            CreateApplicationReadySignal());
    }

    /// <summary>Creates startup coordination over an already captured process-entry READY signal.</summary>
    public static IManagedApplicationStartupCoordinator CreateManagedApplicationStartupCoordinator(
        string applicationVersion,
        IVersionManagementExperience versionManagement,
        IApplicationReadySignal applicationReadySignal)
    {
        ArgumentNullException.ThrowIfNull(applicationReadySignal);
        return new ManagedApplicationStartupCoordinator(
            ManagedAppVersion.Parse(applicationVersion),
            applicationReadySignal,
            versionManagement);
    }

    /// <summary>Creates the exact stable-launcher shutdown handoff.</summary>
    /// <param name="managedRoot">Optional stable managed root override.</param>
    /// <param name="statePath">Optional exact version-state path override.</param>
    /// <param name="expectedIdentity">Captured exact Root Bootstrap authority.</param>
    /// <returns>The constrained launcher handoff.</returns>
    public static IStableLauncherHandoff CreateStableLauncherHandoff(
        string? managedRoot = null,
        string? statePath = null,
        ManagedImmutableBootstrapIdentity? expectedIdentity = null)
    {
        return new StableLauncherHandoff(
            managedRoot ?? ManagedInstallationLayout.ResolveManagedRoot(AppContext.BaseDirectory),
            statePath,
            expectedIdentity);
    }

    /// <summary>Captures the inherited managed-process lifetime at desktop entry.</summary>
    public static IInheritedManagedProcessLifetimeCapture CaptureInheritedManagedProcessLifetime(
        string? statePath,
        bool managedOptionsAdvertised)
    {
        return InheritedManagedProcessLifetime.Capture(
            statePath,
            ManagedProcessLifetimeKind.Application,
            managedOptionsAdvertised || InheritedManagedProcessLifetime.IsApplicationReadyContextAdvertised());
    }

    /// <summary>Captures and removes inherited Root Bootstrap authority only for a managed Desktop.</summary>
    /// <param name="lifetimeOutcome">Current Desktop managed-lifetime classification.</param>
    /// <returns>Exact inherited identity, or null when unavailable or not managed.</returns>
    public static ManagedImmutableBootstrapIdentity? CaptureInheritedManagedBootstrapIdentity(
        InheritedManagedProcessLifetimeOutcome lifetimeOutcome)
    {
        return lifetimeOutcome == InheritedManagedProcessLifetimeOutcome.Captured
            ? InheritedManagedBootstrapIdentityContext.CaptureAndClear()
            : null;
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
            new SystemClock(),
            admissionStatus: BuiltInProfileAdmissionStatus.Instance);
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

using System.Reflection;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Terminal result from the thin public distribution Launcher host.</summary>
public sealed record ManagedDistributionLauncherHostResult(
    ManagedLauncherEntryResult? Entry,
    ManagedFirstInstallationExperience? Setup,
    ManagedDistributionLauncherRecoverySession? Recovery,
    ManagedDistributionPayloadIssue PayloadIssue)
{
    /// <summary>Gets whether one exact embedded payload was available to route this invocation.</summary>
    public bool IsPayloadAvailable => PayloadIssue == ManagedDistributionPayloadIssue.None;
}

/// <summary>One explicit recovery result and the canonical entry rerun after completion.</summary>
public sealed record ManagedDistributionLauncherRecoveryExecutionResult(
    ManagedSetupRecoveryExecutionResult Execution,
    ManagedDistributionLauncherHostResult? RefreshedHost);

/// <summary>Root-bound access to the existing recovery diagnosis and execution owners.</summary>
public sealed class ManagedDistributionLauncherRecoverySession
{
    private readonly Func<
        string,
        CancellationToken,
        ValueTask<ManagedSetupRecoveryDiagnosis>> _diagnose;
    private readonly Func<
        ManagedSetupRecoveryPlan,
        ManagedSetupRecoveryAction?,
        TimeSpan,
        CancellationToken,
        ValueTask<ManagedSetupRecoveryExecutionResult>> _execute;
    private readonly Func<
        CancellationToken,
        ValueTask<ManagedDistributionLauncherHostResult>> _rerunEntry;

    internal ManagedDistributionLauncherRecoverySession(
        string managedRoot,
        ManagedInstallationRecoveryExperience diagnosis,
        ManagedSetupRecoveryExecutionCoordinator execution,
        Func<CancellationToken, ValueTask<ManagedDistributionLauncherHostResult>> rerunEntry)
        : this(
            managedRoot,
            diagnosis.DiagnoseAsync,
            execution.ExecuteAsync,
            rerunEntry)
    {
    }

    internal ManagedDistributionLauncherRecoverySession(
        string managedRoot,
        Func<string, CancellationToken, ValueTask<ManagedSetupRecoveryDiagnosis>> diagnose,
        Func<
            ManagedSetupRecoveryPlan,
            ManagedSetupRecoveryAction?,
            TimeSpan,
            CancellationToken,
            ValueTask<ManagedSetupRecoveryExecutionResult>> execute,
        Func<CancellationToken, ValueTask<ManagedDistributionLauncherHostResult>> rerunEntry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        if (!Path.IsPathFullyQualified(managedRoot))
        {
            throw new ArgumentException("Recovery root must be absolute.", nameof(managedRoot));
        }
        ManagedRoot = managedRoot;
        _diagnose = diagnose ?? throw new ArgumentNullException(nameof(diagnose));
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _rerunEntry = rerunEntry ?? throw new ArgumentNullException(nameof(rerunEntry));
    }

    /// <summary>Gets the exact root carried by the typed RecoveryRequired entry.</summary>
    public string ManagedRoot { get; }

    /// <summary>Runs the sole read-only Application recovery diagnosis.</summary>
    public ValueTask<ManagedSetupRecoveryDiagnosis> DiagnoseAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _diagnose(ManagedRoot, cancellationToken);
    }

    /// <summary>Executes an explicit plan and reruns canonical entry only after completion.</summary>
    public async ValueTask<ManagedDistributionLauncherRecoveryExecutionResult> ExecuteAsync(
        ManagedSetupRecoveryPlan plan,
        ManagedSetupRecoveryAction? confirmedAction,
        TimeSpan writerLeaseTimeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ManagedSetupRecoveryExecutionResult execution = await _execute(
            plan,
            confirmedAction,
            writerLeaseTimeout,
            cancellationToken).ConfigureAwait(false);
        ManagedDistributionLauncherHostResult? refreshed =
            execution.Outcome == ManagedSetupRecoveryExecutionOutcome.Completed
                ? await _rerunEntry(cancellationToken).ConfigureAwait(false)
                : null;
        return new(execution, refreshed);
    }
}

/// <summary>
/// One distribution-Launcher composition graph over the existing entry, Setup, and adapter owners.
/// </summary>
public sealed class ManagedDistributionLauncherHostServices : IDisposable
{
    /// <summary>The exact embedded Setup admission descriptor resource.</summary>
    public const string PayloadAdmissionResourceName =
        "NvtFwCombiner.DistributionLauncher.Payload.managed-setup-payload-admission.v1.json";

    /// <summary>The exact embedded immutable Root Bootstrap resource.</summary>
    public const string BootstrapResourceName =
        "NvtFwCombiner.DistributionLauncher.Payload.NvtFwCombiner.Bootstrap.exe";

    /// <summary>The deterministic child root under downloaded delivery media.</summary>
    public const string DefaultManagedRootDirectoryName = "NvtFwCombiner";

    private bool _disposed;

    private ManagedDistributionLauncherHostServices(
        string managedRoot,
        string statePath,
        JsonVersionManagerStateStore stateStore,
        FileSystemManagedInstallationRootProbe rootProbe,
        EmbeddedManagedDistributionPayloadSource payloadSource,
        VersionManagementExperience versionManagement,
        FileSystemManagedFirstInstallationRootMaterializer rootMaterializer,
        StableLauncherHandoff handoff,
        ManagedInstallationRecoveryExperience recoveryExperience,
        ManagedSetupRecoveryExecutionCoordinator recoveryExecution,
        ManagedAppVersion runningLauncherVersion)
    {
        ManagedRoot = managedRoot;
        StatePath = statePath;
        VersionManagement = versionManagement;
        EntryCoordinator = new(
            managedRoot,
            stateStore,
            rootProbe,
            payloadSource,
            runningLauncherVersion,
            handoff);
        SetupExperience = new(
            statePath,
            stateStore,
            rootProbe,
            payloadSource,
            versionManagement,
            rootMaterializer,
            handoff);
        RecoveryExperience = recoveryExperience;
        RecoveryExecution = recoveryExecution;
    }

    /// <summary>Gets the deterministic delivery-media child used only while durable state is missing.</summary>
    public string ManagedRoot { get; }

    /// <summary>Gets the exact shared per-user state path.</summary>
    public string StatePath { get; }

    private VersionManagementExperience VersionManagement { get; }

    private ManagedLauncherEntryCoordinator EntryCoordinator { get; }

    private ManagedFirstInstallationExperience SetupExperience { get; }

    private ManagedInstallationRecoveryExperience RecoveryExperience { get; }

    private ManagedSetupRecoveryExecutionCoordinator RecoveryExecution { get; }

    /// <summary>Creates the production graph from the exact running executable and entry assembly.</summary>
    public static ManagedDistributionLauncherHostServices Create()
    {
        string launcherPath = Environment.ProcessPath ??
            throw new InvalidOperationException("The running Launcher path is unavailable.");
        Assembly launcherAssembly = Assembly.GetEntryAssembly() ??
            throw new InvalidOperationException("The running Launcher assembly is unavailable.");
        string launcherVersion = launcherAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
            throw new InvalidOperationException("The running Launcher version is unavailable.");
        return Create(
            launcherPath,
            launcherVersion,
            launcherAssembly.GetManifestResourceStream,
            Environment.GetEnvironmentVariable,
            JsonVersionManagerStateStore.GetDefaultPath());
    }

    internal static ManagedDistributionLauncherHostServices Create(
        string launcherPath,
        string launcherVersion,
        Func<string, Stream?> openEmbeddedResource,
        Func<string, string?> readEnvironment,
        string statePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(launcherPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(launcherVersion);
        ArgumentNullException.ThrowIfNull(openEmbeddedResource);
        ArgumentNullException.ThrowIfNull(readEnvironment);
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);

        string exactLauncherPath = Path.GetFullPath(launcherPath);
        string deliveryParent = Path.GetDirectoryName(exactLauncherPath) ??
            throw new ArgumentException("Launcher path has no parent directory.", nameof(launcherPath));
        string managedRoot = Path.Combine(deliveryParent, DefaultManagedRootDirectoryName);
        string exactStatePath = Path.GetFullPath(statePath);
        IReadOnlyList<string> registryPaths = UpdateSourceRegistryLocator.ResolveAll(
            explicitLocatorSupplied: false,
            explicitLocator: null,
            readEnvironment);

        var stateStore = new JsonVersionManagerStateStore(exactStatePath);
        var rootProbe = new FileSystemManagedInstallationRootProbe();
        var payloadSource = new EmbeddedManagedDistributionPayloadSource(
            exactLauncherPath,
            () => openEmbeddedResource(PayloadAdmissionResourceName),
            () => openEmbeddedResource(BootstrapResourceName));
        ManagedAppVersion runningLauncherVersion = ManagedAppVersion.Parse(launcherVersion);

        IUpdateSourceRegistry[] registries =
        [.. registryPaths.Select(UpdateSourceRegistryAdapterFactory.Create)];
        IUpdateSourceRegistry? registry = registries.Length == 0
            ? null
            : new ReplicatedUpdateSourceRegistry(registries);
        var repository = new FileSystemManagedVersionRepository();
        var versionManagement = new VersionManagementExperience(
            runningLauncherVersion,
            managedRoot,
            stateStore,
            new FileSystemUpdateCatalogSource(),
            repository,
            new JsonLauncherMutationFence(exactStatePath),
            registry);
        var rootMaterializer = new FileSystemManagedFirstInstallationRootMaterializer(repository);
        var handoff = new StableLauncherHandoff(
            managedRoot,
            exactStatePath);
        var markerProbe = new FileSystemManagedSetupRecoveryProbe();
        var lifetimeProbe = new FileSystemManagedProcessLifetimeProbe();
        var recoveryAdapter = new FileSystemManagedSetupRecoveryExecution(
            exactStatePath,
            repository,
            new FileSystemInstalledLauncherRepository(),
            new JsonLauncherBootstrapStateStore(exactStatePath));
        var recoveryExperience = new ManagedInstallationRecoveryExperience(
            stateStore,
            rootProbe,
            markerProbe,
            lifetimeProbe,
            recoveryAdapter);
        var recoveryExecution = new ManagedSetupRecoveryExecutionCoordinator(
            stateStore,
            rootProbe,
            markerProbe,
            lifetimeProbe,
            recoveryAdapter,
            recoveryAdapter);
        return new(
            managedRoot,
            exactStatePath,
            stateStore,
            rootProbe,
            payloadSource,
            versionManagement,
            rootMaterializer,
            handoff,
            recoveryExperience,
            recoveryExecution,
            runningLauncherVersion);
    }

    /// <summary>
    /// Runs local entry classification. Only SetupRequired receives the existing Setup experience.
    /// </summary>
    public async ValueTask<ManagedDistributionLauncherHostResult> RunAsync(
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ManagedLauncherEntryResult entry = await EntryCoordinator.RunAsync(cancellationToken)
            .ConfigureAwait(false);
        ManagedDistributionPayloadIssue issue =
            entry.Outcome == ManagedLauncherEntryOutcome.PayloadUnavailable
                ? ManagedDistributionPayloadIssue.Unavailable
                : entry.Outcome == ManagedLauncherEntryOutcome.PayloadInvalid
                    ? ManagedDistributionPayloadIssue.Invalid
                    : ManagedDistributionPayloadIssue.None;
        return issue == ManagedDistributionPayloadIssue.None
            ? new(
                entry,
                entry.Outcome == ManagedLauncherEntryOutcome.SetupRequired ? SetupExperience : null,
                entry is
                {
                    Outcome: ManagedLauncherEntryOutcome.RecoveryRequired,
                    ManagedRoot: not null,
                }
                    ? new(
                        entry.ManagedRoot,
                        RecoveryExperience,
                        RecoveryExecution,
                        RunAsync)
                    : null,
                ManagedDistributionPayloadIssue.None)
            : new(null, null, null, issue);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        VersionManagement.Dispose();
    }

}

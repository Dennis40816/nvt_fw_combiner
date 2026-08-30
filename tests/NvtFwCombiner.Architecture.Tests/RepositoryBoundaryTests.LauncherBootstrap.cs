using System.Text.Json;
using System.Text.RegularExpressions;

namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Every production child start crosses the one Platform containment gate.</summary>
    [Fact]
    public void ProductionProcessStartsAreOwnedOnlyByPlatformGate()
    {
        const string processGatePath =
            "src/NvtFwCombiner.Platform/Processes/ProcessLaunchGate.cs";
        const string containedStarterPath =
            "src/NvtFwCombiner.Platform/Processes/WindowsContainedProcessStarter.cs";
        string processGate = ReadText(processGatePath);
        string containedStarter = ReadText(containedStarterPath);
        string app = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/AnonymousPipeManagedApplicationProcess.cs");
        string launcher = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/AnonymousPipeManagedLauncherProcess.cs");
        string bootstrap = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/StableLauncherHandoff.cs");
        string external = ReadText(
            "src/NvtFwCombiner.Infrastructure/ExternalTools/SystemExternalProcessRunner.cs");
        string explorer = ReadText(
            "src/NvtFwCombiner.Infrastructure/Shell/WindowsExplorerFileRevealService.cs");

        AssertContainsAll(
            processGate,
            "private static readonly Lock StartLock",
            "WindowsContainedProcessStarter.Start");
        AssertContainsAll(
            containedStarter,
            "ProcThreadAttributeHandleList",
            "DuplicateHandle(",
            "CreateProcessW");
        Assert.Equal(1, CountOccurrences(app, "ProcessLaunchGate.StartContained("));
        Assert.Equal(1, CountOccurrences(launcher, "ProcessLaunchGate.StartContained("));
        Assert.Equal(2, CountOccurrences(bootstrap, "ProcessLaunchGate.StartContained("));
        Assert.Equal(1, CountOccurrences(external, "ProcessLaunchGate.Start("));
        Assert.Equal(1, CountOccurrences(explorer, "ProcessLaunchGate.Start("));

        string sourceRoot = Path.Combine(Root.FullName, "src");
        var rawOwners = new List<string>();
        foreach (string path in Directory.EnumerateFiles(
                     sourceRoot,
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(Root.FullName, path).Replace('\\', '/');
            string source = File.ReadAllText(path);
            if (RawProcessStartPattern().IsMatch(source) &&
                !StringComparer.Ordinal.Equals(relative, processGatePath))
            {
                rawOwners.Add($"{relative}: Process.Start");
            }
            if (source.Contains("WindowsContainedProcessStarter.Start", StringComparison.Ordinal) &&
                !StringComparer.Ordinal.Equals(relative, processGatePath))
            {
                rawOwners.Add($"{relative}: WindowsContainedProcessStarter.Start");
            }
            if (RawProcessConstructionPattern().IsMatch(source))
            {
                rawOwners.Add($"{relative}: new Process");
            }
            if ((RawCreateProcessPattern().IsMatch(source) ||
                 RawCreateProcessEntryPointPattern().IsMatch(source)) &&
                !StringComparer.Ordinal.Equals(relative, containedStarterPath))
            {
                rawOwners.Add($"{relative}: CreateProcess");
            }
        }
        Assert.Empty(rawOwners);
    }

    [GeneratedRegex(
        @"\b(?:(?:global::)?System\.Diagnostics\.)?Process\s*\.\s*Start\s*\(",
        RegexOptions.CultureInvariant)]
    private static partial Regex RawProcessStartPattern();

    [GeneratedRegex(
        @"\bnew\s+(?:(?:global::)?System\.Diagnostics\.)?Process\s*(?:\(|\{)",
        RegexOptions.CultureInvariant)]
    private static partial Regex RawProcessConstructionPattern();

    [GeneratedRegex(@"\bCreateProcess(?:W|A)?\b", RegexOptions.CultureInvariant)]
    private static partial Regex RawCreateProcessPattern();

    [GeneratedRegex(
        "\\bEntryPoint\\s*=\\s*\"CreateProcess(?:W|A)?\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex RawCreateProcessEntryPointPattern();

    /// <summary>The public Launcher remains a thin host over the single Bootstrap graph.</summary>
    [Fact]
    public void DistributionLauncherUsesOneBootstrapOwnedCompositionGraph()
    {
        string solution = ReadText("NvtFwCombiner.slnx");
        string project = ReadText(
            "src/NvtFwCombiner.DistributionLauncher/NvtFwCombiner.DistributionLauncher.csproj");
        string program = ReadText("src/NvtFwCombiner.DistributionLauncher/Program.cs");
        string host = ReadText(
            "src/NvtFwCombiner.Bootstrap/ManagedDistributionLauncherHostServices.cs");
        string payloadSource = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/ManagedDistributionLauncherRuntime.cs");
        string handoff = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/StableLauncherHandoff.cs");

        AssertContainsAll(
            solution,
            "src/NvtFwCombiner.DistributionLauncher/NvtFwCombiner.DistributionLauncher.csproj");
        AssertContainsAll(
            project,
            "<OutputType>WinExe</OutputType>",
            "NvtFwCombiner.Bootstrap",
            "NvtFwCombiner.DistributionLauncher.Payload.managed-setup-payload-admission.v1.json",
            "NvtFwCombiner.DistributionLauncher.Payload.NvtFwCombiner.Bootstrap.exe");
        AssertDoesNotContainAny(
            project,
            "NvtFwCombiner.VersionManagement.Infrastructure\\",
            "NvtFwCombiner.Desktop",
            "PackageReference");
        AssertContainsAll(
            program,
            "ManagedDistributionLauncherHostServices.Create()",
            ".RunAsync(CancellationToken.None)",
            "DistributionLauncherExitCode");
        AssertDoesNotContainAny(
            program,
            "UpdateSourceRegistryLocator",
            "FileSystemUpdateCatalogSource",
            "FileSystemManagedVersionRepository",
            "JsonVersionManagerStateStore");
        AssertContainsAll(
            host,
            "Environment.ProcessPath",
            "Assembly.GetEntryAssembly()",
            "UpdateSourceRegistryLocator.ResolveAll(",
            "new JsonVersionManagerStateStore(",
            "new FileSystemManagedInstallationRootProbe()",
            "new EmbeddedManagedDistributionPayloadSource(",
            "new VersionManagementExperience(",
            "new FileSystemManagedFirstInstallationRootMaterializer(repository)",
            "new StableLauncherHandoff(",
            "ManagedLauncherEntryOutcome.SetupRequired ? SetupExperience : null");
        AssertDoesNotContainAny(
            host,
            "JsonDocument",
            "JsonSerializer",
            "SHA256",
            "MemoryStream",
            ".CopyTo(",
            "Task.Run",
            "CancellationTokenSource",
            "File.ReadAllBytes",
            "File.ReadAllText");
        Assert.Equal(
            1,
            CountOccurrences(payloadSource, "public EmbeddedManagedDistributionPayloadSource("));
        Assert.Contains(
            "internal EmbeddedManagedDistributionPayloadSource(",
            payloadSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "string launcher = Path.Combine(_managedRoot, \"NvtFwCombiner.Bootstrap.exe\")",
            handoff,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PathComparer.Equals(requestedRoot, _managedRoot)",
            handoff,
            StringComparison.Ordinal);
    }

    /// <summary>The immutable trust anchor is separate from every version-scoped launcher payload.</summary>
    [Fact]
    public void LauncherBootstrapUsesOneWayVersionManagementDependencies()
    {
        string solution = ReadText("NvtFwCombiner.slnx");
        string bootstrapProject = ReadText(
            "src/NvtFwCombiner.LauncherBootstrap/NvtFwCombiner.LauncherBootstrap.csproj");
        string bootstrapProgram = ReadText("src/NvtFwCombiner.LauncherBootstrap/Program.cs");
        string launcherProgram = ReadText("src/NvtFwCombiner.Launcher/Program.cs");
        string desktopProgram = ReadText("src/NvtFwCombiner.Desktop/Program.cs");
        string composition = ReadText("src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs");
        string contracts = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/LauncherBootstrapContracts.cs");
        string bootstrapRuntime = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/AnonymousPipeManagedLauncherProcess.cs") +
            ReadText(
                "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/BootstrapStartupProtocol.cs") +
            ReadText(
                "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/LauncherBootstrapRuntime.cs");

        AssertContainsAll(
            solution,
            "src/NvtFwCombiner.LauncherBootstrap/NvtFwCombiner.LauncherBootstrap.csproj");
        AssertContainsAll(
            bootstrapProject,
            "<AssemblyName>NvtFwCombiner.LauncherBootstrap</AssemblyName>",
            "NvtFwCombiner.VersionManagement.Infrastructure");
        AssertDoesNotContainAny(
            bootstrapProject,
            "NvtFwCombiner.Desktop",
            "NvtFwCombiner.Launcher\\NvtFwCombiner.Launcher.csproj",
            "PackageReference");
        AssertContainsAll(
            bootstrapProgram,
            "LauncherBootstrapLaunchOptions.Parse(args, AppContext.BaseDirectory)",
            "LauncherBootstrapRuntime.RunEntryAsync");
        AssertDoesNotContainAny(
            bootstrapProgram,
            "LauncherBootstrapStartupContext",
            "CaptureStartup",
            "WaitForStartAsync");
        AssertContainsAll(
            bootstrapRuntime,
            "using LauncherBootstrapStartupContext startup = CaptureStartup(statePath)",
            "startup.WaitForStartAsync(cancellationToken)",
            "RunCoreAsync(managedRoot, statePath, startup, cancellationToken)");
        Assert.True(
            bootstrapRuntime.IndexOf("CaptureStartup(statePath)", StringComparison.Ordinal) <
            bootstrapRuntime.IndexOf("startup.WaitForStartAsync(cancellationToken)", StringComparison.Ordinal));
        Assert.True(
            bootstrapRuntime.IndexOf("startup.WaitForStartAsync(cancellationToken)", StringComparison.Ordinal) <
            bootstrapRuntime.IndexOf("RunCoreAsync(managedRoot, statePath, startup, cancellationToken)", StringComparison.Ordinal));
        AssertContainsAll(
            bootstrapRuntime,
            "JsonVersionManagerStateStore.GetDefaultPath()",
            "--state-path",
            "ManagedProcessLifetimeKind.Bootstrap",
            "BootstrapStartGate.Capture()",
            "BootstrapAdmissionSignal.Capture()",
            "TryAcquireWriteLeaseAsync",
            "LauncherBootstrapCoordinator.StartupWriterLeaseTimeout");
        Assert.True(
            bootstrapRuntime.IndexOf("ManagedProcessLifetimeKind.Bootstrap", StringComparison.Ordinal) <
            bootstrapRuntime.IndexOf("BootstrapStartGate.Capture()", StringComparison.Ordinal));
        Assert.True(
            bootstrapRuntime.IndexOf("BootstrapStartGate.Capture()", StringComparison.Ordinal) <
            bootstrapRuntime.IndexOf("new ManagedVersionSeedBootstrapper(", StringComparison.Ordinal));
        AssertContainsAll(
            launcherProgram,
            "InheritedManagedProcessLifetime.Capture(",
            "ManagedProcessLifetimeKind.Launcher",
            "CaptureNestedReadyContext()",
            "InvalidInheritedContext",
            "AnonymousPipeManagedApplicationProcess(statePath)",
            "ReportNestedReadyAsync",
            "return 16");
        Assert.True(
            launcherProgram.IndexOf("CaptureNestedReadyContext()", StringComparison.Ordinal) <
            launcherProgram.IndexOf("new AnonymousPipeManagedApplicationProcess", StringComparison.Ordinal));
        Assert.DoesNotContain(
            "NVT_FW_COMBINER_EXPECTED_LAUNCHER_READY",
            launcherProgram,
            StringComparison.Ordinal);
        AssertContainsAll(
            bootstrapRuntime,
            "new ManagedVersionSeedBootstrapper(",
            "new LauncherBootstrapCoordinator(");
        Assert.True(
            bootstrapRuntime.IndexOf("new ManagedVersionSeedBootstrapper(", StringComparison.Ordinal) <
            bootstrapRuntime.IndexOf("new LauncherBootstrapCoordinator(", StringComparison.Ordinal));
        Assert.DoesNotContain("ManagedVersionSeedBootstrapper", launcherProgram, StringComparison.Ordinal);
        AssertContainsAll(
            desktopProgram,
            "CaptureInheritedManagedProcessLifetime(",
            "CaptureInheritedManagedBootstrapIdentity(lifetime.Outcome)",
            "ParseManagedHostOptions(args)",
            "--update-source-registry-path",
            "UpdateSourceRegistryLocator.ResolveAll(",
            "Environment.GetEnvironmentVariable",
            "CreateVersionManagementExperience");
        Assert.True(
            desktopProgram.IndexOf("UpdateSourceRegistryLocator.ResolveAll(", StringComparison.Ordinal) <
            desktopProgram.IndexOf("CreateVersionManagementExperience(", StringComparison.Ordinal));
        Assert.DoesNotContain("SetEnvironmentVariable", desktopProgram, StringComparison.Ordinal);
        Assert.True(
            desktopProgram.IndexOf("ParseManagedHostOptions(args)", StringComparison.Ordinal) <
            desktopProgram.IndexOf("CaptureInheritedManagedProcessLifetime(", StringComparison.Ordinal));
        Assert.True(
            desktopProgram.IndexOf("CaptureInheritedManagedProcessLifetime(", StringComparison.Ordinal) <
            desktopProgram.IndexOf("CaptureInheritedManagedBootstrapIdentity(lifetime.Outcome)", StringComparison.Ordinal));
        Assert.True(
            desktopProgram.IndexOf("CaptureInheritedManagedBootstrapIdentity(lifetime.Outcome)", StringComparison.Ordinal) <
            desktopProgram.IndexOf("CreateStableLauncherHandoff(", StringComparison.Ordinal));
        Assert.Contains("bootstrapIdentity", desktopProgram, StringComparison.Ordinal);
        AssertContainsAll(
            composition,
            "lifetimeOutcome == InheritedManagedProcessLifetimeOutcome.Captured",
            "InheritedManagedBootstrapIdentityContext.CaptureAndClear()",
            "ManagedImmutableBootstrapIdentity? expectedIdentity = null");
        AssertContainsAll(contracts, "launcher/NvtFwCombiner.Launcher.exe", "SupportedProtocolVersion = 1");
    }

    /// <summary>Durable generation equality stays with both Application state owners.</summary>
    [Fact]
    public void LauncherSelfTestConsumesOwnerGeneratedDurableSnapshotTokens()
    {
        string appState = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/VersionActivationPolicy.cs");
        string launcherState = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/LauncherBootstrapContracts.cs");
        string selfTest = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemLauncherInstallationSelfTest.cs");

        AssertContainsAll(appState, "CreateDurableSnapshotToken", "HasSameDurableSnapshot");
        AssertContainsAll(launcherState, "CreateDurableSnapshotToken", "HasSameDurableSnapshot");
        Assert.Equal(4, CountOccurrences(selfTest, "CreateDurableSnapshotToken()"));
        Assert.DoesNotContain("SameSnapshot", selfTest, StringComparison.Ordinal);
        Assert.DoesNotContain("TryAcquireWriteLease", selfTest, StringComparison.Ordinal);
    }

    /// <summary>Cleanup uncertainty and ordinary-attempt recovery remain one bounded fail-closed contract.</summary>
    [Fact]
    public void ManagedProcessCleanupAndLifetimeRecoveryStayFailClosed()
    {
        string process = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/AnonymousPipeManagedApplicationProcess.cs") +
            ReadText(
                "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/StableLauncherHandoff.cs");
        string lifetime = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/ManagedProcessLifetimeLease.cs");
        string appCoordinator = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/ManagedActivationCoordinator.cs");
        string launcherCoordinator = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/LauncherBootstrapCoordinator.ActiveAttemptRecovery.cs");
        string schema = ReadText("docs/contracts/launcher-bootstrap-v1.schema.json");

        AssertContainsAll(
            process,
            "AggregateException",
            "WaitForExit(process, _waitTimeout)",
            "ManagedProcessStartOutcome.TerminationUnconfirmed");
        Assert.DoesNotContain("TryObserveConfirmedExit", process, StringComparison.Ordinal);
        AssertContainsAll(
            lifetime,
            "SetHandleInformation",
            "AssignProcessToJobObject",
            "GetFinalPathNameByHandle",
            "StatePathEnvironment",
            "KindEnvironment",
            "TryReleaseAcceptedTree",
            "QueryInformationJobObject",
            "TerminateJobObject",
            "InvalidInheritedContext",
            "ManagedProcessLifetimeStatus.Active",
            "ManagedProcessLifetimeStatus.Unavailable");
        AssertContainsAll(appCoordinator, "ActiveLaunchRecorded", "GetLifetimeStatusAsync");
        AssertContainsAll(launcherCoordinator, "ActiveLaunchRecorded", "GetLifetimeStatusAsync");
        Assert.Contains("activeLaunchRecorded", schema, StringComparison.Ordinal);
    }

    /// <summary>Raw recovery, tree authority, and executable identity stay in their declared owners.</summary>
    [Fact]
    public void LauncherRecoveryAndStableExecutableLeaseRemainOwnedAndOrdered()
    {
        string coordinator = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/LauncherBootstrapCoordinator.cs");
        string activeRecovery = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/LauncherBootstrapCoordinator.ActiveAttemptRecovery.cs");
        string contracts = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/ManagedVersionRepository.cs");
        string stableLease = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/StableManagedExecutableLaunchLease.cs");
        string stableCustody = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/WindowsStablePathCustody.cs") +
            ReadText(
                "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/WindowsStablePathCustody.Native.cs");
        string appProcess = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/AnonymousPipeManagedApplicationProcess.cs");
        string launcherProcess = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/AnonymousPipeManagedLauncherProcess.cs") +
            ReadText(
                "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/BootstrapStartupProtocol.cs") +
            ReadText(
                "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/LauncherBootstrapRuntime.cs");
        string stableLeaseTest = ReadText(
            "tests/NvtFwCombiner.Infrastructure.Tests/VersionManagement/FileSystemManagedVersionRepositoryLaunchLeaseTests.cs");

        Assert.True(
            coordinator.IndexOf("LoadRawStatesAsync", StringComparison.Ordinal) <
            coordinator.IndexOf("RecoverActiveAttemptsAsync", StringComparison.Ordinal));
        Assert.True(
            coordinator.IndexOf("RecoverActiveAttemptsAsync", StringComparison.Ordinal) <
            coordinator.IndexOf("HasCrossJournalConflict", StringComparison.Ordinal));
        AssertContainsAll(activeRecovery, "ActiveLaunchRecorded", "AppMutationPending");
        AssertContainsAll(
            contracts,
            "IManagedExecutableLaunchLease",
            "AcquireApplicationLaunchLeaseAsync",
            "TryValidateForStart");
        AssertContainsAll(
            stableLease,
            "WindowsStablePathCustody.TryAcquireFile",
            "SHA256.HashDataAsync",
            "HasPortableExecutableHeader");
        string stableHandoff = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/StableLauncherHandoff.cs");
        string inheritedIdentity = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/InheritedManagedBootstrapIdentityContext.cs");
        AssertContainsAll(
            stableHandoff,
            "StableManagedExecutableLaunchLease.TryAcquireAsync(",
            "_expectedIdentity.Length",
            "_expectedIdentity.Sha256",
            "InheritedManagedBootstrapIdentityContext.Apply(startInfo, identity)");
        Assert.DoesNotContain("TryAcquireMeasuredAsync", stableHandoff, StringComparison.Ordinal);
        AssertContainsAll(
            inheritedIdentity,
            "MaximumSerializedCharacters = 128",
            "Environment.SetEnvironmentVariable",
            "ManagedImmutableBootstrapIdentity");
        AssertContainsAll(
            stableCustody,
            "allowWriteShare: false",
            "uint share = NativeMethods.ShareRead",
            "share |= NativeMethods.ShareWrite");
        AssertDoesNotContainAny(
            stableLease,
            "FileShare.Read",
            "FileShare.Write",
            "NativeMethods.ShareRead",
            "NativeMethods.ShareWrite");
        AssertContainsAll(
            stableLeaseTest,
            "AcquireApplicationLaunchLeaseAsync",
            "StartUntilReadyAsync",
            "Assert.Throws<IOException>(() => File.Move(executable, displaced))");
        AssertDoesNotContainAny(appProcess, "SHA256", "HasPortableExecutableHeader");
        AssertDoesNotContainAny(launcherProcess, "HasPortableExecutableHeader");
        Assert.Contains("TryValidateForStart", appProcess, StringComparison.Ordinal);
        Assert.Contains("TryValidateForStart", launcherProcess, StringComparison.Ordinal);
    }

    /// <summary>Launcher activation shares the existing exact app-state lease and declares no second writer.</summary>
    [Fact]
    public void LauncherBootstrapDeclaresNoIndependentLeaseAuthority()
    {
        string application = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/LauncherBootstrapCoordinator.cs");
        string store = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/JsonLauncherBootstrapStateStore.cs");
        string process = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/AnonymousPipeManagedLauncherProcess.cs") +
            ReadText(
                "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/BootstrapStartupProtocol.cs") +
            ReadText(
                "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/LauncherBootstrapRuntime.cs");

        AssertContainsAll(
            application,
            "_appStateStore.TryAcquireWriteLeaseAsync",
            "LoadConsistentStatesAsync",
            "HasCrossJournalConflict",
            "TrySaveLauncherStateAsync");
        Assert.Equal(1, CountOccurrences(application, "await LoadAppStateAsync("));
        Assert.Equal(1, CountOccurrences(application, "await LoadLauncherStateAsync("));
        Assert.DoesNotContain("SaveCoreAsync", application, StringComparison.Ordinal);
        Assert.DoesNotContain("TryAcquireWriteLease", store, StringComparison.Ordinal);
        Assert.DoesNotContain("Mutex", store, StringComparison.Ordinal);
        AssertContainsAll(process, "LauncherReadyProtocol.TryParse", "readyAdmission");
    }

    /// <summary>Every production version-management graph receives the single JSON launcher fence explicitly.</summary>
    [Fact]
    public void VersionManagementProductionCompositionRequiresOneLauncherFence()
    {
        string experience = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/VersionManagementExperience.cs");
        string fence = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/LauncherMutationFence.cs");
        string composition = ReadText("src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs");

        AssertContainsAll(experience, "ILauncherMutationFence launcherFence", "throw new ArgumentNullException(nameof(launcherFence))");
        Assert.DoesNotContain("ILauncherMutationFence?", experience, StringComparison.Ordinal);
        Assert.DoesNotContain("NoLauncherMutationFence", fence, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(composition, "new JsonLauncherMutationFence("));
    }

    /// <summary>The outer release contract pins exactly one version-scoped launcher identity.</summary>
    [Fact]
    public void ReleaseManifestPinsVersionScopedLauncherContract()
    {
        string schema = ReadText("docs/contracts/release-manifest-v1.schema.json");

        AssertContainsAll(
            schema,
            "\"schemaVersion\": { \"enum\": [\"1.1\", \"1.2\"] }",
            "\"versionManagementProtocolVersion\": { \"const\": 1 }",
            "\"path\": { \"const\": \"launcher/NvtFwCombiner.Launcher.exe\" }",
            "\"maxContains\": 1");
    }

    /// <summary>Every Launcher/Bootstrap schema projects the one canonical 200 MB ceiling.</summary>
    [Fact]
    public void LauncherAndBootstrapSchemasShareCanonicalExecutableCeiling()
    {
        const long expected = 200_000_000;
        string identity = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/ManagedLauncherEntry.cs");
        Assert.Contains(
            "public const long MaximumExecutableBytes = 200_000_000;",
            identity,
            StringComparison.Ordinal);
        Assert.Equal(expected, SchemaMaximum(
            "docs/contracts/managed-setup-payload-admission-v1.schema.json",
            "properties", "bootstrap", "properties", "size", "maximum"));
        Assert.Equal(expected, SchemaMaximum(
            "docs/contracts/installer-release-manifest-v1.schema.json",
            "properties", "embeddedBootstrap", "properties", "size", "maximum"));
        Assert.Equal(expected, SchemaMaximum(
            "docs/contracts/launcher-bootstrap-v1.schema.json",
            "$defs", "identity", "properties", "size", "maximum"));
        Assert.Equal(expected, SchemaMaximum(
            "docs/contracts/release-manifest-v1.schema.json",
            "$defs", "launcher", "properties", "size", "maximum"));
    }

    private static long SchemaMaximum(string path, params string[] segments)
    {
        using JsonDocument schema = JsonDocument.Parse(ReadText(path));
        JsonElement value = schema.RootElement;
        foreach (string segment in segments)
        {
            value = value.GetProperty(segment);
        }
        return value.GetInt64();
    }
}

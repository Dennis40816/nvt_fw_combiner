namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
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
        string contracts = ReadText(
            "src/NvtFwCombiner.VersionManagement.Application/VersionManagement/LauncherBootstrapContracts.cs");
        string bootstrapRuntime = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/AnonymousPipeManagedLauncherProcess.cs");

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
            "LauncherBootstrapRuntime.RunAsync");
        AssertContainsAll(
            bootstrapRuntime,
            "JsonVersionManagerStateStore.GetDefaultPath()",
            "--state-path",
            "TryAcquireWriteLeaseAsync",
            "ManagedActivationCoordinator.DefaultWriterLeaseTimeout");
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
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/AnonymousPipeManagedApplicationProcess.cs");
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
        string appProcess = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/AnonymousPipeManagedApplicationProcess.cs");
        string launcherProcess = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/AnonymousPipeManagedLauncherProcess.cs");
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
            "AcquireApplicationLaunchLeaseAsync");
        AssertContainsAll(stableLease, "FileShare.Read", "SHA256.HashDataAsync", "HasPortableExecutableHeader");
        AssertContainsAll(
            stableLeaseTest,
            "AcquireApplicationLaunchLeaseAsync",
            "StartUntilReadyAsync",
            "Assert.Throws<IOException>(() => File.Move(executable, displaced))");
        AssertDoesNotContainAny(appProcess, "SHA256", "HasPortableExecutableHeader");
        AssertDoesNotContainAny(launcherProcess, "HasPortableExecutableHeader");
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
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/AnonymousPipeManagedLauncherProcess.cs");

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
}

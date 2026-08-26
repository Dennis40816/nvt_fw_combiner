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
        AssertContainsAll(desktopProgram, "ParseManagedHostOptions(args)", "CreateVersionManagementExperience");
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

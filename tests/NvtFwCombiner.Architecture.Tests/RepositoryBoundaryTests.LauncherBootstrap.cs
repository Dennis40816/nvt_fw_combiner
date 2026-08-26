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
            "<AssemblyName>NvtFwCombiner.Bootstrap</AssemblyName>",
            "NvtFwCombiner.VersionManagement.Infrastructure");
        AssertDoesNotContainAny(
            bootstrapProject,
            "NvtFwCombiner.Desktop",
            "NvtFwCombiner.Launcher\\NvtFwCombiner.Launcher.csproj",
            "PackageReference");
        AssertContainsAll(bootstrapProgram, "LauncherBootstrapRuntime.RunAsync", "--state-path");
        AssertContainsAll(launcherProgram, "AnonymousPipeManagedApplicationProcess(statePath)", "ReportNestedReadyAsync");
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

        AssertContainsAll(application, "_appStateStore.TryAcquireWriteLeaseAsync", "PendingMutation");
        Assert.DoesNotContain("TryAcquireWriteLease", store, StringComparison.Ordinal);
        Assert.DoesNotContain("Mutex", store, StringComparison.Ordinal);
        AssertContainsAll(process, "LauncherReadyProtocol.TryParse", "readyAdmission");
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

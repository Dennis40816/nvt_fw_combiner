using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.Platform.Processes;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class AnonymousPipeManagedLauncherProcessTests
{
    private const string BootstrapIdentityEnvironment =
        "NVT_FW_COMBINER_ROOT_BOOTSTRAP_IDENTITY";
    private const string IdentityMarkerEnvironment = "NVT_READY_PROBE_IDENTITY_MARKER";

    /// <summary>A managed Launcher strips ambient Bootstrap identity that was not explicitly admitted.</summary>
    [Fact]
    public async Task ManagedLauncherDoesNotLaunderAmbientBootstrapIdentity()
    {
        using var workspace = TempWorkspace.Create();
        ManagedLauncherIdentity launcher = PrepareProbe(workspace.Root);
        string forged = $"1|NvtFwCombiner.Bootstrap.exe|12345|{new string('a', 64)}";

        string[] observation = await ObserveLauncherIdentityAsync(
            workspace,
            launcher,
            inheritedIdentity: null,
            ambientIdentity: forged);

        Assert.Equal("Captured", observation[0]);
        Assert.Equal("<null>", observation[1]);
        Assert.Equal("<null>", observation[2]);
        Assert.Equal("<null>", observation[3]);
    }

    /// <summary>A real managed child clears malformed identity without losing its managed lifetime.</summary>
    [Fact]
    public async Task ManagedLauncherClearsMalformedIdentityAndStillReachesReady()
    {
        using var workspace = TempWorkspace.Create();
        ManagedLauncherIdentity launcher = PrepareProbe(workspace.Root);

        string[] observation = await ObserveLauncherIdentityAsync(
            workspace,
            launcher,
            inheritedIdentity: null,
            ambientIdentity: "1|malformed");

        Assert.Equal("Captured", observation[0]);
        Assert.Equal("<null>", observation[1]);
        Assert.Equal("<null>", observation[2]);
        Assert.Equal("<null>", observation[3]);
    }

    /// <summary>A genuine inherited Bootstrap identity is the only identity propagated to Launcher.</summary>
    [Fact]
    public async Task ManagedLauncherReceivesOnlyExplicitBootstrapIdentity()
    {
        using var workspace = TempWorkspace.Create();
        ManagedLauncherIdentity launcher = PrepareProbe(workspace.Root);
        var identity = new ManagedImmutableBootstrapIdentity(
            "NvtFwCombiner.Bootstrap.exe",
            67890,
            new string('b', 64));

        string[] observation = await ObserveLauncherIdentityAsync(
            workspace,
            launcher,
            identity,
            ambientIdentity: $"1|NvtFwCombiner.Bootstrap.exe|12345|{new string('a', 64)}");

        Assert.Equal("Captured", observation[0]);
        Assert.Contains("|67890|", observation[1], StringComparison.Ordinal);
        Assert.Equal("<null>", observation[2]);
        Assert.Equal(identity.FileName, observation[3]);
        Assert.Equal(identity.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), observation[4]);
        Assert.Equal(identity.Sha256, observation[5]);
    }

    /// <summary>Legacy direct Bootstrap clears forged ambient identity before any managed child starts.</summary>
    [Fact]
    public async Task LegacyBootstrapClearsAmbientIdentityBeforeManagedStateAccess()
    {
        using var workspace = TempWorkspace.Create();
        string? prior = Environment.GetEnvironmentVariable(BootstrapIdentityEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(
                BootstrapIdentityEnvironment,
                $"1|NvtFwCombiner.Bootstrap.exe|12345|{new string('a', 64)}");

            _ = await LauncherBootstrapRuntime.RunEntryAsync(
                workspace.Root,
                workspace.PathFor("state.json"),
                TestContext.Current.CancellationToken);

            Assert.Null(Environment.GetEnvironmentVariable(BootstrapIdentityEnvironment));
        }
        finally
        {
            Environment.SetEnvironmentVariable(BootstrapIdentityEnvironment, prior);
        }
    }

    /// <summary>Complete managed startup does not require optional restart authority.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("1|malformed")]
    public async Task RealBootstrapAcceptsCompleteManagedContextWithoutValidIdentity(
        string? inheritedIdentity)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string managedRoot = workspace.PathFor("managed");
        string sourceRoot = workspace.PathFor("source");
        _ = Directory.CreateDirectory(managedRoot);
        UpdateCatalogVersionSnapshot package = FileSystemManagedVersionRepositoryTests
            .CreatePackageForManagedSetup(
                sourceRoot,
                "1.0.4",
                includeManagedLauncher: true,
                useStandaloneLauncherProbe: true);
        ManagedVersionInstallResult installed = await new FileSystemManagedVersionRepository()
            .InstallAsync(
                managedRoot,
                sourceRoot,
                package,
                TestContext.Current.CancellationToken);
        Assert.True(installed.IsSuccess, installed.Issue.ToString());
        ManagedVersionAdmission admission = Assert.IsType<ManagedVersionAdmission>(installed.Admission);
        string launcherPath = Path.Combine(
            managedRoot,
            "versions",
            package.Version.ToString(),
            ManagedLauncherIdentity.ExecutablePath.Replace('/', Path.DirectorySeparatorChar));
        byte[] launcherBytes = await File.ReadAllBytesAsync(
            launcherPath,
            TestContext.Current.CancellationToken);
        ManagedLauncherIdentity launcher = ManagedLauncherIdentity.Create(
            package.Version,
            admission.AdmissionIdentity,
            admission.ReleaseManifestSha256,
            package.Version,
            ManagedLauncherIdentity.SupportedProtocolVersion,
            ManagedLauncherIdentity.ExecutablePath,
            launcherBytes.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(launcherBytes)));
        string statePath = workspace.PathFor("state/version-manager.v1.json");
        var stateStore = new JsonVersionManagerStateStore(statePath);
        await stateStore.SaveAsync(
            VersionManagerState.Create(
                sourceRoot,
                package.Version,
                package.Version,
                [admission],
                pendingActivation: null,
                failedActivationVersion: null,
                retentionReviewDue: false,
                managedRootIdentity: managedRoot),
            TestContext.Current.CancellationToken);
        LauncherBootstrapStateSaveResult launcherSaved = await new JsonLauncherBootstrapStateStore(
                statePath)
            .TrySaveAsync(
                LauncherBootstrapState.Create(
                    managedRoot,
                    launcher,
                    launcher,
                    pending: null,
                    failed: null),
                TestContext.Current.CancellationToken);
        Assert.True(launcherSaved.IsSuccess);

        using ManagedProcessLifetimeLease lifetime = ManagedProcessLifetimeLease.TryAcquire(
            statePath,
            ManagedProcessLifetimeKind.Bootstrap) ??
            throw new InvalidOperationException("Bootstrap lifetime could not be acquired.");
        using var gate = new BootstrapStartAuthorization();
        using var admissionPipe = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.None);
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(
                AppContext.BaseDirectory,
                "launcher-bootstrap",
                "NvtFwCombiner.LauncherBootstrap.exe"),
            WorkingDirectory = managedRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--managed-root");
        startInfo.ArgumentList.Add(managedRoot);
        startInfo.ArgumentList.Add("--state-path");
        startInfo.ArgumentList.Add(statePath);
        lifetime.ApplyInheritedContext(startInfo);
        gate.ApplyInheritedContext(startInfo);
        startInfo.Environment[
            AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment] =
            admissionPipe.GetClientHandleAsString();
        startInfo.Environment[BootstrapIdentityEnvironment] = inheritedIdentity;

        using Process process = ProcessLaunchGate.StartContained(
            startInfo,
            [
                ProcessInheritedHandle.Parse(
                    AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment,
                    admissionPipe.GetClientHandleAsString()),
                gate.InheritedHandle,
                new ProcessInheritedHandle(
                    ManagedProcessLifetimeLease.HandleEnvironment,
                    lifetime.InheritedHandleValue),
            ]) ??
            throw new InvalidOperationException("Root Bootstrap process did not start.");
        gate.DisposeLocalClientHandle();
        admissionPipe.DisposeLocalCopyOfClientHandle();
        Assert.True(gate.TryAuthorize());
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var reader = new StreamReader(admissionPipe);
        Assert.Equal("ADMITTED", await reader.ReadLineAsync(timeout.Token));
        await process.WaitForExitAsync(timeout.Token);
        Assert.Equal(15, process.ExitCode);
    }

    private static async Task<string[]> ObserveLauncherIdentityAsync(
        TempWorkspace workspace,
        ManagedLauncherIdentity launcher,
        ManagedImmutableBootstrapIdentity? inheritedIdentity,
        string ambientIdentity)
    {
        string marker = workspace.PathFor($"identity-{Guid.NewGuid():N}.txt");
        string? priorBehavior = Environment.GetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR");
        string? priorMarker = Environment.GetEnvironmentVariable(IdentityMarkerEnvironment);
        string? priorVersion = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION");
        string? priorAdmission = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION");
        string? priorManifest = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST");
        string? priorIdentity = Environment.GetEnvironmentVariable(BootstrapIdentityEnvironment);
        try
        {
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", "launcher-identity-observation");
            Environment.SetEnvironmentVariable(IdentityMarkerEnvironment, marker);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION", launcher.OwnerAppVersion.ToString());
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION", launcher.OwnerAdmissionIdentity);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST", launcher.OwnerReleaseManifestSha256);
            Environment.SetEnvironmentVariable(BootstrapIdentityEnvironment, ambientIdentity);
            using BootstrapAdmissionSignal admission = BootstrapAdmissionSignal.Capture();
            using TestExecutableLaunchLease executable = ExecutableLease(workspace.Root, launcher);
            var process = new AnonymousPipeManagedLauncherProcess(
                ManagedProcessTermination.Instance,
                admission,
                inheritedBootstrapIdentity: inheritedIdentity);

            LauncherProcessStartResult result = await process.StartUntilReadyAsync(
                workspace.Root,
                workspace.PathFor("state.json"),
                launcher,
                executable,
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            Assert.Equal(LauncherProcessStartOutcome.Ready, result.Outcome);
            return await File.ReadAllLinesAsync(marker, TestContext.Current.CancellationToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", priorBehavior);
            Environment.SetEnvironmentVariable(IdentityMarkerEnvironment, priorMarker);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION", priorVersion);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION", priorAdmission);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST", priorManifest);
            Environment.SetEnvironmentVariable(BootstrapIdentityEnvironment, priorIdentity);
        }
    }
}

using System.IO.Pipes;
using System.Diagnostics;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Runs exact managed launcher identities through the outer READY channel.</summary>
[Collection(nameof(ReadyProbeProcessSerialGroup))]
public sealed class AnonymousPipeManagedLauncherProcessTests
{
    /// <summary>The exact identity and custom state path reach the candidate launcher.</summary>
    [Fact]
    public async Task ExactReadyCarriesCustomStatePathToCandidate()
    {
        using var workspace = TempWorkspace.Create();
        ManagedLauncherIdentity identity = PrepareProbe(workspace.Root);
        string statePath = Path.Combine(workspace.Root, "state", "custom state.json");
        string argumentsPath = Path.Combine(workspace.Root, "arguments.txt");

        LauncherProcessStartResult result = await RunAsync(
            workspace.Root,
            statePath,
            identity,
            "ready",
            argumentsPath,
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        Assert.Equal(LauncherProcessStartOutcome.Ready, result.Outcome);
        Assert.Equal(
            ["--managed-root", Path.GetFullPath(workspace.Root), "--state-path", Path.GetFullPath(statePath)],
            await File.ReadAllLinesAsync(argumentsPath, TestContext.Current.CancellationToken));
    }

    /// <summary>A launcher that does not echo the exact identity is rejected.</summary>
    [Fact]
    public async Task InvalidOuterReadyIsRejected()
    {
        using var workspace = TempWorkspace.Create();
        ManagedLauncherIdentity identity = PrepareProbe(workspace.Root);

        LauncherProcessStartResult result = await RunAsync(
            workspace.Root,
            Path.Combine(workspace.Root, "state.json"),
            identity,
            "invalid",
            argumentsPath: null,
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Assert.Equal(LauncherProcessStartOutcome.InvalidReadySignal, result.Outcome);
    }

    /// <summary>A silent launcher is killed at the bounded outer READY deadline.</summary>
    [Fact]
    public async Task OuterReadyTimeoutIsBounded()
    {
        using var workspace = TempWorkspace.Create();
        ManagedLauncherIdentity identity = PrepareProbe(workspace.Root);

        LauncherProcessStartResult result = await RunAsync(
            workspace.Root,
            Path.Combine(workspace.Root, "state.json"),
            identity,
            "timeout",
            argumentsPath: null,
            TimeSpan.FromMilliseconds(200),
            TestContext.Current.CancellationToken);

        Assert.Equal(LauncherProcessStartOutcome.ReadyTimeout, result.Outcome);
    }

    /// <summary>A wait failure after kill cannot authorize launcher rollback in the same invocation.</summary>
    [Fact]
    public async Task WaitFailureReturnsUnconfirmedTermination()
    {
        using var workspace = TempWorkspace.Create();
        ManagedLauncherIdentity identity = PrepareProbe(workspace.Root);
        string? previous = Environment.GetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR");
        try
        {
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", "timeout");
            var termination = new ManagedProcessTermination(
                new FailingTerminationOperations());
            using TestExecutableLaunchLease executableLease = ExecutableLease(workspace.Root, identity);

            LauncherProcessStartResult result = await new AnonymousPipeManagedLauncherProcess(termination)
                .StartUntilReadyAsync(
                    workspace.Root,
                    Path.Combine(workspace.Root, "state.json"),
                    identity,
                    executableLease,
                    TimeSpan.FromMilliseconds(200),
                    TestContext.Current.CancellationToken);

            Assert.Equal(LauncherProcessStartOutcome.TerminationUnconfirmed, result.Outcome);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", previous);
        }
    }

    /// <summary>A nested cleanup-uncertain Launcher exit remains typed and cannot become ordinary rollback.</summary>
    [Fact]
    public async Task NestedUnconfirmedTerminationExitRemainsTyped()
    {
        using var workspace = TempWorkspace.Create();
        ManagedLauncherIdentity identity = PrepareProbe(workspace.Root);

        LauncherProcessStartResult result = await RunAsync(
            workspace.Root,
            Path.Combine(workspace.Root, "state.json"),
            identity,
            "termination-unconfirmed",
            argumentsPath: null,
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Assert.Equal(LauncherProcessStartOutcome.TerminationUnconfirmed, result.Outcome);
        Assert.Equal(LauncherBootstrapRuntime.UnconfirmedTerminationExitCode, result.ExitCode);
    }

    /// <summary>Outer READY inheritance distinguishes unmanaged, partial, blank, and malformed contexts.</summary>
    [Theory]
    [InlineData(null, null, LauncherReadyInheritanceOutcome.NotInherited)]
    [InlineData("123", null, LauncherReadyInheritanceOutcome.InvalidInheritedContext)]
    [InlineData(null, "expected", LauncherReadyInheritanceOutcome.InvalidInheritedContext)]
    [InlineData("", "expected", LauncherReadyInheritanceOutcome.InvalidInheritedContext)]
    [InlineData("123", "", LauncherReadyInheritanceOutcome.InvalidInheritedContext)]
    [InlineData("not-a-handle", "expected", LauncherReadyInheritanceOutcome.InvalidInheritedContext)]
    [InlineData("123", "malformed", LauncherReadyInheritanceOutcome.InvalidInheritedContext)]
    public void OuterReadyInheritanceRejectsPartialBlankAndMalformedValues(
        string? handle,
        string? expected,
        LauncherReadyInheritanceOutcome outcome)
    {
        WithOuterReadyEnvironment(handle, expected, () =>
        {
            LauncherReadyInheritance context = LauncherBootstrapRuntime.CaptureNestedReadyContext();

            Assert.Equal(outcome, context.Outcome);
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment));
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment));
        });
    }

    /// <summary>A valid expected-only value is partial, while both valid values are inherited.</summary>
    [Fact]
    public void OuterReadyInheritanceRequiresBothValidValues()
    {
        using var workspace = TempWorkspace.Create();
        using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        ManagedLauncherIdentity identity = PrepareProbe(workspace.Root);
        string expected = LauncherReadyProtocol.CreateExpectedPrefix(identity);
        WithOuterReadyEnvironment(
            handle: null,
            expected,
            () => Assert.Equal(
                LauncherReadyInheritanceOutcome.InvalidInheritedContext,
                LauncherBootstrapRuntime.CaptureNestedReadyContext().Outcome));
        WithOuterReadyEnvironment(
            pipe.GetClientHandleAsString(),
            expected,
            () => Assert.Equal(
                LauncherReadyInheritanceOutcome.Inherited,
                LauncherBootstrapRuntime.CaptureNestedReadyContext().Outcome));
    }

    /// <summary>Invalid PE bytes fail as a typed start outcome so Application can select exact rollback.</summary>
    [Fact]
    public async Task InvalidPeLauncherIsTypedStartFailure()
    {
        using var workspace = TempWorkspace.Create();
        ManagedAppVersion owner = ManagedAppVersion.Parse("0.10.6");
        string launcherRoot = Path.Combine(workspace.Root, "versions", owner.ToString(), "launcher");
        _ = Directory.CreateDirectory(launcherRoot);
        string executable = Path.Combine(launcherRoot, "NvtFwCombiner.Launcher.exe");
        byte[] bytes = "MZ-invalid-managed-launcher"u8.ToArray();
        await File.WriteAllBytesAsync(executable, bytes, TestContext.Current.CancellationToken);
        ManagedLauncherIdentity identity = ManagedLauncherIdentity.Create(
            owner,
            "catalog-identity-0.10.6",
            new string('a', 64),
            ManagedAppVersion.Parse("1.0.0"),
            ManagedLauncherIdentity.SupportedProtocolVersion,
            ManagedLauncherIdentity.ExecutablePath,
            bytes.LongLength,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
        using TestExecutableLaunchLease executableLease = ExecutableLease(workspace.Root, identity);

        LauncherProcessStartResult result = await new AnonymousPipeManagedLauncherProcess().StartUntilReadyAsync(
            workspace.Root,
            Path.Combine(workspace.Root, "state.json"),
            identity,
            executableLease,
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(LauncherProcessStartOutcome.StartFailed, result.Outcome);
        Assert.Null(result.ExitCode);
    }

    /// <summary>A competing exact-path writer prevents nested READY from observing two journal generations.</summary>
    [Fact]
    public async Task CompetingWriterPreventsMixedNestedReadySnapshot()
    {
        using var workspace = TempWorkspace.Create();
        string statePath = Path.Combine(workspace.Root, "state", "version-manager.v1.json");
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");
        var admission = new ManagedVersionAdmission(version, "catalog-identity-0.10.6", new string('a', 64));
        var identity = ManagedLauncherIdentity.Create(
            version,
            admission.AdmissionIdentity,
            admission.ReleaseManifestSha256,
            ManagedAppVersion.Parse("1.0.0"),
            ManagedLauncherIdentity.SupportedProtocolVersion,
            ManagedLauncherIdentity.ExecutablePath,
            123,
            new string('b', 64));
        var appStore = new JsonVersionManagerStateStore(statePath);
        await appStore.SaveAsync(
            VersionManagerState.Create(
                updateSource: null,
                activeVersion: version,
                lastKnownGoodVersion: version,
                admissions: [admission],
                pendingActivation: null,
                failedActivationVersion: null,
                retentionReviewDue: false,
                managedRootIdentity: workspace.Root),
            TestContext.Current.CancellationToken);
        LauncherBootstrapStateSaveResult launcherSaved = await new JsonLauncherBootstrapStateStore(statePath)
            .TrySaveAsync(
                LauncherBootstrapState.Create(workspace.Root, identity, identity, pending: null, failed: null),
                TestContext.Current.CancellationToken);
        Assert.True(launcherSaved.IsSuccess);

        using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        string? previousHandle = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment);
        string? previousExpected = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment,
                pipe.GetClientHandleAsString());
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment,
                LauncherReadyProtocol.CreateExpectedPrefix(identity));
            using VersionManagerWriteLeaseResult competing = await appStore.TryAcquireWriteLeaseAsync(
                TimeSpan.Zero,
                TestContext.Current.CancellationToken);
            Assert.True(competing.IsAcquired);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            LauncherReadyInheritance context = LauncherBootstrapRuntime.CaptureNestedReadyContext();
            bool reported = await LauncherBootstrapRuntime.ReportNestedReadyAsync(
                context,
                workspace.Root,
                statePath,
                cancellation.Token);
            pipe.DisposeLocalCopyOfClientHandle();
            using var reader = new StreamReader(pipe);
            string? message = await reader.ReadLineAsync(TestContext.Current.CancellationToken);

            Assert.False(reported);
            Assert.Null(message);
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment));
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment,
                previousHandle);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment,
                previousExpected);
        }
    }

    private static async ValueTask<LauncherProcessStartResult> RunAsync(
        string managedRoot,
        string statePath,
        ManagedLauncherIdentity identity,
        string behavior,
        string? argumentsPath,
        TimeSpan deadline,
        CancellationToken cancellationToken)
    {
        string? previousBehavior = Environment.GetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR");
        string? previousArguments = Environment.GetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH");
        string? previousVersion = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION");
        string? previousAdmission = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION");
        string? previousManifest = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST");
        try
        {
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", behavior);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH", argumentsPath);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION", identity.OwnerAppVersion.ToString());
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION", identity.OwnerAdmissionIdentity);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST", identity.OwnerReleaseManifestSha256);
            using TestExecutableLaunchLease executableLease = ExecutableLease(managedRoot, identity);
            return await new AnonymousPipeManagedLauncherProcess().StartUntilReadyAsync(
                managedRoot,
                statePath,
                identity,
                executableLease,
                deadline,
                cancellationToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", previousBehavior);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH", previousArguments);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION", previousVersion);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION", previousAdmission);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST", previousManifest);
        }
    }

    private static TestExecutableLaunchLease ExecutableLease(
        string managedRoot,
        ManagedLauncherIdentity identity)
    {
        string workingDirectory = Path.Combine(
            managedRoot,
            "versions",
            identity.OwnerAppVersion.ToString(),
            "launcher");
        return new TestExecutableLaunchLease(
            Path.Combine(workingDirectory, "NvtFwCombiner.Launcher.exe"),
            workingDirectory);
    }

    private sealed record TestExecutableLaunchLease(
        string ExecutablePath,
        string WorkingDirectory) : IManagedExecutableLaunchLease
    {
        public void Dispose() { }
    }

    private static void WithOuterReadyEnvironment(string? handle, string? expected, Action action)
    {
        string? previousHandle = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment);
        string? previousExpected = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment,
                handle);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment,
                expected);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment,
                previousHandle);
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment,
                previousExpected);
        }
    }

    private sealed class FailingTerminationOperations : IManagedProcessTerminationOperations
    {
        public bool HasExited(Process process)
        {
            return false;
        }

        public void Kill(Process process)
        {
            process.Kill(entireProcessTree: true);
        }

        public bool WaitForExit(Process process, TimeSpan timeout)
        {
            throw new InvalidOperationException("Injected wait failure.");
        }

        public int GetExitCode(Process process)
        {
            return process.ExitCode;
        }
    }

    private static ManagedLauncherIdentity PrepareProbe(string managedRoot)
    {
        ManagedAppVersion owner = ManagedAppVersion.Parse("0.10.6");
        string launcherRoot = Path.Combine(managedRoot, "versions", owner.ToString(), "launcher");
        _ = Directory.CreateDirectory(launcherRoot);
        string probeRoot = Path.Combine(AppContext.BaseDirectory, "ready-probe");
        foreach (string source in Directory.EnumerateFiles(probeRoot))
        {
            string fileName = Path.GetFileName(source);
            string targetName = string.Equals(fileName, "NvtFwCombiner.ReadyProbe.exe", StringComparison.OrdinalIgnoreCase)
                ? "NvtFwCombiner.Launcher.exe"
                : fileName;
            File.Copy(source, Path.Combine(launcherRoot, targetName));
        }
        string executable = Path.Combine(launcherRoot, "NvtFwCombiner.Launcher.exe");
        byte[] bytes = File.ReadAllBytes(executable);
        return ManagedLauncherIdentity.Create(
            owner,
            "catalog-identity-0.10.6",
            new string('a', 64),
            ManagedAppVersion.Parse("1.0.0"),
            ManagedLauncherIdentity.SupportedProtocolVersion,
            ManagedLauncherIdentity.ExecutablePath,
            bytes.LongLength,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
    }
}

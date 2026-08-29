using System.IO.Pipes;
using System.Diagnostics;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Runs exact managed launcher identities through the outer READY channel.</summary>
[Collection(nameof(ReadyProbeProcessSerialGroup))]
public sealed partial class AnonymousPipeManagedLauncherProcessTests
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

    /// <summary>Root Bootstrap emits one ADMITTED line only after the version Launcher starts.</summary>
    [Fact]
    public async Task StartedVersionLauncherEmitsOneUseBootstrapAdmission()
    {
        using var workspace = TempWorkspace.Create();
        ManagedLauncherIdentity identity = PrepareProbe(workspace.Root);
        using var admissionPipe = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        string inheritedDuplicate = DuplicateInheritableClientHandle(admissionPipe);
        string? previous = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment);
        try
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment,
                inheritedDuplicate);
            admissionPipe.DisposeLocalCopyOfClientHandle();
            Task<LauncherProcessStartResult> running = RunAsync(
                workspace.Root,
                Path.Combine(workspace.Root, "state.json"),
                identity,
                "ready",
                argumentsPath: null,
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken).AsTask();
            using var reader = new StreamReader(admissionPipe);

            string? admitted = await reader.ReadLineAsync(TestContext.Current.CancellationToken);
            string? second = await reader.ReadLineAsync(TestContext.Current.CancellationToken);
            LauncherProcessStartResult result = await running;

            Assert.Equal("ADMITTED", admitted);
            Assert.Null(second);
            Assert.Equal(LauncherProcessStartOutcome.Ready, result.Outcome);
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment,
                previous);
        }
    }

    /// <summary>A failed candidate start preserves the one-use admission for a successful LKG start.</summary>
    [Fact]
    public async Task CandidateStartFailureThenLkgReadyStillEmitsExactlyOneAdmission()
    {
        using var workspace = TempWorkspace.Create();
        ManagedLauncherIdentity lkg = PrepareProbe(workspace.Root);
        ManagedAppVersion candidateVersion = ManagedAppVersion.Parse("1.0.4");
        string candidateRoot = Path.Combine(
            workspace.Root,
            "versions",
            candidateVersion.ToString(),
            "launcher");
        _ = Directory.CreateDirectory(candidateRoot);
        string invalidExecutable = Path.Combine(candidateRoot, "NvtFwCombiner.Launcher.exe");
        byte[] invalidBytes = "MZ-invalid-candidate"u8.ToArray();
        await File.WriteAllBytesAsync(
            invalidExecutable,
            invalidBytes,
            TestContext.Current.CancellationToken);
        ManagedLauncherIdentity candidate = ManagedLauncherIdentity.Create(
            candidateVersion,
            "candidate-admission-1.0.4",
            new string('c', 64),
            ManagedAppVersion.Parse("1.0.0"),
            ManagedLauncherIdentity.SupportedProtocolVersion,
            ManagedLauncherIdentity.ExecutablePath,
            invalidBytes.LongLength,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(invalidBytes)));
        string statePath = Path.Combine(workspace.Root, "state.json");
        using var admissionPipe = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        string inheritedDuplicate = DuplicateInheritableClientHandle(admissionPipe);
        string? previousAdmission = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment);
        string? previousBehavior = Environment.GetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR");
        string? previousVersion = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION");
        string? previousIdentity = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION");
        string? previousManifest = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST");
        try
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment,
                inheritedDuplicate);
            var process = new AnonymousPipeManagedLauncherProcess();
            admissionPipe.DisposeLocalCopyOfClientHandle();
            using var candidateLease = new TestExecutableLaunchLease(
                invalidExecutable,
                candidateRoot);

            LauncherProcessStartResult candidateResult = await process.StartUntilReadyAsync(
                workspace.Root,
                statePath,
                candidate,
                candidateLease,
                TimeSpan.FromSeconds(1),
                TestContext.Current.CancellationToken);

            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", "ready");
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION", lkg.OwnerAppVersion.ToString());
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION", lkg.OwnerAdmissionIdentity);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST", lkg.OwnerReleaseManifestSha256);
            using TestExecutableLaunchLease lkgLease = ExecutableLease(workspace.Root, lkg);
            LauncherProcessStartResult lkgResult = await process.StartUntilReadyAsync(
                workspace.Root,
                statePath,
                lkg,
                lkgLease,
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            using var reader = new StreamReader(admissionPipe);
            string? admitted = await reader.ReadLineAsync(TestContext.Current.CancellationToken);
            string? second = await reader.ReadLineAsync(TestContext.Current.CancellationToken);

            Assert.Equal(LauncherProcessStartOutcome.StartFailed, candidateResult.Outcome);
            Assert.Equal(LauncherProcessStartOutcome.Ready, lkgResult.Outcome);
            Assert.Equal("ADMITTED", admitted);
            Assert.Null(second);
            Assert.Null(Environment.GetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment,
                previousAdmission);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", previousBehavior);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION", previousVersion);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION", previousIdentity);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST", previousManifest);
        }
    }

    /// <summary>Once admitted, a candidate READY timeout does not block the LKG READY attempt.</summary>
    [Fact]
    public async Task CandidateReadyTimeoutThenLkgReadyDoesNotReemitAdmission()
    {
        using var workspace = TempWorkspace.Create();
        ManagedLauncherIdentity identity = PrepareProbe(workspace.Root);
        string statePath = Path.Combine(workspace.Root, "state.json");
        using var admissionPipe = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        string inheritedDuplicate = DuplicateInheritableClientHandle(admissionPipe);
        string? previousAdmission = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment);
        string? previousBehavior = Environment.GetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR");
        string? previousVersion = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION");
        string? previousIdentity = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION");
        string? previousManifest = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST");
        try
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment,
                inheritedDuplicate);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION", identity.OwnerAppVersion.ToString());
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION", identity.OwnerAdmissionIdentity);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST", identity.OwnerReleaseManifestSha256);
            var process = new AnonymousPipeManagedLauncherProcess();
            admissionPipe.DisposeLocalCopyOfClientHandle();

            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", "timeout");
            using TestExecutableLaunchLease candidateLease = ExecutableLease(workspace.Root, identity);
            LauncherProcessStartResult candidate = await process.StartUntilReadyAsync(
                workspace.Root,
                statePath,
                identity,
                candidateLease,
                TimeSpan.FromMilliseconds(200),
                TestContext.Current.CancellationToken);

            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", "ready");
            using TestExecutableLaunchLease lkgLease = ExecutableLease(workspace.Root, identity);
            LauncherProcessStartResult lkg = await process.StartUntilReadyAsync(
                workspace.Root,
                statePath,
                identity,
                lkgLease,
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            using var reader = new StreamReader(admissionPipe);
            string? admitted = await reader.ReadLineAsync(TestContext.Current.CancellationToken);
            string? second = await reader.ReadLineAsync(TestContext.Current.CancellationToken);

            Assert.Equal(LauncherProcessStartOutcome.ReadyTimeout, candidate.Outcome);
            Assert.Equal(LauncherProcessStartOutcome.Ready, lkg.Outcome);
            Assert.Equal("ADMITTED", admitted);
            Assert.Null(second);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment,
                previousAdmission);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", previousBehavior);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION", previousVersion);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION", previousIdentity);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST", previousManifest);
        }
    }

    /// <summary>The captured admission handle is non-inheritable, so a live child cannot delay pipe EOF.</summary>
    [Fact]
    public async Task VersionLauncherCannotInheritAdmissionHandleOrDelayEof()
    {
        using var workspace = TempWorkspace.Create();
        ManagedLauncherIdentity identity = PrepareProbe(workspace.Root);
        using var admissionPipe = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        string inheritedDuplicate = DuplicateInheritableClientHandle(admissionPipe);
        string? previousAdmission = Environment.GetEnvironmentVariable(
            AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment);
        string? previousBehavior = Environment.GetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR");
        string? previousVersion = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION");
        string? previousIdentity = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION");
        string? previousManifest = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST");
        try
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment,
                inheritedDuplicate);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", "timeout");
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION", identity.OwnerAppVersion.ToString());
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION", identity.OwnerAdmissionIdentity);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST", identity.OwnerReleaseManifestSha256);
            var process = new AnonymousPipeManagedLauncherProcess();
            admissionPipe.DisposeLocalCopyOfClientHandle();
            using TestExecutableLaunchLease lease = ExecutableLease(workspace.Root, identity);
            Task<LauncherProcessStartResult> running = process.StartUntilReadyAsync(
                workspace.Root,
                Path.Combine(workspace.Root, "state.json"),
                identity,
                lease,
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken).AsTask();
            using var reader = new StreamReader(admissionPipe);

            string? admitted = await reader.ReadLineAsync(TestContext.Current.CancellationToken);
            using var eofDeadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            string? eof = await reader.ReadLineAsync(eofDeadline.Token);
            Assert.False(running.IsCompleted);
            LauncherProcessStartResult result = await running;

            Assert.Equal("ADMITTED", admitted);
            Assert.Null(eof);
            Assert.Equal(LauncherProcessStartOutcome.ReadyTimeout, result.Outcome);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                AnonymousPipeManagedLauncherProcess.BootstrapAdmissionPipeHandleEnvironment,
                previousAdmission);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_BEHAVIOR", previousBehavior);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION", previousVersion);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION", previousIdentity);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST", previousManifest);
        }
    }

    /// <summary>Accepted outer READY releases cleanup authority so its real child and grandchild survive Launcher disposal.</summary>
    [Fact]
    public async Task AcceptedOuterReadyKeepsChildAndGrandchildAlive()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        ManagedLauncherIdentity identity = PrepareProbe(workspace.Root);
        string marker = Path.Combine(workspace.Root, "accepted-tree");
        string? previousMarker = Environment.GetEnvironmentVariable("NVT_READY_PROBE_TREE_MARKER");
        int rootId = 0;
        try
        {
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_TREE_MARKER", marker);
            LauncherProcessStartResult result = await RunAsync(
                workspace.Root,
                Path.Combine(workspace.Root, "state.json"),
                identity,
                "ready-tree-root",
                argumentsPath: null,
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            rootId = int.Parse(await File.ReadAllTextAsync(
                marker + ".root",
                TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
            int childId = int.Parse(await File.ReadAllTextAsync(
                marker + ".child",
                TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);

            Assert.Equal(LauncherProcessStartOutcome.Ready, result.Outcome);
            long exitDeadline = Environment.TickCount64 + 5_000;
            while (IsRunning(rootId) && Environment.TickCount64 < exitDeadline)
            {
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }
            Assert.False(IsRunning(rootId));
            using Process acceptedDesktop = Process.GetProcessById(childId);
            Assert.False(acceptedDesktop.HasExited);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_TREE_MARKER", previousMarker);
            if (rootId != 0)
            {
                try
                {
                    using Process root = Process.GetProcessById(rootId);
                    if (!root.HasExited)
                    {
                        root.Kill(entireProcessTree: true);
                        _ = root.WaitForExit(5_000);
                    }
                }
                catch (ArgumentException)
                {
                }
            }
            string childMarker = marker + ".child";
            if (File.Exists(childMarker) && int.TryParse(File.ReadAllText(childMarker), out int childId))
            {
                try
                {
                    using Process child = Process.GetProcessById(childId);
                    if (!child.HasExited)
                    {
                        child.Kill(entireProcessTree: true);
                        _ = child.WaitForExit(5_000);
                    }
                }
                catch (ArgumentException)
                {
                }
            }
        }
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
        LauncherProcessStartResult result = await new AnonymousPipeManagedLauncherProcess()
            .StartUntilReadyAsync(
                workspace.Root,
                Path.Combine(workspace.Root, "state.json"),
                identity,
                executableLease,
                TimeSpan.FromSeconds(1),
                TestContext.Current.CancellationToken);

        Assert.Equal(LauncherProcessStartOutcome.StartFailed, result.Outcome);
        Assert.Null(result.ExitCode);
    }

    /// <summary>A changed launcher tree is rejected by the final synchronous Process.Start gate.</summary>
    [Fact]
    public async Task InvalidatedTreeLeaseFailsBeforeLauncherProcessStart()
    {
        using var workspace = TempWorkspace.Create();
        ManagedAppVersion owner = ManagedAppVersion.Parse("0.10.6");
        ManagedLauncherIdentity identity = ManagedLauncherIdentity.Create(
            owner,
            "catalog-identity-0.10.6",
            new string('a', 64),
            ManagedAppVersion.Parse("1.0.0"),
            ManagedLauncherIdentity.SupportedProtocolVersion,
            ManagedLauncherIdentity.ExecutablePath,
            68,
            new string('b', 64));
        string launcherRoot = Path.Combine(
            workspace.Root,
            "versions",
            identity.OwnerAppVersion.ToString(),
            "launcher");
        _ = Directory.CreateDirectory(launcherRoot);
        using var executableLease = new TestExecutableLaunchLease(
            Path.Combine(launcherRoot, "NvtFwCombiner.Launcher.exe"),
            launcherRoot,
            IsValidForStart: false);

        LauncherProcessStartResult result = await new AnonymousPipeManagedLauncherProcess()
            .StartUntilReadyAsync(
                workspace.Root,
                Path.Combine(workspace.Root, "state.json"),
                identity,
                executableLease,
                TimeSpan.FromSeconds(1),
                TestContext.Current.CancellationToken);

        Assert.Equal(LauncherProcessStartOutcome.StartFailed, result.Outcome);
        Assert.Null(result.ExitCode);
    }

}

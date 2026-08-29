using System.IO.Pipes;
using System.Diagnostics;
using System.Security.Cryptography;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class AnonymousPipeManagedApplicationProcessTests
{
    /// <summary>Exact Bootstrap authority crosses a real managed child and is ignored by a manual child.</summary>
    [Fact]
    public async Task BootstrapIdentityIsInheritedCapturedClearedAndRequiresManagedLifetime()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = TempWorkspace.Create();
        string probeRoot = Path.Combine(AppContext.BaseDirectory, "ready-probe");
        string probe = Path.Combine(probeRoot, "NvtFwCombiner.ReadyProbe.exe");
        string bootstrap = Path.Combine(workspace.Root, "NvtFwCombiner.Bootstrap.exe");
        foreach (string source in Directory.EnumerateFiles(probeRoot))
        {
            string name = Path.GetFileName(source);
            File.Copy(
                source,
                Path.Combine(
                    workspace.Root,
                    string.Equals(name, "NvtFwCombiner.ReadyProbe.exe", StringComparison.OrdinalIgnoreCase)
                        ? "NvtFwCombiner.Bootstrap.exe"
                        : name));
        }
        byte[] bytes = await File.ReadAllBytesAsync(
            bootstrap,
            TestContext.Current.CancellationToken);
        var identity = new ManagedImmutableBootstrapIdentity(
            "NvtFwCombiner.Bootstrap.exe",
            bytes.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
        string expectedContext = $"1|{identity.FileName}|{identity.Length}|{identity.Sha256}";
        string managedMarker = workspace.PathFor("identity/managed.txt");
        string manualMarker = workspace.PathFor("identity/manual.txt");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(managedMarker)!);
        string? previousBehavior = Environment.GetEnvironmentVariable(BehaviorEnvironment);
        string? previousMarker = Environment.GetEnvironmentVariable("NVT_READY_PROBE_IDENTITY_MARKER");
        try
        {
            Environment.SetEnvironmentVariable(BehaviorEnvironment, "bootstrap-identity-chain-root");
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_IDENTITY_MARKER", managedMarker);
            var handoff = new StableLauncherHandoff(
                workspace.Root,
                workspace.PathFor("state/version-manager.v1.json"));

            ImmutableBootstrapStartResult started = await handoff.StartAsync(
                workspace.Root,
                identity,
                TestContext.Current.CancellationToken);
            using IImmutableBootstrapLaunch launch = Assert.IsType<IImmutableBootstrapLaunch>(
                started.Launch,
                exactMatch: false);
            ImmutableBootstrapAdmissionResult admission = await launch.WaitForAdmissionAsync(
                AdmissionBudget,
                TestContext.Current.CancellationToken);
            ImmutableBootstrapCompletionResult completion = await launch.WaitForCompletionAsync(
                CompletionBudget,
                TestContext.Current.CancellationToken);

            Assert.True(started.IsStarted);
            Assert.Equal(ImmutableBootstrapAdmissionOutcome.Admitted, admission.Outcome);
            Assert.Equal(ImmutableBootstrapCompletionOutcome.Ready, completion.Outcome);
            string[] managed = await File.ReadAllLinesAsync(
                managedMarker,
                TestContext.Current.CancellationToken);
            Assert.Equal(
                [
                    nameof(InheritedManagedProcessLifetimeOutcome.Captured),
                    expectedContext,
                    "<null>",
                    identity.FileName,
                    identity.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    identity.Sha256,
                ],
                managed);

            var manualInfo = new ProcessStartInfo
            {
                FileName = probe,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            manualInfo.Environment[BehaviorEnvironment] = "identity-context-child";
            manualInfo.Environment["NVT_READY_PROBE_IDENTITY_MARKER"] = manualMarker;
            manualInfo.Environment["NVT_FW_COMBINER_ROOT_BOOTSTRAP_IDENTITY"] = expectedContext;
            foreach (string key in new[]
                     {
                         "NVT_FW_COMBINER_PROCESS_LIFETIME_CONTEXT",
                         "NVT_FW_COMBINER_PROCESS_LIFETIME_HANDLE",
                         "NVT_FW_COMBINER_PROCESS_LIFETIME_JOB",
                         "NVT_FW_COMBINER_PROCESS_LIFETIME_STATE_PATH",
                         "NVT_FW_COMBINER_PROCESS_LIFETIME_KIND",
                     })
            {
                _ = manualInfo.Environment.Remove(key);
            }
            using Process manualProcess = Process.Start(manualInfo) ??
                throw new InvalidOperationException("Manual identity probe did not start.");
            await manualProcess.WaitForExitAsync(TestContext.Current.CancellationToken);

            string[] manual = await File.ReadAllLinesAsync(
                manualMarker,
                TestContext.Current.CancellationToken);
            Assert.Equal(0, manualProcess.ExitCode);
            Assert.Equal(
                [
                    nameof(InheritedManagedProcessLifetimeOutcome.NotInherited),
                    expectedContext,
                    expectedContext,
                    "<null>",
                    "<null>",
                    "<null>",
                ],
                manual);
        }
        finally
        {
            Environment.SetEnvironmentVariable(BehaviorEnvironment, previousBehavior);
            Environment.SetEnvironmentVariable("NVT_READY_PROBE_IDENTITY_MARKER", previousMarker);
        }
    }

    /// <summary>Admission cancellation terminates both Root Bootstrap and a real descendant.</summary>
    [Fact]
    public async Task BootstrapAdmissionCancellationTerminatesOuterJobDescendant()
    {
        using var workspace = TempWorkspace.Create();
        string marker = workspace.PathFor("bootstrap-admission-cancel/grandchild.txt");
        using StableLauncherHandoff.ImmutableBootstrapProcessLaunch launch = StartBootstrapTree(
            workspace.Root,
            marker,
            "tree-root-wait",
            out AnonymousPipeClientStream client,
            out int rootId);
        await using (client)
        {
            int childId = await WaitForProcessMarkerAsync(marker);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            ImmutableBootstrapAdmissionResult result =
                await launch.WaitForAdmissionAsync(AdmissionBudget, cancellation.Token);

            Assert.Equal(ImmutableBootstrapAdmissionOutcome.HealthUnavailable, result.Outcome);
            Assert.False(IsRunning(rootId));
            Assert.False(IsRunning(childId));
        }
    }

    /// <summary>An exit before ADMITTED cannot strand a descendant outside cleanup custody.</summary>
    [Fact]
    public async Task BootstrapExitBeforeAdmissionTerminatesOuterJobDescendant()
    {
        using var workspace = TempWorkspace.Create();
        string marker = workspace.PathFor("bootstrap-exit-before-admission/grandchild.txt");
        using StableLauncherHandoff.ImmutableBootstrapProcessLaunch launch =
            StartBootstrapTreeWithoutAdmissionWriter(
            workspace.Root,
            marker,
            "tree-root-exit",
            out int rootId);
        int childId = await WaitForProcessMarkerAsync(marker);

        ImmutableBootstrapAdmissionResult result = await launch.WaitForAdmissionAsync(
            AdmissionBudget,
            TestContext.Current.CancellationToken);

        Assert.Equal(ImmutableBootstrapAdmissionOutcome.HealthUnavailable, result.Outcome);
        Assert.False(IsRunning(rootId));
        Assert.False(IsRunning(childId));
    }

    /// <summary>Completion timeout terminates the admitted Root Bootstrap and its descendant.</summary>
    [Fact]
    public async Task BootstrapCompletionTimeoutTerminatesOuterJobDescendant()
    {
        using var workspace = TempWorkspace.Create();
        string marker = workspace.PathFor("bootstrap-completion-timeout/grandchild.txt");
        using StableLauncherHandoff.ImmutableBootstrapProcessLaunch launch = StartBootstrapTree(
            workspace.Root,
            marker,
            "tree-root-wait",
            out AnonymousPipeClientStream client,
            out int rootId);
        await using (client)
        {
            int childId = await WaitForProcessMarkerAsync(marker);
            await client.WriteAsync("ADMITTED\n"u8.ToArray(), TestContext.Current.CancellationToken);
            await client.FlushAsync(TestContext.Current.CancellationToken);
            ImmutableBootstrapAdmissionResult admission = await launch.WaitForAdmissionAsync(
                AdmissionBudget,
                TestContext.Current.CancellationToken);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            ImmutableBootstrapCompletionResult completion =
                await launch.WaitForCompletionAsync(CompletionBudget, cancellation.Token);

            Assert.Equal(ImmutableBootstrapAdmissionOutcome.Admitted, admission.Outcome);
            Assert.Equal(ImmutableBootstrapCompletionOutcome.Unavailable, completion.Outcome);
            Assert.False(IsRunning(rootId));
            Assert.False(IsRunning(childId));
        }
    }

    /// <summary>Successful exit zero or rollback one releases the accepted outer job tree.</summary>
    [Theory]
    [InlineData("tree-root-exit", ImmutableBootstrapCompletionOutcome.Ready, 0)]
    [InlineData("tree-root-rollback", ImmutableBootstrapCompletionOutcome.RolledBack, 1)]
    public async Task BootstrapSuccessReleasesAcceptedOuterJobDescendant(
        string behavior,
        ImmutableBootstrapCompletionOutcome expected,
        int exitCode)
    {
        using var workspace = TempWorkspace.Create();
        string marker = workspace.PathFor($"bootstrap-accepted-{exitCode}/grandchild.txt");
        int childId = 0;
        try
        {
            using StableLauncherHandoff.ImmutableBootstrapProcessLaunch launch = StartBootstrapTree(
                workspace.Root,
                marker,
                behavior,
                out AnonymousPipeClientStream client,
                out _);
            await using (client)
            {
                childId = await WaitForProcessMarkerAsync(marker);
                await client.WriteAsync("ADMITTED\n"u8.ToArray(), TestContext.Current.CancellationToken);
                await client.FlushAsync(TestContext.Current.CancellationToken);

                ImmutableBootstrapAdmissionResult admission = await launch.WaitForAdmissionAsync(
                    AdmissionBudget,
                    TestContext.Current.CancellationToken);
                ImmutableBootstrapCompletionResult completion = await launch.WaitForCompletionAsync(
                    CompletionBudget,
                    TestContext.Current.CancellationToken);

                Assert.Equal(ImmutableBootstrapAdmissionOutcome.Admitted, admission.Outcome);
                Assert.Equal(expected, completion.Outcome);
                Assert.Equal(exitCode, completion.ExitCode);
                Assert.True(IsRunning(childId));
            }
        }
        finally
        {
            TerminateProcess(childId);
        }
    }

    /// <summary>A later failed invocation cannot recapture an earlier accepted Desktop tree.</summary>
    [Fact]
    public async Task SequentialBootstrapFailurePreservesPriorAcceptedDescendant()
    {
        using var workspace = TempWorkspace.Create();
        int acceptedChildId = 0;
        try
        {
            string acceptedMarker = workspace.PathFor("bootstrap-first-accepted/grandchild.txt");
            using (StableLauncherHandoff.ImmutableBootstrapProcessLaunch accepted = StartBootstrapTree(
                       workspace.Root,
                       acceptedMarker,
                       "tree-root-exit",
                       out AnonymousPipeClientStream acceptedClient,
                       out _))
            await using (acceptedClient)
            {
                acceptedChildId = await WaitForProcessMarkerAsync(acceptedMarker);
                await acceptedClient.WriteAsync(
                    "ADMITTED\n"u8.ToArray(),
                    TestContext.Current.CancellationToken);
                await acceptedClient.FlushAsync(TestContext.Current.CancellationToken);

                ImmutableBootstrapAdmissionResult admission = await accepted.WaitForAdmissionAsync(
                    AdmissionBudget,
                    TestContext.Current.CancellationToken);
                ImmutableBootstrapCompletionResult completion = await accepted.WaitForCompletionAsync(
                    CompletionBudget,
                    TestContext.Current.CancellationToken);

                Assert.Equal(ImmutableBootstrapAdmissionOutcome.Admitted, admission.Outcome);
                Assert.Equal(ImmutableBootstrapCompletionOutcome.Ready, completion.Outcome);
                Assert.True(IsRunning(acceptedChildId));
            }

            string failedMarker = workspace.PathFor("bootstrap-second-failed/grandchild.txt");
            using StableLauncherHandoff.ImmutableBootstrapProcessLaunch failed = StartBootstrapTree(
                workspace.Root,
                failedMarker,
                "tree-root-wait",
                out AnonymousPipeClientStream failedClient,
                out int failedRootId);
            await using (failedClient)
            {
                int failedChildId = await WaitForProcessMarkerAsync(failedMarker);
                using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

                ImmutableBootstrapAdmissionResult failedAdmission = await failed.WaitForAdmissionAsync(
                    AdmissionBudget,
                    cancellation.Token);

                Assert.Equal(
                    ImmutableBootstrapAdmissionOutcome.HealthUnavailable,
                    failedAdmission.Outcome);
                Assert.False(IsRunning(failedRootId));
                Assert.False(IsRunning(failedChildId));
                Assert.True(IsRunning(acceptedChildId));
            }
        }
        finally
        {
            TerminateProcess(acceptedChildId);
        }
    }
}

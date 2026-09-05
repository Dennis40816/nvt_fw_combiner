using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Globalization;
using Microsoft.Win32.SafeHandles;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

const string handleKey = "NVT_FW_COMBINER_READY_PIPE_HANDLE";
const string versionKey = "NVT_FW_COMBINER_EXPECTED_VERSION";
const string launcherHandleKey = "NVT_FW_COMBINER_LAUNCHER_READY_PIPE_HANDLE";
const string launcherExpectedKey = "NVT_FW_COMBINER_EXPECTED_LAUNCHER_READY";
const string lifetimeKindKey = "NVT_FW_COMBINER_PROCESS_LIFETIME_KIND";
const string behaviorKey = "NVT_READY_PROBE_BEHAVIOR";
const string bootstrapIdentityKey = "NVT_FW_COMBINER_ROOT_BOOTSTRAP_IDENTITY";
const string identityMarkerKey = "NVT_READY_PROBE_IDENTITY_MARKER";
const string bootstrapAdmissionKey = "NVT_FW_COMBINER_BOOTSTRAP_ADMISSION_PIPE_HANDLE";
string behavior = Environment.GetEnvironmentVariable(behaviorKey) ?? "ready";
if (string.Equals(behavior, "probe-ambient-pipe", StringComparison.Ordinal))
{
    string marker = Environment.GetEnvironmentVariable("NVT_READY_PROBE_HANDLE_MARKER") ??
        throw new InvalidOperationException("Missing handle marker.");
    string value = Environment.GetEnvironmentVariable("NVT_READY_PROBE_AMBIENT_HANDLE") ??
        throw new InvalidOperationException("Missing ambient handle value.");
    await File.WriteAllTextAsync(marker, "started");
    if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long raw))
    {
        try
        {
            using var probeHandle = new SafePipeHandle(new IntPtr(raw), ownsHandle: false);
            await using var probePipe = new AnonymousPipeClientStream(PipeDirection.Out, probeHandle);
            await probePipe.WriteAsync("leaked"u8.ToArray());
            await probePipe.FlushAsync();
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
        {
            // Exact physical-pipe observation: an excluded or reused non-pipe handle cannot write.
        }
    }
    return 0;
}
if (string.Equals(behavior, "probe-contained-isolation", StringComparison.Ordinal))
{
    string payload = Environment.GetEnvironmentVariable("NVT_READY_PROBE_ISOLATION_PAYLOAD") ??
        throw new InvalidOperationException("Missing isolation payload.");
    await TryWriteInheritedPipeAsync(
        Environment.GetEnvironmentVariable("NVT_READY_PROBE_ALLOWED_HANDLE"),
        Encoding.UTF8.GetBytes(payload));
    await TryWriteInheritedPipeAsync(
        Environment.GetEnvironmentVariable("NVT_READY_PROBE_CROSS_HANDLE"),
        Encoding.UTF8.GetBytes($"cross:{payload}"));
    return 0;
}
if (string.Equals(behavior, "probe-arguments-environment", StringComparison.Ordinal))
{
    string marker = Environment.GetEnvironmentVariable("NVT_READY_PROBE_ARGUMENT_MARKER") ??
        throw new InvalidOperationException("Missing argument marker.");
    string value = Environment.GetEnvironmentVariable("NVT_READY_PROBE_UNICODE_ENVIRONMENT") ??
        throw new InvalidOperationException("Missing Unicode environment value.");
    await File.WriteAllLinesAsync(marker, [value, Environment.CurrentDirectory, .. args]);
    return 0;
}
if (behavior is "tree-grandchild" or "ready-tree-grandchild")
{
    string marker = Environment.GetEnvironmentVariable("NVT_READY_PROBE_TREE_MARKER") ??
        throw new InvalidOperationException("Missing tree marker.");
    string target = string.Equals(behavior, "ready-tree-grandchild", StringComparison.Ordinal)
        ? marker + ".child"
        : marker;
    await File.WriteAllTextAsync(target, Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    await Task.Delay(string.Equals(behavior, "ready-tree-grandchild", StringComparison.Ordinal)
        ? TimeSpan.FromSeconds(30)
        : TimeSpan.FromSeconds(3));
    return 0;
}
string? launcherExpected = Environment.GetEnvironmentVariable(launcherExpectedKey);
string? version = Environment.GetEnvironmentVariable(versionKey);
bool isManagedApplication = version is not null;
bool isBootstrap = string.Equals(
    Environment.GetEnvironmentVariable(lifetimeKindKey),
    ManagedProcessLifetimeKind.Bootstrap.ToString(),
    StringComparison.Ordinal);
bool quietBootstrap = isBootstrap && string.Equals(behavior, "ready", StringComparison.Ordinal);
string? statePath = null;
for (int index = 0; index < args.Length; index++)
{
    if (string.Equals(args[index], "--state-path", StringComparison.Ordinal) && index + 1 < args.Length)
    {
        statePath = Path.GetFullPath(args[++index]);
    }
}
if (quietBootstrap || string.Equals(behavior, "bootstrap-identity-chain-root", StringComparison.Ordinal))
{
    string? startContext = Environment.GetEnvironmentVariable(
        "NVT_FW_COMBINER_BOOTSTRAP_START_CONTEXT");
    string startHandle = Environment.GetEnvironmentVariable(
        "NVT_FW_COMBINER_BOOTSTRAP_START_PIPE_HANDLE") ??
        throw new InvalidOperationException("Missing Bootstrap START pipe.");
    Environment.SetEnvironmentVariable("NVT_FW_COMBINER_BOOTSTRAP_START_CONTEXT", null);
    Environment.SetEnvironmentVariable("NVT_FW_COMBINER_BOOTSTRAP_START_PIPE_HANDLE", null);
    if (!string.Equals(startContext, "v1", StringComparison.Ordinal))
    {
        return 26;
    }
    await using var startPipe = new AnonymousPipeClientStream(PipeDirection.In, startHandle);
    byte[] start = new byte[6];
    await startPipe.ReadExactlyAsync(start);
    if (!start.AsSpan().SequenceEqual("START\n"u8))
    {
        return 26;
    }
}
if (string.Equals(behavior, "bootstrap-identity-chain-root", StringComparison.Ordinal))
{
    string marker = Environment.GetEnvironmentVariable(identityMarkerKey) ??
        throw new InvalidOperationException("Missing identity marker.");
    var childInfo = new ProcessStartInfo
    {
        FileName = Environment.ProcessPath ?? throw new InvalidOperationException("Missing process path."),
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    childInfo.ArgumentList.Add("--state-path");
    childInfo.ArgumentList.Add(statePath ?? throw new InvalidOperationException("Missing state path."));
    childInfo.Environment[behaviorKey] = "identity-context-child";
    childInfo.Environment[identityMarkerKey] = marker;
    using Process child = Process.Start(childInfo) ??
        throw new InvalidOperationException("Identity child did not start.");
    await child.WaitForExitAsync();
    if (child.ExitCode != 0)
    {
        return child.ExitCode;
    }
}
using IInheritedManagedProcessLifetimeCapture lifetime = InheritedManagedProcessLifetime.Capture(
    statePath,
    isManagedApplication
        ? ManagedProcessLifetimeKind.Application
        : isBootstrap
            ? ManagedProcessLifetimeKind.Bootstrap
            : ManagedProcessLifetimeKind.Launcher,
    statePath is not null || version is not null || launcherExpected is not null);
if (string.Equals(behavior, "identity-context-child", StringComparison.Ordinal))
{
    string marker = Environment.GetEnvironmentVariable(identityMarkerKey) ??
        throw new InvalidOperationException("Missing identity marker.");
    string? before = Environment.GetEnvironmentVariable(bootstrapIdentityKey);
    ManagedImmutableBootstrapIdentity? authority =
        lifetime.Outcome == InheritedManagedProcessLifetimeOutcome.Captured
            ? InheritedManagedBootstrapIdentityContext.CaptureAndClear()
            : null;
    string? after = Environment.GetEnvironmentVariable(bootstrapIdentityKey);
    await File.WriteAllLinesAsync(
        marker,
        [
            lifetime.Outcome.ToString(),
            before ?? "<null>",
            after ?? "<null>",
            authority?.FileName ?? "<null>",
            authority?.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>",
            authority?.Sha256 ?? "<null>",
        ]);
    return 0;
}
if (lifetime.Outcome != InheritedManagedProcessLifetimeOutcome.Captured)
{
    return 24;
}
if (quietBootstrap)
{
    // Custody tests need a real gated process, not an interactive shell sharing runner stdio.
    return ImmutableBootstrapExitCodeCodec.EncodeFailure(ImmutableBootstrapExitIssue.StateUnavailable);
}
if (string.Equals(behavior, "bootstrap-exit-22", StringComparison.Ordinal))
{
    string marker = Environment.GetEnvironmentVariable("NVT_READY_PROBE_TREE_MARKER") ??
        throw new InvalidOperationException("Missing exit marker.");
    await File.WriteAllTextAsync(
        marker,
        Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
    return ImmutableBootstrapExitCodeCodec.EncodeFailure(
        ImmutableBootstrapExitIssue.InvalidInheritedContext);
}
if (string.Equals(behavior, "bootstrap-eof-before-exit-18", StringComparison.Ordinal))
{
    string marker = Environment.GetEnvironmentVariable("NVT_READY_PROBE_TREE_MARKER") ??
        throw new InvalidOperationException("Missing exit marker.");
    await File.WriteAllTextAsync(
        marker,
        Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
    string release = marker + ".release";
    long deadline = Environment.TickCount64 + 5_000;
    while (!File.Exists(release) && Environment.TickCount64 < deadline)
    {
        await Task.Delay(10);
    }
    return File.Exists(release)
        ? ImmutableBootstrapExitCodeCodec.EncodeFailure(
            ImmutableBootstrapExitIssue.StateUnavailable)
        : 25;
}
if (string.Equals(behavior, "launcher-identity-observation", StringComparison.Ordinal))
{
    string marker = Environment.GetEnvironmentVariable(identityMarkerKey) ??
        throw new InvalidOperationException("Missing identity marker.");
    string? before = Environment.GetEnvironmentVariable(bootstrapIdentityKey);
    ManagedImmutableBootstrapIdentity? authority =
        InheritedManagedBootstrapIdentityContext.CaptureAndClear();
    string? after = Environment.GetEnvironmentVariable(bootstrapIdentityKey);
    await File.WriteAllLinesAsync(
        marker,
        [
            lifetime.Outcome.ToString(),
            before ?? "<null>",
            after ?? "<null>",
            authority?.FileName ?? "<null>",
            authority?.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>",
            authority?.Sha256 ?? "<null>",
        ]);
}
if (string.Equals(behavior, "bootstrap-identity-chain-root", StringComparison.Ordinal))
{
    string admissionHandle = Environment.GetEnvironmentVariable(bootstrapAdmissionKey) ??
        throw new InvalidOperationException("Missing Bootstrap admission pipe.");
    await using var admissionPipe = new AnonymousPipeClientStream(PipeDirection.Out, admissionHandle);
    await admissionPipe.WriteAsync("ADMITTED\n"u8.ToArray());
    await admissionPipe.FlushAsync();
    return 0;
}
string? argumentsPath = Environment.GetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH");
if (!string.IsNullOrWhiteSpace(argumentsPath))
{
    await File.WriteAllLinesAsync(argumentsPath, args);
}
if (string.Equals(behavior, "ready-tree-root", StringComparison.Ordinal))
{
    string marker = Environment.GetEnvironmentVariable("NVT_READY_PROBE_TREE_MARKER") ??
        throw new InvalidOperationException("Missing tree marker.");
    await File.WriteAllTextAsync(
        marker + ".root",
        Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    var childInfo = new ProcessStartInfo
    {
        FileName = Environment.ProcessPath ?? throw new InvalidOperationException("Missing process path."),
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    childInfo.Environment[behaviorKey] = "ready-tree-grandchild";
    childInfo.Environment["NVT_READY_PROBE_TREE_MARKER"] = marker;
    _ = Process.Start(childInfo) ?? throw new InvalidOperationException("Grandchild did not start.");
    long treeDeadline = Environment.TickCount64 + 5_000;
    while (!File.Exists(marker + ".child") && Environment.TickCount64 < treeDeadline)
    {
        await Task.Delay(10);
    }
    if (!File.Exists(marker + ".child"))
    {
        return 25;
    }
}
if (string.Equals(behavior, "tree-root-exit", StringComparison.Ordinal) ||
    string.Equals(behavior, "tree-root-wait", StringComparison.Ordinal) ||
    string.Equals(behavior, "tree-root-rollback", StringComparison.Ordinal))
{
    string marker = Environment.GetEnvironmentVariable("NVT_READY_PROBE_TREE_MARKER") ??
        throw new InvalidOperationException("Missing tree marker.");
    var childInfo = new ProcessStartInfo
    {
        FileName = Environment.ProcessPath ?? throw new InvalidOperationException("Missing process path."),
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    childInfo.Environment[behaviorKey] = "tree-grandchild";
    childInfo.Environment["NVT_READY_PROBE_TREE_MARKER"] = marker;
    using Process child = Process.Start(childInfo) ?? throw new InvalidOperationException("Grandchild did not start.");
    long deadline = Environment.TickCount64 + 5_000;
    while (!File.Exists(marker) && Environment.TickCount64 < deadline)
    {
        await Task.Delay(10);
    }
    if (!File.Exists(marker))
    {
        return 25;
    }
    if (string.Equals(behavior, "tree-root-wait", StringComparison.Ordinal))
    {
        await Task.Delay(TimeSpan.FromSeconds(30));
    }
    return string.Equals(behavior, "tree-root-rollback", StringComparison.Ordinal) ? 1 : 0;
}
string expected = isManagedApplication
    ? $"READY:{version}"
    : launcherExpected ?? throw new InvalidOperationException("Missing expected READY identity.");
if (!isManagedApplication && launcherExpected is not null)
{
    string readyVersion = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_VERSION") ??
        throw new InvalidOperationException("Missing test app version.");
    string readyAdmission = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_ADMISSION") ??
        throw new InvalidOperationException("Missing test app admission.");
    string readyManifest = Environment.GetEnvironmentVariable("NVT_READY_PROBE_APP_MANIFEST") ??
        throw new InvalidOperationException("Missing test app manifest.");
    expected = string.Join(
        ':',
        expected,
        readyVersion,
        Convert.ToBase64String(Encoding.UTF8.GetBytes(readyAdmission)),
        readyManifest);
}
bool candidate = expected.Contains("0.10.6", StringComparison.Ordinal);
if (string.Equals(behavior, "exit", StringComparison.Ordinal) ||
    (!isManagedApplication && candidate && string.Equals(behavior, "exit-outer-candidate", StringComparison.Ordinal)) ||
    (candidate && string.Equals(behavior, "exit-candidate", StringComparison.Ordinal)))
{
    return 7;
}
if (!isManagedApplication && string.Equals(behavior, "termination-unconfirmed", StringComparison.Ordinal))
{
    return 17;
}
if (string.Equals(behavior, "timeout", StringComparison.Ordinal) ||
    (candidate && string.Equals(behavior, "timeout-candidate", StringComparison.Ordinal)))
{
    await Task.Delay(TimeSpan.FromSeconds(30));
    return 0;
}

string handle = Environment.GetEnvironmentVariable(isManagedApplication ? handleKey : launcherHandleKey) ??
    throw new InvalidOperationException("Missing pipe handle.");
await using var pipe = new AnonymousPipeClientStream(PipeDirection.Out, handle);
byte[] message = string.Equals(behavior, "invalid-utf8", StringComparison.Ordinal)
    ? [0xC3, 0x28, 0x0A]
    : string.Equals(behavior, "oversized", StringComparison.Ordinal)
        ? Encoding.UTF8.GetBytes(new string('X', 256) + "\n")
    : Encoding.UTF8.GetBytes(
        string.Equals(behavior, "invalid", StringComparison.Ordinal)
            ? "INVALID\n"
            : expected + "\n");
await pipe.WriteAsync(message);
await pipe.FlushAsync();
if (candidate && string.Equals(behavior, "ready-exit-candidate", StringComparison.Ordinal))
{
    return 9;
}
await Task.Delay(string.Equals(behavior, "ready-tree-root", StringComparison.Ordinal)
    ? TimeSpan.Zero
    : TimeSpan.FromMilliseconds(200));
return 0;

static async Task TryWriteInheritedPipeAsync(string? value, byte[] payload)
{
    if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long raw))
    {
        return;
    }
    try
    {
        using var probeHandle = new SafePipeHandle(new IntPtr(raw), ownsHandle: false);
        await using var probePipe = new AnonymousPipeClientStream(PipeDirection.Out, probeHandle);
        await probePipe.WriteAsync(payload);
        await probePipe.FlushAsync();
    }
    catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
    {
        // Exact physical-pipe assertions are made by the parent test.
    }
}

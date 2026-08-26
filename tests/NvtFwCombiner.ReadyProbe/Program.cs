using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

const string handleKey = "NVT_FW_COMBINER_READY_PIPE_HANDLE";
const string versionKey = "NVT_FW_COMBINER_EXPECTED_VERSION";
const string launcherHandleKey = "NVT_FW_COMBINER_LAUNCHER_READY_PIPE_HANDLE";
const string launcherExpectedKey = "NVT_FW_COMBINER_EXPECTED_LAUNCHER_READY";
const string behaviorKey = "NVT_READY_PROBE_BEHAVIOR";
string behavior = Environment.GetEnvironmentVariable(behaviorKey) ?? "ready";
using IInheritedManagedProcessLifetimeCapture lifetime = InheritedManagedProcessLifetime.Capture();
if (!string.Equals(behavior, "tree-grandchild", StringComparison.Ordinal) &&
    lifetime.Outcome != InheritedManagedProcessLifetimeOutcome.Captured)
{
    return 24;
}
if (string.Equals(behavior, "tree-grandchild", StringComparison.Ordinal))
{
    string marker = Environment.GetEnvironmentVariable("NVT_READY_PROBE_TREE_MARKER") ??
        throw new InvalidOperationException("Missing tree marker.");
    await File.WriteAllTextAsync(marker, Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    await Task.Delay(TimeSpan.FromSeconds(3));
    return 0;
}
if (string.Equals(behavior, "tree-root-exit", StringComparison.Ordinal))
{
    string marker = Environment.GetEnvironmentVariable("NVT_READY_PROBE_TREE_MARKER") ??
        throw new InvalidOperationException("Missing tree marker.");
    var childInfo = new ProcessStartInfo
    {
        FileName = Environment.ProcessPath ?? throw new InvalidOperationException("Missing process path."),
        UseShellExecute = false,
    };
    childInfo.Environment[behaviorKey] = "tree-grandchild";
    childInfo.Environment["NVT_READY_PROBE_TREE_MARKER"] = marker;
    using Process child = Process.Start(childInfo) ?? throw new InvalidOperationException("Grandchild did not start.");
    long deadline = Environment.TickCount64 + 5_000;
    while (!File.Exists(marker) && Environment.TickCount64 < deadline)
    {
        await Task.Delay(10);
    }
    return File.Exists(marker) ? 0 : 25;
}
string? argumentsPath = Environment.GetEnvironmentVariable("NVT_READY_PROBE_ARGS_PATH");
if (!string.IsNullOrWhiteSpace(argumentsPath))
{
    await File.WriteAllLinesAsync(argumentsPath, args);
}
string? launcherExpected = Environment.GetEnvironmentVariable(launcherExpectedKey);
string? version = Environment.GetEnvironmentVariable(versionKey);
bool isManagedApplication = version is not null;
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
await Task.Delay(200);
return 0;

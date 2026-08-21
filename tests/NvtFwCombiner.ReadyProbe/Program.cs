using System.IO.Pipes;
using System.Text;

const string handleKey = "NVT_FW_COMBINER_READY_PIPE_HANDLE";
const string versionKey = "NVT_FW_COMBINER_EXPECTED_VERSION";
const string behaviorKey = "NVT_READY_PROBE_BEHAVIOR";
string behavior = Environment.GetEnvironmentVariable(behaviorKey) ?? "ready";
string version = Environment.GetEnvironmentVariable(versionKey) ?? throw new InvalidOperationException("Missing expected version.");
bool candidate = string.Equals(version, "0.10.6", StringComparison.Ordinal);
if (string.Equals(behavior, "exit", StringComparison.Ordinal) ||
    (candidate && string.Equals(behavior, "exit-candidate", StringComparison.Ordinal)))
{
    return 7;
}
if (string.Equals(behavior, "timeout", StringComparison.Ordinal) ||
    (candidate && string.Equals(behavior, "timeout-candidate", StringComparison.Ordinal)))
{
    await Task.Delay(TimeSpan.FromSeconds(30));
    return 0;
}

string handle = Environment.GetEnvironmentVariable(handleKey) ?? throw new InvalidOperationException("Missing pipe handle.");
await using var pipe = new AnonymousPipeClientStream(PipeDirection.Out, handle);
byte[] message = string.Equals(behavior, "invalid-utf8", StringComparison.Ordinal)
    ? [0xC3, 0x28, 0x0A]
    : string.Equals(behavior, "oversized", StringComparison.Ordinal)
        ? Encoding.UTF8.GetBytes(new string('X', 256) + "\n")
    : Encoding.UTF8.GetBytes(
        string.Equals(behavior, "invalid", StringComparison.Ordinal)
            ? "INVALID\n"
            : $"READY:{version}\n");
await pipe.WriteAsync(message);
await pipe.FlushAsync();
if (candidate && string.Equals(behavior, "ready-exit-candidate", StringComparison.Ordinal))
{
    return 9;
}
await Task.Delay(200);
return 0;

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

internal sealed class RuntimeTrustProbeProcess(
    IExternalProcessRunner runner,
    string hostExecutable,
    IReadOnlyList<string> hostArguments)
{
    internal const string Command = "--internal-runtime-trust-v1";

    internal static RuntimeTrustProbeProcess CreateDefault()
    {
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("The trusted host path is unavailable.");
        string[] arguments = string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase)
            ? [Assembly.GetEntryAssembly()?.Location ?? throw new InvalidOperationException("The trusted entry assembly is unavailable.")]
            : [];
        return new(new SystemExternalProcessRunner(), executable, arguments);
    }

    internal async ValueTask<RuntimeTrustProbeVerdict?> VerifyAsync(string path, string sha256, CancellationToken cancellationToken)
    {
        string requestId = Guid.NewGuid().ToString("D");
        var start = new ExternalProcessStartInfo(hostExecutable, Path.GetDirectoryName(hostExecutable)!,
            [.. hostArguments, Command, path, sha256, requestId], TimeSpan.FromSeconds(30));
        ExternalProcessResult result;
        try
        {
            result = await runner.RunAsync(start, cancellationToken).ConfigureAwait(false);
        }
        catch (ExternalProcessCleanupCapacityException exception)
        {
            // Keep the existing issue code, but carry the capacity reason and restart guidance to the caller
            // (owner decision 92) so the user is told how to recover rather than seeing a generic failure.
            return new("runtime.trust.probe-failed", ExternalProcessCleanupText.CapacityMessage(exception));
        }
        catch (ExternalProcessStartFailedException)
        {
            return new("runtime.trust.probe-failed");
        }
        // Timeout keeps priority over an incomplete cleanup: a timed-out probe is always runtime.trust.timeout.
        if (result.TimedOut) { return new("runtime.trust.timeout"); }
        if (result.Cleanup != ExternalProcessCleanup.Complete) { return new("runtime.trust.probe-failed"); }
        if (result.ExitCode != 0) { return new("runtime.trust.probe-failed"); }
        try
        {
            using JsonDocument json = StrictJsonDocumentReader.Parse(Encoding.UTF8.GetBytes(result.StandardOutput), 4096, 4);
            RuntimeTrustProbeResponse? response = json.Deserialize(RuntimeTrustProbeJson.Default.RuntimeTrustProbeResponse);
            return response is null || response.ProtocolVersion != 1 || response.RequestId != requestId ||
                !string.Equals(response.Sha256, sha256, StringComparison.Ordinal) ||
                (response.IssueCode is not null && !response.IssueCode.StartsWith("runtime.trust.", StringComparison.Ordinal))
                ? new("runtime.trust.protocol-invalid")
                : response.IssueCode is null ? null : new RuntimeTrustProbeVerdict(response.IssueCode);
        }
        catch (JsonException)
        {
            return new("runtime.trust.protocol-invalid");
        }
    }

    internal static bool TryHandle(string[] arguments, TextWriter output, out int exitCode)
    {
        exitCode = 0;
        if (arguments.Length == 0 || arguments[0] != Command) { return false; }
        if (arguments.Length != 4 || !Path.IsPathFullyQualified(arguments[1]) ||
            arguments[2].Length != 64 || !arguments[2].All(Uri.IsHexDigit) ||
            !Guid.TryParseExact(arguments[3], "D", out _))
        {
            exitCode = 2;
            return true;
        }
        string? issue = WindowsRuntimeTrustProbe.Verify(arguments[1], arguments[2]);
        output.Write(JsonSerializer.Serialize(new RuntimeTrustProbeResponse
        {
            ProtocolVersion = 1,
            RequestId = arguments[3],
            Sha256 = arguments[2],
            IssueCode = issue,
        }, RuntimeTrustProbeJson.Default.RuntimeTrustProbeResponse));
        return true;
    }
}

/// <summary>A trust-probe rejection: the stable issue code, and an optional user-facing recovery message.</summary>
internal sealed record RuntimeTrustProbeVerdict(string IssueCode, string? UserMessage = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class RuntimeTrustProbeResponse
{
    public required int ProtocolVersion { get; init; }
    public required string RequestId { get; init; }
    public required string Sha256 { get; init; }
    public required string? IssueCode { get; init; }
}

[JsonSerializable(typeof(RuntimeTrustProbeResponse))]
internal sealed partial class RuntimeTrustProbeJson : JsonSerializerContext;

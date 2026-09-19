using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>The executable composition root reserves one strict internal trust-probe entry point.</summary>
public sealed class ToolchainProbeHostTests
{
    /// <summary>Ordinary application arguments never enter the internal probe path.</summary>
    [Fact]
    public void OrdinaryArgumentsAreNotHandled()
    {
        using var output = new StringWriter();
        Assert.False(CompositionHostServices.TryHandleRuntimeTrustProbe(["replace"], output, out int exitCode));
        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, output.ToString());
    }

    /// <summary>A matching internal token with malformed arguments terminates instead of falling into normal startup.</summary>
    [Fact]
    public void MalformedInternalArgumentsFailClosed()
    {
        using var output = new StringWriter();
        Assert.True(CompositionHostServices.TryHandleRuntimeTrustProbe(
            ["--internal-runtime-trust-v1", "relative.dll"], output, out int exitCode));
        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output.ToString());
    }

    /// <summary>The host emits one correlated bounded response for a syntactically valid request.</summary>
    [Fact]
    public void ValidRequestEmitsCorrelatedResponse()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        using TempWorkspace workspace = TempWorkspace.Create("runtime-probe-host");
        byte[] bytes = [0x4D, 0x5A, 0x01];
        string path = workspace.Write("candidate.dll", bytes);
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        string requestId = Guid.NewGuid().ToString("D");
        using var output = new StringWriter();

        Assert.True(CompositionHostServices.TryHandleRuntimeTrustProbe(
            ["--internal-runtime-trust-v1", path, sha256, requestId], output, out int exitCode));

        Assert.Equal(0, exitCode);
        using JsonDocument json = JsonDocument.Parse(output.ToString());
        Assert.Equal(1, json.RootElement.GetProperty("ProtocolVersion").GetInt32());
        Assert.Equal(requestId, json.RootElement.GetProperty("RequestId").GetString());
        Assert.Equal(sha256, json.RootElement.GetProperty("Sha256").GetString());
        Assert.Equal("runtime.trust.signature-invalid", json.RootElement.GetProperty("IssueCode").GetString());
    }
}

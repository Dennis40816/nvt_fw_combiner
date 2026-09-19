using System.Text.Json;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Candidate inspection joins exact PE compatibility and correlated trust evidence.</summary>
public sealed class ToolchainRuntimeCandidateInspectorTests
{
    /// <summary>The bundled package is described from its approved bytes without pretending user verification.</summary>
    [Fact]
    public async Task BundledRuntimeUsesApprovedToolCompatibility()
    {
        var inspector = new ToolchainRuntimeCandidateInspector(new LocalFileStore(),
            new RuntimeTrustProbeProcess(new CorrelatedRunner(), TrustedHostPath(), []));

        ToolchainRuntimeCandidateInspection result = await inspector.InspectBundledAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ToolchainRuntimeCandidateVerification.Unknown, result.Verification);
        Assert.NotNull(result.Identity);
        Assert.EndsWith("vcruntime140.dll", result.Identity.Path, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Issues);
    }

    /// <summary>Import inspection cannot consume executable bytes that differ from the manifest pin.</summary>
    [Fact]
    public void ChangedToolSnapshotRejectsBeforeCompatibilityParsing()
    {
        byte[] changed = [0x4D, 0x5A, 0x01];
        Assert.Equal("runtime.tool.identity-changed",
            ToolchainRuntimeCandidateInspector.CheckToolSnapshot(changed, new string('a', 64), [0x4D, 0x5A]));
    }

    /// <summary>A system candidate is admitted only after a matching response from the isolated trust process.</summary>
    [Fact]
    public async Task UserRuntimeRequiresCorrelatedProbeSuccess()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        var runner = new CorrelatedRunner();
        var inspector = new ToolchainRuntimeCandidateInspector(new LocalFileStore(),
            new RuntimeTrustProbeProcess(runner, TrustedHostPath(), ["trusted-entry.dll"]));
        string path = Path.Combine(Environment.SystemDirectory, "vcruntime140.dll");

        ToolchainRuntimeCandidateInspection result = await inspector.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(ToolchainRuntimeCandidateVerification.Verified, result.Verification);
        Assert.NotNull(result.Identity);
        Assert.Equal(Path.GetFullPath(path), result.Identity.Path);
        Assert.Equal(1, runner.Calls);
    }

    /// <summary>Mismatched trust correlation rejects the candidate instead of trusting child output.</summary>
    [Fact]
    public async Task MismatchedProbeResponseRejectsCandidate()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        var inspector = new ToolchainRuntimeCandidateInspector(new LocalFileStore(),
            new RuntimeTrustProbeProcess(new CorrelatedRunner(mismatch: true), TrustedHostPath(), []));

        ToolchainRuntimeCandidateInspection result = await inspector.InspectAsync(
            Path.Combine(Environment.SystemDirectory, "vcruntime140.dll"), TestContext.Current.CancellationToken);

        Assert.Equal(ToolchainRuntimeCandidateVerification.Rejected, result.Verification);
        Assert.Contains(result.Issues, static issue => issue.Code == "runtime.trust.protocol-invalid");
    }

    private sealed class CorrelatedRunner(bool mismatch = false) : IExternalProcessRunner
    {
        internal int Calls { get; private set; }

        public ValueTask<ExternalProcessResult> RunAsync(ExternalProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            string requestId = mismatch ? Guid.NewGuid().ToString("D") : startInfo.Arguments[^1];
            string sha256 = startInfo.Arguments[^2];
            string json = JsonSerializer.Serialize(new
            {
                ProtocolVersion = 1,
                RequestId = requestId,
                Sha256 = sha256,
                IssueCode = (string?)null,
            });
            return ValueTask.FromResult(new ExternalProcessResult(0, false, json, string.Empty));
        }
    }

    private static string TrustedHostPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "trusted-host.exe");
    }
}

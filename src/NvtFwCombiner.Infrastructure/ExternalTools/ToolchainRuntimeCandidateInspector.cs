using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

internal sealed class ToolchainRuntimeCandidateInspector(
    ILocalFileStore files,
    RuntimeTrustProbeProcess trustProbe) : IToolchainRuntimeCandidateInspector
{
    private const long MaximumRuntimeBytes = 8 * 1024 * 1024;

    public ValueTask<ToolchainRuntimeCandidateInspection> InspectAsync(string path, CancellationToken cancellationToken)
    {
        return InspectCoreAsync(path, requireTrust: true, cancellationToken);
    }

    public async ValueTask<ToolchainRuntimeCandidateInspection> InspectBundledAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ResolvedRuntimeTool> tools = await ExternalProcessorEnvironmentLoader.DiscoverRuntimeToolsAsync(cancellationToken).ConfigureAwait(false);
        string? path = tools.Select(static tool => Path.Combine(Path.GetDirectoryName(tool.Path)!, "vcruntime140.dll"))
            .FirstOrDefault(File.Exists);
        return path is null ? Rejected("runtime.bundled.missing", "The bundled runtime is unavailable.")
            : await InspectCoreAsync(path, requireTrust: false, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<ToolchainRuntimeCandidateInspection>> DetectAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) { return []; }
        string candidate = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "vcruntime140.dll");
        return File.Exists(candidate) ? [await InspectAsync(candidate, cancellationToken).ConfigureAwait(false)] : [];
    }

    private async ValueTask<ToolchainRuntimeCandidateInspection> InspectCoreAsync(
        string path,
        bool requireTrust,
        CancellationToken cancellationToken)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            byte[] runtime = await files.ReadAsync(fullPath, MaximumRuntimeBytes, static async (stream, token) =>
            {
                byte[] bytes = new byte[checked((int)stream.Length)];
                await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
                return bytes;
            }, cancellationToken).ConfigureAwait(false);
            string sha256 = Convert.ToHexStringLower(SHA256.HashData(runtime));
            RuntimeCandidateImageFacts? facts = RuntimeCandidateImageInspector.Inspect(ImmutableArray.Create(runtime)).Facts;
            if (facts is null) { return Rejected("runtime.image.malformed", "The selected runtime is not a valid PE image."); }

            IReadOnlyList<ResolvedRuntimeTool> tools = await ExternalProcessorEnvironmentLoader.DiscoverRuntimeToolsAsync(cancellationToken).ConfigureAwait(false);
            if (tools.Count == 0) { return Rejected("runtime.tool.missing", "The approved Combiner tool is unavailable."); }
            foreach (ResolvedRuntimeTool tool in tools)
            {
                byte[] executable = await files.ReadAsync(tool.Path, MaximumRuntimeBytes, static async (stream, token) =>
                {
                    byte[] bytes = new byte[checked((int)stream.Length)];
                    await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
                    return bytes;
                }, cancellationToken).ConfigureAwait(false);
                string? issue = RuntimeCandidateDependencyInspector.Check(ImmutableArray.Create(executable), ImmutableArray.Create(runtime));
                if (issue is not null) { return Rejected(issue, "The runtime does not satisfy the approved tool imports."); }
            }

            if (requireTrust)
            {
                string root = Path.Combine(Path.GetTempPath(), "nvt-fw-combiner", "runtime-probe");
                string stagingPath = Path.Combine(root, Guid.NewGuid().ToString("N"));
                using ExternalStagingDirectory? staging = ExternalStagingDirectory.TryAcquire(stagingPath);
                if (staging is null) { return Rejected("runtime.trust.staging-failed", "A private trust-probe directory could not be acquired."); }
                string snapshotPath = Path.Combine(stagingPath, "vcruntime140.dll");
                await File.WriteAllBytesAsync(snapshotPath, runtime, cancellationToken).ConfigureAwait(false);
                string? issue = await trustProbe.VerifyAsync(snapshotPath, sha256, cancellationToken).ConfigureAwait(false);
                if (issue is not null) { return Rejected(issue, "Windows could not verify the selected Microsoft runtime."); }
            }

            string version = FileVersionInfo.GetVersionInfo(fullPath).FileVersion ?? "Unknown";
            return new(new(fullPath, sha256, version, facts.Machine.ToString()), ToolchainRuntimeCandidateVerification.Verified, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or CryptographicException)
        {
            return Rejected("runtime.candidate.inspect-failed", exception.Message);
        }
    }

    private static ToolchainRuntimeCandidateInspection Rejected(string code, string message)
    {
        return new(null, ToolchainRuntimeCandidateVerification.Rejected, [new(code, message)]);
    }
}

using System.Security.Cryptography;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

internal sealed class ExternalRuntimeDeployment(
    ToolchainRuntimeConfigurationSnapshot configuration,
    ILocalFileStore files,
    string deploymentRoot)
{
    private const long MaximumFileBytes = 16 * 1024 * 1024;

    internal async ValueTask<ExternalRuntimeDeploymentResult> PrepareAsync(
        string executablePath,
        string executableSha256,
        CancellationToken cancellationToken)
    {
        if (configuration.Status != ToolchainRuntimeConfigurationStatus.Current || configuration.Selection is null)
        {
            return Failed("toolchain-runtime.selection.blocked", "The selected Toolchain runtime is not available.");
        }
        if (configuration.Selection.Source == ToolchainRuntimeSource.Bundled)
        {
            return new(executablePath, null, null);
        }

        ToolchainRuntimeSelection selection = configuration.Selection;
        try
        {
            byte[] executable = await ReadAsync(executablePath, cancellationToken).ConfigureAwait(false);
            if (!HashMatches(executable, executableSha256))
            {
                return Failed("external-tool.hash.mismatch", "The approved external tool changed before deployment.");
            }
            byte[] runtime = await ReadAsync(selection.Path!, cancellationToken).ConfigureAwait(false);
            if (!HashMatches(runtime, selection.Sha256!))
            {
                return Failed("toolchain-runtime.identity.changed", "The selected runtime changed before deployment.");
            }

            string path = Path.Combine(deploymentRoot, Guid.NewGuid().ToString("N"));
            ExternalStagingDirectory? directory = null;
            try
            {
                directory = ExternalStagingDirectory.TryAcquire(path);
                if (directory is null)
                {
                    return Failed("toolchain-runtime.deployment.exists", "A private runtime deployment could not be acquired.");
                }
                string deployedExecutable = Path.Combine(path, Path.GetFileName(executablePath));
                await File.WriteAllBytesAsync(deployedExecutable, executable, cancellationToken).ConfigureAwait(false);
                await File.WriteAllBytesAsync(Path.Combine(path, "vcruntime140.dll"), runtime, cancellationToken).ConfigureAwait(false);
                ExternalRuntimeDeploymentResult result = new(deployedExecutable, directory, null);
                directory = null;
                return result;
            }
            finally
            {
                directory?.Dispose();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Failed("toolchain-runtime.deployment.failed", $"Runtime deployment failed ({exception.GetType().Name}).");
        }
    }

    private async ValueTask<byte[]> ReadAsync(string path, CancellationToken cancellationToken)
    {
        return await files.ReadAsync(path, MaximumFileBytes, static async (stream, token) =>
        {
            byte[] bytes = new byte[checked((int)stream.Length)];
            await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
            return bytes;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static bool HashMatches(byte[] bytes, string expected)
    {
        return string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static ExternalRuntimeDeploymentResult Failed(string code, string message)
    {
        return new(null, null, new CompositionIssue(code, message));
    }
}

internal sealed class ExternalRuntimeDeploymentResult(
    string? executablePath,
    IDisposable? directory,
    CompositionIssue? issue) : IDisposable
{
    internal string? ExecutablePath { get; } = executablePath;
    internal CompositionIssue? Issue { get; } = issue;
    public void Dispose()
    {
        directory?.Dispose();
    }
}

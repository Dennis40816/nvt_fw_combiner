using NvtFwCombiner.Contracts.Configuration;

namespace NvtFwCombiner.Application.Ports;

/// <summary>Strict bounded transport over one host-selected configuration path.</summary>
public interface IToolchainRuntimeConfigurationStorage
{
    /// <summary>Returns null only when configuration is missing; malformed or unreadable data throws.</summary>
    ValueTask<ToolchainRuntimeConfigurationDocument?> ReadAsync(CancellationToken cancellationToken);
    /// <summary>Atomically persists one document; successful return means commit completed.</summary>
    ValueTask WriteAsync(ToolchainRuntimeConfigurationDocument document, CancellationToken cancellationToken);
}

/// <summary>The stored or proposed configuration is not valid strict transport JSON.</summary>
public sealed class ToolchainRuntimeConfigurationFormatException(Exception? innerException = null)
    : IOException("Invalid Toolchain runtime configuration document.", innerException);

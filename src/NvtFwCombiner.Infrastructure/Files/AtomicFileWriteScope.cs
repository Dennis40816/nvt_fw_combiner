namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>One verified parent-directory authority held through atomic promotion.</summary>
internal interface IAtomicFileWriteScope : IDisposable
{
    ValueTask WriteAsync(
        ReadOnlyMemory<byte> documentBytes,
        CancellationToken cancellationToken);
}

internal static class AtomicFileWriteScope
{
    internal static IAtomicFileWriteScope Open(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        return OperatingSystem.IsWindows()
            ? WindowsAtomicFileWriteScope.Open(destinationPath)
            : OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()
                ? UnixAtomicFileWriteScope.Open(destinationPath)
                : throw new PlatformNotSupportedException(
                    "Saved Rule atomic writes require Windows, Linux, or macOS filesystem primitives.");
    }
}

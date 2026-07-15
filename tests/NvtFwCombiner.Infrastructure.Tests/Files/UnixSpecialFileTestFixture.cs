using System.Runtime.InteropServices;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

internal static partial class UnixSpecialFileTestFixture
{
    public static bool IsUnix { get; } = !OperatingSystem.IsWindows();

    internal static void CreateFifo(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!IsUnix)
        {
            throw new PlatformNotSupportedException("FIFO fixtures require a Unix platform.");
        }

        const uint ownerReadWrite = 0x180;
        if (UnixMkFifo(path, ownerReadWrite) != 0)
        {
            throw new IOException(
                $"Could not create FIFO test fixture (native error {Marshal.GetLastPInvokeError()}).");
        }
    }

    [LibraryImport(
        "System.Native",
        EntryPoint = "SystemNative_MkFifo",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true)]
    private static partial int UnixMkFifo(string path, uint mode);
}

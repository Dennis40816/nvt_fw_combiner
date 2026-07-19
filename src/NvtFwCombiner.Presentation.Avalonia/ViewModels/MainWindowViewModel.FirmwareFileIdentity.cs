namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly record struct FirmwareFileIdentity(
        bool Exists,
        long Length,
        DateTime LastWriteTimeUtc)
    {
        internal static FirmwareFileIdentity Capture(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return default;
            }

            try
            {
                var file = new FileInfo(path);
                return file.Exists
                    ? new FirmwareFileIdentity(true, file.Length, file.LastWriteTimeUtc)
                    : default;
            }
            catch (Exception exception) when (exception is
                IOException or
                UnauthorizedAccessException or
                ArgumentException or
                NotSupportedException)
            {
                return default;
            }
        }
    }
}

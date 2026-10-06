using Avalonia;
using Avalonia.Media.Imaging;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>An expected capture failure, retaining its terminal phase.</summary>
internal sealed class DesktopCaptureFailureException : InvalidOperationException
{
    internal DesktopCaptureFailureException(string phase, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Phase = phase;
    }

    internal string Phase { get; }
}

/// <summary>Production pixel rendering and atomic PNG publication; never interprets report contents.</summary>
internal class DesktopScreenshotCapture
{
    internal static string DescribeProtectedInputs(IReadOnlyList<string> inputs)
    {
        return inputs.Count == 0 ? string.Empty : $" (protected inputs: {string.Join(", ", inputs.Select(input => $"'{input}'"))})";
    }

    internal static async ValueTask<string> ValidateDestinationAsync(
        ILocalFileStore files,
        string destination,
        IReadOnlyList<string> protectedInputs,
        bool overwrite,
        string? localStateDirectory = null,
        CancellationToken cancellationToken = default)
    {
        string path = Path.GetFullPath(destination);
        string name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !name.EndsWith(UiLaunchOptions.CaptureExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("--capture requires a valid .png filename.", nameof(destination));
        }
        LocalFileDestinationInfo destinationInfo = await files.InspectDestinationAsync(path, cancellationToken).ConfigureAwait(false);
        if (!destinationInfo.ParentExists)
        {
            throw new ArgumentException("--capture parent folder must already exist.", nameof(destination));
        }
        if (!destinationInfo.ParentHasExactPath)
        {
            throw new ArgumentException("--capture destination folder must be given by its real path.", nameof(destination));
        }
        if (destinationInfo.IsDirectory)
        {
            throw new ArgumentException("--capture destination is a directory.", nameof(destination));
        }
        foreach (string input in protectedInputs)
        {
            if (await PathsAliasAsync(files, path, input, cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException($"--capture destination aliases protected input '{input}'{DescribeProtectedInputs(protectedInputs)}.", nameof(destination));
            }
        }
        if (localStateDirectory is not null)
        {
            await RefuseLocalStateAliasesAsync(files, path, localStateDirectory, cancellationToken).ConfigureAwait(false);
        }
        return !overwrite && destinationInfo.Exists
            ? throw new ArgumentException("--capture destination exists; use --overwrite-capture.", nameof(destination))
            : path;
    }

    internal static async ValueTask RefuseLocalStateAliasesAsync(
        ILocalFileStore files,
        string destination,
        string localStateDirectory,
        CancellationToken cancellationToken)
    {
        string[] protectedPaths = [ReportHistoryFileStore.PathIn(localStateDirectory), ShellPreferenceFileStore.PathIn(localStateDirectory)];
        foreach (string path in protectedPaths)
        {
            if (await PathsAliasAsync(files, destination, path, cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException($"--capture destination aliases local state '{path}'.", nameof(destination));
            }
        }
    }

    private static async ValueTask<bool> PathsAliasAsync(
        ILocalFileStore files,
        string first,
        string second,
        CancellationToken cancellationToken)
    {
        // Protect the same lexical destination even before the report/state file exists.
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), comparison) ||
            await files.RefersToSameFileAsync(first, second, cancellationToken).ConfigureAwait(false);
    }

    internal async Task CaptureAsync(
        ILocalFileStore files,
        string localStateDirectory,
        MainWindow window,
        string destination,
        IReadOnlyList<string> protectedInputs,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using RenderTargetBitmap bitmap = Render(window);
        await SaveAsync(files, localStateDirectory, bitmap, destination, protectedInputs, overwrite, cancellationToken);
    }

    internal virtual RenderTargetBitmap Render(MainWindow window)
    {
        try
        {
            PixelSize size = PixelSize.FromSize(window.ClientSize, window.RenderScaling);
            if (size.Width <= 0 || size.Height <= 0)
            {
                throw new InvalidOperationException("The requested client surface has no pixels.");
            }
            var bitmap = new RenderTargetBitmap(size, new Vector(96 * window.RenderScaling, 96 * window.RenderScaling));
            try
            {
                bitmap.Render(window);
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }
        catch (Exception exception) when (IsExpectedPixelFailure(exception))
        {
            throw new DesktopCaptureFailureException("draw", exception.Message, exception);
        }
    }

    internal async Task SaveAsync(
        ILocalFileStore files,
        string localStateDirectory,
        Bitmap bitmap,
        string destination,
        IReadOnlyList<string> protectedInputs,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        string phase = "validation";
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            destination = await ValidateDestinationAsync(files, destination, protectedInputs, overwrite: true,
                localStateDirectory, cancellationToken);
            phase = "encode";
            using var encoded = new MemoryStream();
            Encode(bitmap, encoded);
            cancellationToken.ThrowIfCancellationRequested();
            phase = "validation";
            // Recheck physical identity immediately before publication; no-replace is enforced by the OS.
            _ = await ValidateDestinationAsync(files, destination, protectedInputs, overwrite: true,
                localStateDirectory, cancellationToken);
            phase = "publish";
            await PublishAsync(files, destination, encoded.ToArray(), overwrite, cancellationToken);
        }
        catch (Exception exception) when (IsExpectedPixelFailure(exception) &&
            (exception is not ArgumentException || phase != "validation"))
        {
            throw new DesktopCaptureFailureException(phase == "encode" && exception is IOException ? "write" : phase,
                exception.Message, exception);
        }
    }

    internal virtual void Encode(Bitmap bitmap, Stream stream)
    {
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
    }

    internal virtual ValueTask PublishAsync(
        ILocalFileStore files,
        string destination,
        ReadOnlyMemory<byte> bytes,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        return files.WriteAsync(destination, bytes,
            new LocalFileWriteOptions(overwrite ? LocalFileWriteMode.ReplaceExisting : LocalFileWriteMode.CreateNew,
                RequireExistingParent: true), cancellationToken);
    }

    private static bool IsExpectedPixelFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or InvalidOperationException or
            ArgumentException or NotSupportedException;
    }
}

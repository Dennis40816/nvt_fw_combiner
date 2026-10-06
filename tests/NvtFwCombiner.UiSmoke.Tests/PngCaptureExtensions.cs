using Avalonia.Media.Imaging;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Saves test captures as PNG through the Avalonia encoder-options overload.</summary>
/// <remarks>
/// The default PNG options produce the same bytes as the obsolete <c>Save(string, int?)</c> and
/// <c>Save(Stream, int?)</c> overloads, which use these options in Avalonia 12.1.
/// </remarks>
internal static class PngCaptureExtensions
{
    internal static void SavePng(this Bitmap bitmap, string fileName)
    {
        bitmap.Save(fileName, PngBitmapEncoderOptions.Default);
    }

    internal static void SavePng(this Bitmap bitmap, Stream stream)
    {
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
    }
}

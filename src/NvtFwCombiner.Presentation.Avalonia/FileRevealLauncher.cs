using System.ComponentModel;
using System.Diagnostics;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Opens Windows Explorer with one existing BIN selected.</summary>
internal static class FileRevealLauncher
{
    internal static ProcessStartInfo? TryCreateStartInfo(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !Path.IsPathFullyQualified(filePath))
        {
            return null;
        }

        try
        {
            var file = new FileInfo(filePath);
            if (!file.Exists || file.DirectoryName is null)
            {
                return null;
            }

            var startInfo = new ProcessStartInfo("explorer.exe")
            {
                UseShellExecute = false,
                WorkingDirectory = file.DirectoryName,
            };
            startInfo.ArgumentList.Add("/select,");
            startInfo.ArgumentList.Add(file.FullName);
            return startInfo;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    internal static bool TryReveal(string? filePath)
    {
        if (!OperatingSystem.IsWindows() || TryCreateStartInfo(filePath) is not { } startInfo)
        {
            return false;
        }

        try
        {
            using var process = Process.Start(startInfo);
            return process is not null;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

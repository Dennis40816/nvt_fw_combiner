using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.Shell;

/// <summary>Reveals an existing file through the trusted Windows Explorer executable.</summary>
internal sealed class WindowsExplorerFileRevealService : IFileRevealService
{
    /// <inheritdoc />
    public bool TryRevealFile(string? filePath)
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

    internal static ProcessStartInfo? TryCreateStartInfo(
        string? filePath,
        string? windowsDirectory = null)
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

            string windowsRoot = windowsDirectory ?? Environment.GetFolderPath(
                Environment.SpecialFolder.Windows,
                Environment.SpecialFolderOption.DoNotVerify);
            if (string.IsNullOrWhiteSpace(windowsRoot) || !Path.IsPathFullyQualified(windowsRoot))
            {
                return null;
            }

            string explorerPath = Path.GetFullPath(Path.Combine(windowsRoot, "explorer.exe"));
            if (!File.Exists(explorerPath))
            {
                return null;
            }

            var startInfo = new ProcessStartInfo(explorerPath)
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
        catch (SecurityException)
        {
            return null;
        }
    }
}

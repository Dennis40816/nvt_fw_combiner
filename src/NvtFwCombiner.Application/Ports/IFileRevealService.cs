namespace NvtFwCombiner.Application.Ports;

/// <summary>Reveals one existing local file in the host file manager.</summary>
public interface IFileRevealService
{
    /// <summary>Attempts to show the exact file without interpreting a shell command string.</summary>
    bool TryRevealFile(string? filePath);
}

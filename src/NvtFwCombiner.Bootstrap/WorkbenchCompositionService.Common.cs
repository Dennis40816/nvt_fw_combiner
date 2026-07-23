using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static (string Directory, string FileName) ResolveOutputTarget(
        string firstInputPath,
        bool build,
        string? outputPath,
        string defaultOutputFileName,
        string? automaticOutputDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            string automaticDirectory = string.IsNullOrWhiteSpace(automaticOutputDirectory)
                ? Path.GetDirectoryName(firstInputPath)!
                : Path.GetFullPath(automaticOutputDirectory);
            return (automaticDirectory, defaultOutputFileName);
        }

        if (!build)
        {
            throw new ArgumentException("Preview does not accept an output file path.", nameof(outputPath));
        }

        string fullPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        string fileName = Path.GetFileName(fullPath);
        return string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName)
            ? throw new ArgumentException("Output path must include a directory and file name.", nameof(outputPath))
            : (directory, fileName);
    }

    private static IcNumberSelection ToIcNumberSelection(string number)
    {
        return WorkbenchIcNumberSelections.FromNumberToken(number);
    }

    internal static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}"));
    }
}

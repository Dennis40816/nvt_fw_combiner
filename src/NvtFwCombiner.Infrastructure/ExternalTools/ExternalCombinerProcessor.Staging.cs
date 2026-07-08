using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

public sealed partial class ExternalCombinerProcessor
{
    private static IReadOnlyList<string> ExpandArguments(
        IEnumerable<string> arguments,
        string workBin,
        string outputBin,
        string runDirectory)
    {
        return [
            .. arguments.Select(argument => argument
                .Replace("{staging.workBin}", workBin, StringComparison.Ordinal)
                .Replace("{staging.outputBin}", outputBin, StringComparison.Ordinal)
                .Replace("{staging.runDir}", runDirectory, StringComparison.Ordinal)),
        ];
    }

    private static CompositionIssue? FindUnexpectedStagingFileIssue(
        string runDirectory,
        ExternalCombinerToolManifest manifest)
    {
        HashSet<string> allowed = new(StringComparer.OrdinalIgnoreCase)
        {
            WorkFileName,
        };
        if (string.Equals(manifest.InputMode, "input-output-file", StringComparison.Ordinal))
        {
            _ = allowed.Add(OutputFileName);
        }

        foreach (string name in manifest.AllowedExtraOutputFiles)
        {
            _ = allowed.Add(name);
        }

        foreach (string entry in Directory.EnumerateFileSystemEntries(runDirectory))
        {
            string name = Path.GetFileName(entry);
            if (!allowed.Contains(name) || Directory.Exists(entry))
            {
                return new CompositionIssue(
                    "external-tool.unexpected-output-file",
                    $"External processor produced unexpected staging entry '{name}'.");
            }
        }

        return null;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

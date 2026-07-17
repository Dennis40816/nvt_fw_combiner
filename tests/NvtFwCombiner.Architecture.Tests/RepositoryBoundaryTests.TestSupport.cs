using System.Text.RegularExpressions;

namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    private static string[] ReadStandardMergeIcIds()
    {
        string source = ReadText("src/NvtFwCombiner.Bootstrap/BuiltInV2RegistrationRegistry.cs");
        return
        [
            .. StandardMergeProfileRegex().Matches(source)
                .Cast<Match>()
                .Select(match => $"NT{match.Groups["ic"].Value}")
                .Order(StringComparer.Ordinal),
        ];
    }

    private static string[] ReadCtrlRamPostbuildIcIds()
    {
        string source = ReadPostbuildCatalogPartials();
        return
        [
            .. CtrlRamPostbuildProfileRegex().Matches(source)
                .Cast<Match>()
                .Select(match => $"NT{match.Groups["ic"].Value}")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    [GeneratedRegex(@"new BuiltInV2StandardMergeRegistration\(\s*""NT(?<ic>\d{5})""")]
    private static partial Regex StandardMergeProfileRegex();

    [GeneratedRegex(@"""icId""\s*:\s*""NT(?<ic>\d{5})""")]
    private static partial Regex CtrlRamPostbuildProfileRegex();

    private static string ReadText(string relativePath)
    {
        string fullPath = Path.Combine(Root.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(fullPath);
    }

    private static string ReadViewModelPartials()
    {
        string directory = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Presentation.Avalonia",
            "ViewModels");
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(directory, "MainWindowViewModel*.cs")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string ReadPresentationSources()
    {
        string directory = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Presentation.Avalonia");
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) ||
                               path.EndsWith(".axaml", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string ReadBootstrapSources()
    {
        string directory = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap");
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string ReadProfileSources()
    {
        string directory = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles");
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string ReadDomainSources()
    {
        string directory = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Domain");
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string[] ReadMarkdownBullets(string relativePath, string heading)
    {
        string[] lines = ReadLines(relativePath);
        int start = Array.FindIndex(lines, line => string.Equals(line.Trim(), heading, StringComparison.Ordinal));
        if (start < 0)
        {
            throw new InvalidOperationException($"Could not find heading '{heading}' in {relativePath}.");
        }

        int end = Array.FindIndex(lines, start + 1, line => line.StartsWith("## ", StringComparison.Ordinal));
        if (end < 0)
        {
            end = lines.Length;
        }

        return
        [
            .. lines[(start + 1)..end]
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
            .Select(line => line[2..])
        ];
    }

    private static string[] ReadPlanningResourceRows(string title)
    {
        string resources = ReadShellTextResourcesPartials();
        int titleIndex = resources.IndexOf($"\"{title}\",", StringComparison.Ordinal);
        if (titleIndex < 0)
        {
            throw new InvalidOperationException($"Could not find planning card '{title}'.");
        }

        int rowsStart = resources.IndexOf('[', titleIndex);
        int rowsEnd = rowsStart >= 0 ? resources.IndexOf(']', rowsStart) : -1;
        return rowsStart < 0 || rowsEnd < 0
            ? throw new InvalidOperationException($"Could not find rows for planning card '{title}'.")
            :
            [
                .. resources[(rowsStart + 1)..rowsEnd]
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith('"'))
            .Select(line => line.TrimEnd(',').Trim('"'))
            ];
    }

    private static string ReadShellTextResourcesPartials()
    {
        string directory = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Presentation.Avalonia",
            "ViewModels");
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(directory, "ShellTextResources*.cs")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string ReadPostbuildCatalogPartials()
    {
        return ReadText("profiles/built-in/ctrlram-postbuild-v2/catalog.json");
    }

    private static string ReadFlashMapCatalogPartials()
    {
        string directory = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Application",
            "FlashMaps");
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(directory, "TpFlashMapCatalog*.cs")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static (int Line, string[] Cells) FindMarkdownTableRow(string relativePath, string firstCell)
    {
        string[] lines = ReadLines(relativePath);
        for (int i = 0; i < lines.Length; i++)
        {
            if (!IsMarkdownTableLine(lines[i]))
            {
                continue;
            }

            string[] cells = SplitMarkdownTableRow(lines[i]);
            if (cells.Length > 0 && string.Equals(cells[0], firstCell, StringComparison.Ordinal))
            {
                return (i, cells);
            }
        }

        throw new InvalidOperationException($"Could not find markdown row starting with '{firstCell}'.");
    }

    private static string[] ReadLines(string relativePath)
    {
        string fullPath = Path.Combine(Root.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllLines(fullPath);
    }

    private static bool IsMarkdownTableLine(string line)
    {
        string trimmed = line.Trim();
        return trimmed.StartsWith('|') && trimmed.EndsWith('|');
    }

    private static string[] SplitMarkdownTableRow(string line)
    {
        return line.Trim().Trim('|').Split('|', StringSplitOptions.TrimEntries);
    }

    private static DirectoryInfo LocateRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NvtFwCombiner.slnx")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}

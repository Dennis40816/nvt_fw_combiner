namespace NvtFwCombiner.Architecture.Tests;

/// <summary>Repository-level architecture boundary checks that do not depend on production assemblies.</summary>
public sealed class RepositoryBoundaryTests
{
    private static readonly DirectoryInfo Root = LocateRepositoryRoot();

    /// <summary>Verifies architecture tests do not introduce production project references.</summary>
    [Fact]
    public void ArchitectureTestsRemainDependencyFree()
    {
        string project = ReadText("tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj");

        Assert.DoesNotContain("ProjectReference", project, StringComparison.Ordinal);
    }

    /// <summary>Verifies external combiner versions are documented as exact string tokens.</summary>
    [Fact]
    public void ExternalCombinerVersionsAreDocumentedAsStringTokens()
    {
        string adr = ReadText("docs/adr/0006-external-combiner-tool-runner.md");

        Assert.Contains("`toolVersion` is always a string", adr, StringComparison.Ordinal);
        Assert.Contains("`1.10` and `1.9` are exact version tokens", adr, StringComparison.Ordinal);
    }

    /// <summary>Verifies UI planning documents keep firmware behavior out of ViewModels.</summary>
    [Fact]
    public void DemoUiDocumentsForbidFirmwareSemanticsInViewModels()
    {
        string boundaries = ReadText("docs/ui/viewmodel-boundaries.md");

        Assert.Contains("byte range arithmetic", boundaries, StringComparison.Ordinal);
        Assert.Contains("CRC/Header calculation or `combiner.exe` invocation", boundaries, StringComparison.Ordinal);
        Assert.Contains("No `File.ReadAllBytes` or `Process.Start` in ViewModels", boundaries, StringComparison.Ordinal);
    }

    /// <summary>Verifies the demo shell follows the owner-approved clean home and independent page direction.</summary>
    [Fact]
    public void DemoShellUsesCleanHomeAndIndependentWorkflowPages()
    {
        string shell = ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml");

        Assert.Contains("IsHomeVisible", shell, StringComparison.Ordinal);
        Assert.Contains("IsMergeVisible", shell, StringComparison.Ordinal);
        Assert.Contains("IsReplaceVisible", shell, StringComparison.Ordinal);
        Assert.Contains("ShowDpReplaceCommand", shell, StringComparison.Ordinal);
        Assert.Contains("ShowNormalMergeCommand", shell, StringComparison.Ordinal);
        Assert.Contains("RowDefinitions=\"Auto,Auto,*,Auto\"", shell, StringComparison.Ordinal);
        Assert.Contains("DeviceContextTitle", shell, StringComparison.Ordinal);
        Assert.Contains("SelectedIcLabel", shell, StringComparison.Ordinal);
        Assert.Contains("IcNumberText", shell, StringComparison.Ordinal);
        Assert.Contains("SelectedIcNumberMode", shell, StringComparison.Ordinal);
        Assert.Contains("ToggleButton", shell, StringComparison.Ordinal);
        Assert.Contains("Classes=\"nav\"", shell, StringComparison.Ordinal);
        Assert.Contains("Classes=\"segment\"", shell, StringComparison.Ordinal);
        Assert.Contains("Classes=\"command\"", shell, StringComparison.Ordinal);
        Assert.Contains("Classes=\"action\"", shell, StringComparison.Ordinal);
        Assert.Contains("Classes=\"segmentDisabled\"", shell, StringComparison.Ordinal);
        Assert.Contains("INSPECTOR", shell, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"1.6*,360\"", shell, StringComparison.Ordinal);
        Assert.Contains("Content=\"AB Code\"", shell, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"False\"", shell, StringComparison.Ordinal);
        Assert.Contains("FontFamily=\"fonts:Inter#Inter, Microsoft JhengHei UI, Noto Sans CJK TC, Noto Sans TC, Segoe UI\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Classes=\"secondary\" Content=\"{Binding PreviewActionLabel}\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"#0F172A\" CornerRadius=\"8\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Merge / Replace workspace", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("ColumnDefinitions=\"220,*\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Diagnostics.", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("SavedRulesAndReports", shell, StringComparison.Ordinal);
    }

    /// <summary>Verifies demo-shell copy is routed through bilingual text resources.</summary>
    [Fact]
    public void DemoShellUsesBilingualTextResources()
    {
        string resources = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/DemoShellTextResources.cs");
        string sampleData = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/DemoShellSampleData.cs");

        Assert.Contains("DemoShellLanguage.ChineseTraditional", resources, StringComparison.Ordinal);
        Assert.Contains("合併", resources, StringComparison.Ordinal);
        Assert.Contains("Device context", resources, StringComparison.Ordinal);
        Assert.Contains("DemoShellTextResources.For(language)", sampleData, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Merge preview\"", sampleData, StringComparison.Ordinal);
        Assert.DoesNotContain("Saved rules", resources, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies the owner-priority roadmap schedules normal Replace before deferred AB work.</summary>
    [Fact]
    public void OwnerPriorityTargetsNormalMergeReplaceBeforeAb()
    {
        (int replaceLine, string[] replaceMilestone) = FindMarkdownTableRow(
            "docs/governance/development-tags.md",
            "`0.5.0-dev.N`");
        (int abLine, string[] abMilestone) = FindMarkdownTableRow(
            "docs/governance/development-tags.md",
            "`0.6.0-dev.N`");

        Assert.True(replaceLine < abLine, "Normal Replace must be scheduled before deferred AB work.");
        Assert.Equal("Normal Replace priority", replaceMilestone[1]);
        Assert.Contains("DP", replaceMilestone[2], StringComparison.Ordinal);
        Assert.Contains("CtrlRAM", replaceMilestone[2], StringComparison.Ordinal);
        Assert.Contains("IC num", replaceMilestone[2], StringComparison.Ordinal);
        Assert.Contains("combiner", replaceMilestone[2], StringComparison.Ordinal);
        Assert.Equal("AB merge", abMilestone[1]);
        Assert.Contains("deferred", abMilestone[2], StringComparison.OrdinalIgnoreCase);

        foreach (string ic in new[] { "NT51950", "NT51951" })
        {
            string[] row = FindMarkdownTableRow("docs/architecture/supported-ic-matrix.md", ic).Cells;

            Assert.Contains("normal merge requested", row[1], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("memory map pending", row[1], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DP and CtrlRAM priority", row[3], StringComparison.Ordinal);
            Assert.Contains("AB:", row[4], StringComparison.Ordinal);
            Assert.Contains("Replace:", row[4], StringComparison.Ordinal);
            Assert.Contains("AB", row[5], StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies Replace planning exposes IC number and post-replace combiner readiness in the right surfaces.</summary>
    [Fact]
    public void ReplacePlanningRequiresIcNumAndCombinerPostProcessing()
    {
        string[] replaceBullets = ReadMarkdownBullets(
            "docs/ui/0.1.1-demo-interface-plan.md",
            "## Replace demo content");
        Assert.True(
            replaceBullets.Any(bullet => bullet.StartsWith("Shared IC num selector/input", StringComparison.Ordinal)),
            "Replace demo content must use the shared IC num context before region choices.");
        Assert.Contains(
            replaceBullets,
            bullet => bullet.Contains("single", StringComparison.Ordinal)
                && bullet.Contains("cascade", StringComparison.Ordinal)
                && bullet.Contains("numeric", StringComparison.Ordinal));

        string readinessBullet = Assert.Single(
            replaceBullets,
            bullet => bullet.StartsWith("Processor/tool readiness indicator", StringComparison.Ordinal));
        Assert.Contains("combiner.exe", readinessBullet, StringComparison.Ordinal);
        Assert.Contains("CRC/header", readinessBullet, StringComparison.Ordinal);

        string[] replaceRows = ReadPlanningResourceRows("Replace");
        Assert.Contains(
            replaceRows,
            row => row.StartsWith("Device context:", StringComparison.Ordinal)
                   && row.Contains("IC Num", StringComparison.Ordinal));
        Assert.Contains(
            replaceRows,
            row => row.StartsWith("CRC/header", StringComparison.Ordinal)
                   && row.Contains("combiner.exe", StringComparison.Ordinal));

        string[] row = FindMarkdownTableRow(
            "docs/architecture/integrity-processing-matrix.md",
            "Replace DP/CtrlRAM priority flows").Cells;

        Assert.Contains("post-replace", row[1], StringComparison.Ordinal);
        Assert.Contains("combiner.exe", row[2], StringComparison.Ordinal);
        Assert.Contains("932 common FW postbuild", row[3], StringComparison.Ordinal);
        Assert.DoesNotContain("TPB", string.Join(' ', row), StringComparison.Ordinal);
    }

    private static string ReadText(string relativePath)
    {
        string fullPath = Path.Combine(Root.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(fullPath);
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
        string resources = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/DemoShellTextResources.cs");
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

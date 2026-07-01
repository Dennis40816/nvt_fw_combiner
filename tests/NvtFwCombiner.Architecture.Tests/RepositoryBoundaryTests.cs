using System.Text.RegularExpressions;

namespace NvtFwCombiner.Architecture.Tests;

/// <summary>Repository-level architecture boundary checks that do not depend on production assemblies.</summary>
public sealed partial class RepositoryBoundaryTests
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
    public void UiDocumentsForbidFirmwareSemanticsInViewModels()
    {
        string boundaries = ReadText("docs/ui/viewmodel-boundaries.md");

        Assert.Contains("byte range arithmetic", boundaries, StringComparison.Ordinal);
        Assert.Contains("CRC/Header calculation or `combiner.exe` invocation", boundaries, StringComparison.Ordinal);
        Assert.Contains("No `File.ReadAllBytes` or `Process.Start` in ViewModels", boundaries, StringComparison.Ordinal);
    }

    /// <summary>Verifies the shell follows the owner-approved clean home and independent page direction.</summary>
    [Fact]
    public void ShellUsesCleanHomeAndIndependentWorkflowPages()
    {
        string shell = ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml");

        Assert.Contains("IsHomeVisible", shell, StringComparison.Ordinal);
        Assert.Contains("IsMergeVisible", shell, StringComparison.Ordinal);
        Assert.Contains("IsReplaceVisible", shell, StringComparison.Ordinal);
        Assert.Contains("ShowDpReplaceCommand", shell, StringComparison.Ordinal);
        Assert.Contains("ShowNormalMergeCommand", shell, StringComparison.Ordinal);
        Assert.Contains("RowDefinitions=\"Auto,Auto,*,Auto\"", shell, StringComparison.Ordinal);
        Assert.Contains("DeviceContextTitle", shell, StringComparison.Ordinal);
        Assert.Contains("IcChoices", shell, StringComparison.Ordinal);
        Assert.Contains("SelectedIc", shell, StringComparison.Ordinal);
        Assert.Contains("NumberChoices", shell, StringComparison.Ordinal);
        Assert.Contains("SelectedNumber", shell, StringComparison.Ordinal);
        Assert.Contains("ToggleButton", shell, StringComparison.Ordinal);
        Assert.Contains("Classes=\"nav\"", shell, StringComparison.Ordinal);
        Assert.Contains("Classes=\"command\"", shell, StringComparison.Ordinal);
        Assert.Contains("Classes=\"primary\"", shell, StringComparison.Ordinal);
        Assert.Contains("Classes=\"action\"", shell, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"MinHeight\" Value=\"44\" />", shell, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"CornerRadius\" Value=\"999\" />", shell, StringComparison.Ordinal);
        Assert.Contains("INSPECTOR", shell, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"1.6*,360\"", shell, StringComparison.Ordinal);
        Assert.Contains("AB disabled", shell, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"False\"", shell, StringComparison.Ordinal);
        Assert.Contains("LoadReportJsonButton_OnClick", shell, StringComparison.Ordinal);
        Assert.Contains("SlotDrop_OnDrop", shell, StringComparison.Ordinal);
        Assert.Contains("SlotDragOver_OnDragOver", shell, StringComparison.Ordinal);
        Assert.Contains("BrowseSlotButton_OnClick", shell, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ReplaceModeChoices}\"", shell, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedReplaceMode, Mode=TwoWay}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ReplaceSlots}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding MergeSlots}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding CtrlRamRegions}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ActiveReplaceRows}\"", shell, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PreviewMergeCommand}\"", shell, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding BuildMergeCommand}\"", shell, StringComparison.Ordinal);
        Assert.Contains("HasLoadedReport", shell, StringComparison.Ordinal);
        Assert.Contains("LoadedReport.Operations", shell, StringComparison.Ordinal);
        Assert.Contains("FontFamily=\"fonts:Inter#Inter, Microsoft JhengHei UI, Noto Sans CJK TC, Noto Sans TC, Segoe UI\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Classes=\"secondary\" Content=\"{Binding PreviewActionLabel}\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"#0F172A\" CornerRadius=\"8\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Merge / Replace workspace", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("ColumnDefinitions=\"220,*\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Diagnostics.", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("SavedRulesAndReports", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Policy display only", shell, StringComparison.Ordinal);

        string viewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.cs");
        Assert.Contains("LoadReportJson", viewModel, StringComparison.Ordinal);
        Assert.Contains("ReportReviewViewModel", viewModel, StringComparison.Ordinal);
        Assert.Contains("UiCompositionRunner.GetNumberChoices", viewModel, StringComparison.Ordinal);
        Assert.Contains("ReplaceModeChoices", viewModel, StringComparison.Ordinal);
        Assert.Contains("PreviewMergeCommand", viewModel, StringComparison.Ordinal);
        Assert.Contains("BuildMergeCommand", viewModel, StringComparison.Ordinal);

        string flashMapCatalog = ReadText(
            "src/NvtFwCombiner.Application/FlashMaps/TpFlashMapCatalog.cs");
        Assert.Contains("NF CtrlRAM", flashMapCatalog, StringComparison.Ordinal);
        Assert.Contains("Normal CtrlRAM", flashMapCatalog, StringComparison.Ordinal);
        Assert.Contains("DIFF CtrlRAM", flashMapCatalog, StringComparison.Ordinal);
        Assert.Contains("Vector CtrlRAM", flashMapCatalog, StringComparison.Ordinal);
        Assert.Contains("NT51917", flashMapCatalog, StringComparison.Ordinal);
        Assert.DoesNotContain("CtrlRamRegionCatalog", shell, StringComparison.Ordinal);
    }

    /// <summary>Verifies shell copy is routed through bilingual text resources.</summary>
    [Fact]
    public void ShellUsesBilingualTextResources()
    {
        string resources = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ShellTextResources.cs");
        string factory = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ShellViewModelFactory.cs");

        Assert.Contains("ShellLanguage.ChineseTraditional", resources, StringComparison.Ordinal);
        Assert.Contains("合併", resources, StringComparison.Ordinal);
        Assert.Contains("Device context", resources, StringComparison.Ordinal);
        Assert.Contains("ShellTextResources.For(language)", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Merge preview\"", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("Saved rules", resources, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("demo", resources, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("synthetic", resources, StringComparison.OrdinalIgnoreCase);
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
            Assert.Contains("DP Perspective", row[1], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DP and CtrlRAM priority", row[3], StringComparison.Ordinal);
            Assert.Contains("DP", row[4], StringComparison.Ordinal);
            Assert.Contains("golden", row[5], StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Verifies the per-IC flowchart reference stays synchronized with built-in IC lists.</summary>
    [Fact]
    public void IcWorkflowFlowchartReferenceCoversBuiltInIcLists()
    {
        string reference = ReadText("docs/architecture/ic-workflow-flowcharts.md");
        string[] builtInIcIds =
        [
            .. ReadStandardMergeIcIds()
                .Concat(ReadCtrlRamPostbuildIcIds())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Contains("## Update rule", reference, StringComparison.Ordinal);
        Assert.Contains("IcWorkflowFlowchartReferenceCoversBuiltInIcLists", reference, StringComparison.Ordinal);
        Assert.Contains("NT51928 NB is not covered", reference, StringComparison.Ordinal);
        Assert.Contains("[0x37000, 0x38000)", reference, StringComparison.Ordinal);
        Assert.Contains("R-CTRLRAM-927", reference, StringComparison.Ordinal);

        foreach (string icId in builtInIcIds)
        {
            Assert.Contains($"| {icId} |", reference, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies Replace planning exposes IC number and post-replace combiner readiness in the right surfaces.</summary>
    [Fact]
    public void ReplacePlanningRequiresIcNumAndCombinerPostProcessing()
    {
        string[] replaceBullets = ReadMarkdownBullets(
            "docs/ui/0.1.1-demo-interface-plan.md",
            "## Replace content");
        Assert.True(
            replaceBullets.Any(bullet => bullet.StartsWith("Shared Number selector", StringComparison.Ordinal)),
            "Replace content must use the shared Number context before region choices.");
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
                   && row.Contains("Number", StringComparison.Ordinal));
        Assert.Contains(
            replaceRows,
            row => row.StartsWith("CRC/header", StringComparison.Ordinal)
                   && row.Contains("combiner.exe", StringComparison.Ordinal));

        string[] row = FindMarkdownTableRow(
            "docs/architecture/integrity-processing-matrix.md",
            "CtrlRAM Replace priority flows").Cells;

        Assert.Contains("post-replace", row[1], StringComparison.Ordinal);
        Assert.Contains("combiner.exe", row[2], StringComparison.Ordinal);
        Assert.Contains("Combiner 1.13.0", row[3], StringComparison.Ordinal);
        Assert.Contains("NT51927", row[3], StringComparison.Ordinal);
        Assert.DoesNotContain("TPB", string.Join(' ', row), StringComparison.Ordinal);
    }

    private static string[] ReadStandardMergeIcIds()
    {
        string source = ReadText("src/NvtFwCombiner.Profiles/BuiltInStandardMergeProfiles.cs");
        return
        [
            .. StandardMergeProfileRegex().Matches(source)
                .Cast<Match>()
                .Select(match => $"NT{match.Groups["ic"].Value}")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    private static string[] ReadCtrlRamPostbuildIcIds()
    {
        string source = ReadText("src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCatalog.cs");
        return
        [
            .. CtrlRamPostbuildProfileRegex().Matches(source)
                .Cast<Match>()
                .Select(match => $"NT{match.Groups["ic"].Value}")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    [GeneratedRegex(@"CreateGenFlashProfile\(\s*""(?<ic>\d{5})""")]
    private static partial Regex StandardMergeProfileRegex();

    [GeneratedRegex(@"public static LegacyCombinerPostbuildProfile Nt(?<ic>\d{5})\s*\{")]
    private static partial Regex CtrlRamPostbuildProfileRegex();

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
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ShellTextResources.cs");
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

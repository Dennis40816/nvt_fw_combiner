namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Navigation state is one typed child instead of another MainWindow partial concern.</summary>
    [Fact]
    public void ShellNavigationOrchestrationUsesFocusedTypedChild()
    {
        string mainNavigation = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Navigation.cs");
        string childPath = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Presentation.Avalonia",
            "ViewModels",
            "ShellNavigationViewModel.cs");

        Assert.True(File.Exists(childPath));
        string child = File.ReadAllText(childPath);
        Assert.Contains("internal sealed partial class ShellNavigationViewModel", child, StringComparison.Ordinal);
        Assert.Contains("ObservableCollection<ShellNavigationEntryViewModel>", child, StringComparison.Ordinal);
        Assert.Contains("PendingNavigation", child, StringComparison.Ordinal);
        Assert.DoesNotContain("_pageHistory", mainNavigation, StringComparison.Ordinal);
        Assert.DoesNotContain("PendingNavigation", mainNavigation, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmNavigationAndClear", mainNavigation, StringComparison.Ordinal);

        int mainAggregate = Directory.EnumerateFiles(
                Path.Combine(Root.FullName, "src", "NvtFwCombiner.Presentation.Avalonia", "ViewModels"),
                "MainWindowViewModel*.cs",
                SearchOption.TopDirectoryOnly)
            .Sum(path => File.ReadLines(path).Count(static line => !string.IsNullOrWhiteSpace(line)));
        Assert.InRange(mainAggregate, 1, 985);
    }
}

using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Locks the intended visual priority of the Merge workflow cards.</summary>
public sealed class HomeMergeWorkflowOrderTests
{
    /// <summary>AB Code is immediately after Standard Merge and before Customized Merge.</summary>
    [Fact]
    public void HomeShowsAbCodeBeforeCustomizedMerge()
    {
        string templates = File.ReadAllText(
            RepositoryPaths.FromRepositoryRoot(
                "src",
                "NvtFwCombiner.Presentation.Avalonia",
                "Resources",
                "MainWindowPageTemplates.axaml"));

        int abCode = templates.IndexOf(
            "Command=\"{Binding BeginAbMergeFromHomeCommand}\"",
            StringComparison.Ordinal);
        int customized = templates.IndexOf(
            "Command=\"{Binding BeginGeneralMergeFromHomeCommand}\"",
            StringComparison.Ordinal);

        Assert.True(abCode >= 0);
        Assert.True(customized >= 0);
        Assert.True(abCode < customized);
    }
}

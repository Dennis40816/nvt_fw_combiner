using System.Xml.Linq;

namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    private const string LocalStateForbiddenSwitch = "NvtFwCombiner.LocalState.CurrentUserFolderForbidden";

    /// <summary>
    /// Bootstrap and version-manager state retain their separate default resolvers behind one test-process switch;
    /// Presentation receives the composed directory, and test sources only inject isolated directories.
    /// </summary>
    [Fact]
    public void CurrentUserLocalStateHasOneGuardedOwnerThatTestsCannotReach()
    {
        string owner = ReadText("src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs");
        int guard = owner.IndexOf(
            "AppContext.TryGetSwitch(CurrentUserLocalStateForbiddenSwitch",
            StringComparison.Ordinal);
        int resolution = owner.IndexOf("Environment.SpecialFolder.LocalApplicationData", StringComparison.Ordinal);
        Assert.True(guard >= 0 && resolution > guard);
        string versionManager = ReadText(
            "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/JsonVersionManagerStateStore.cs");
        Assert.Contains($"\"{LocalStateForbiddenSwitch}\"", versionManager, StringComparison.Ordinal);
        Assert.Contains(
            "CurrentUserLocalStateForbiddenSwitch =\n        JsonVersionManagerStateStore.CurrentUserLocalStateForbiddenSwitch;",
            owner.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
        int versionGuard = versionManager.IndexOf(
            "AppContext.TryGetSwitch(CurrentUserLocalStateForbiddenSwitch",
            StringComparison.Ordinal);
        int environmentResolution = versionManager.IndexOf(
            "Environment.GetEnvironmentVariable(\"LOCALAPPDATA\")",
            StringComparison.Ordinal);
        int platformResolution = versionManager.IndexOf(
            "Environment.SpecialFolder.LocalApplicationData",
            StringComparison.Ordinal);
        Assert.True(versionGuard >= 0 && environmentResolution > versionGuard && platformResolution > versionGuard);
        Assert.Equal(
            [
                "src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs",
                "src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/JsonVersionManagerStateStore.cs",
            ],
            SourceFilesContaining("src", "SpecialFolder.LocalApplicationData"));
        Assert.DoesNotContain("LocalApplicationData", ReadPresentationSources(), StringComparison.Ordinal);
        AssertContainsAll(
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml.cs"),
            "ReportHistoryFileStore.PathIn(hostServices.LocalStateDirectory)",
            "ShellPreferenceFileStore.PathIn(hostServices.LocalStateDirectory)",
            "ReportHistoryFileStore.PathIn(_hostServices.LocalStateDirectory)");
        Assert.Contains(
            "ShellPreferenceFileStore.PathIn(localStateDirectory)",
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/DesktopApplication.cs"),
            StringComparison.Ordinal);

        XElement option = Assert.Single(
            XDocument.Load(Path.Combine(Root.FullName, "Directory.Build.props")).Descendants("RuntimeHostConfigurationOption"));
        Assert.Equal(LocalStateForbiddenSwitch, option.Attribute("Include")?.Value);
        Assert.Equal("true", option.Attribute("Value")?.Value);
        Assert.Equal("'$(IsTestProject)' == 'true'", option.Parent?.Attribute("Condition")?.Value);
        foreach (string project in ProjectFiles("tests").Where(static path =>
                     File.ReadAllText(path).Contains("Include=\"xunit.v3\"", StringComparison.Ordinal)))
        {
            Assert.Contains("<IsTestProject>true</IsTestProject>", File.ReadAllText(project), StringComparison.Ordinal);
        }

        foreach (string project in ProjectFiles("src"))
        {
            AssertDoesNotContainAny(
                File.ReadAllText(project),
                "RuntimeHostConfigurationOption",
                "IsTestProject",
                LocalStateForbiddenSwitch);
        }

        string[] reachingTests = SourceFilesContainingAny(
            "tests",
            "CompositionHostServices.Create()",
            "ResolveCurrentUserLocalStateDirectory",
            "SpecialFolder.LocalApplicationData",
            "DefaultHistoryPath",
            "DefaultPreferencesPath");
        Assert.Equal(["tests/NvtFwCombiner.Bootstrap.Tests/LocalStateGuardTests.cs"], reachingTests);
    }

    private static string[] SourceFilesContaining(string directory, string value)
    {
        return SourceFilesContainingAny(directory, value);
    }

    private static string[] SourceFilesContainingAny(string directory, params string[] values)
    {
        string architectureTests = Path.Combine(Root.FullName, "tests", "NvtFwCombiner.Architecture.Tests") +
            Path.DirectorySeparatorChar;
        return
        [
            .. BuildInputs(directory, "*.cs")
                .Where(path => !path.StartsWith(architectureTests, StringComparison.OrdinalIgnoreCase))
                .Where(path => values.Any(value =>
                    File.ReadAllText(path).Contains(value, StringComparison.Ordinal)))
                .Select(path => Path.GetRelativePath(Root.FullName, path).Replace('\\', '/'))
                .Order(StringComparer.Ordinal),
        ];
    }

    private static IEnumerable<string> ProjectFiles(string directory)
    {
        return BuildInputs(directory, "*.csproj");
    }

    private static IEnumerable<string> BuildInputs(string directory, string pattern)
    {
        string binSegment = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
        string objSegment = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
        return Directory.GetFiles(Path.Combine(Root.FullName, directory), pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains(binSegment, StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(objSegment, StringComparison.OrdinalIgnoreCase));
    }
}

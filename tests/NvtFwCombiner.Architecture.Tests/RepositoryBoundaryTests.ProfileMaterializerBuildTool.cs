namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Bootstrap imports one focused tested profile materializer and owns no task implementation.</summary>
    [Fact]
    public void ProfileMaterializationBuildToolIsFocusedOutsideBootstrap()
    {
        string project = ReadText("src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj");
        string relativeToolPath =
            "eng/profile-bundle-materializer/NvtFwCombiner.ProfileBundleMaterializer.targets";
        string absoluteToolPath = Path.Combine(
            Root.FullName,
            relativeToolPath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(absoluteToolPath));
        Assert.Contains("NvtFwCombiner.ProfileBundleMaterializer.targets", project, StringComparison.Ordinal);
        Assert.DoesNotContain("RoslynCodeTaskFactory", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<UsingTask", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<Target Name=\"MaterializeBuiltInProfileBundles\"", project, StringComparison.Ordinal);

        string tool = File.ReadAllText(absoluteToolPath);
        Assert.Contains("RoslynCodeTaskFactory", tool, StringComparison.Ordinal);
        Assert.Contains("<Target Name=\"MaterializeBuiltInProfileBundles\"", tool, StringComparison.Ordinal);
        Assert.Contains("DuplicatePropertyNameHandling.Error", tool, StringComparison.Ordinal);
    }
}

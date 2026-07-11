using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    private static Task<CliRunResult> RunCliAsync(string[] args)
    {
        return CliTestHarness.RunAsync(args, TestContext.Current.CancellationToken);
    }

    private static string ManifestPath(string fixtureRoot, JsonElement pathElement)
    {
        return RepositoryPaths.PathFromRelative(fixtureRoot, pathElement.GetString()!);
    }

}

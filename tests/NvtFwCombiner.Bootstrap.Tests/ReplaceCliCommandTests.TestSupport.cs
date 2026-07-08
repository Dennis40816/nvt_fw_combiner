using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    private static async Task<CliRunResult> RunCliAsync(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode = await CliApplication
            .RunAsync(args, output, error, TestContext.Current.CancellationToken);
        return new CliRunResult(exitCode, output.ToString(), error.ToString());
    }

    private static string ManifestPath(string fixtureRoot, JsonElement pathElement)
    {
        return Path.Combine(fixtureRoot, pathElement.GetString()!.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string GoldenPath(string relativePath)
    {
        return Path.Combine(
            RepositoryPaths.FindRepositoryRoot(),
            "testdata",
            "golden",
            "standard-merge-gen-flash",
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static byte[] CreatePattern(int length, byte seed)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = unchecked((byte)(seed + index));
        }

        return bytes;
    }

    private sealed record CliRunResult(int ExitCode, string Output, string Error);
}

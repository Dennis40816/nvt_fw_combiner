using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

internal static class CliGoldenByteEvidence
{
    internal static string ArtifactPath(JsonElement goldenCase, string artifactId)
    {
        return CanonicalGoldenTestData.ArtifactPath(CanonicalGoldenTestData.Artifact(goldenCase, artifactId));
    }

    internal static async Task<BuildEvidence> BuildAsync(
        TempWorkspace workspace, string name, string[] arguments, bool automaticName = false)
    {
        string reportPath = workspace.PathFor(name + ".json");
        string outputPath = workspace.PathFor(name + ".bin");
        string[] destination = automaticName
            ? ["--bundle-parent", workspace.Root, "--bundle-name", name]
            : ["--output", outputPath];
        CliRunResult result = await CliTestHarness.RunAsync(
            [.. arguments, .. destination, "--report", reportPath], TestContext.Current.CancellationToken);
        Assert.True(result.ExitCode == 0, result.Error + Environment.NewLine + result.Output);
        using var document = JsonDocument.Parse(File.ReadAllText(reportPath));
        JsonElement report = document.RootElement.Clone();
        if (automaticName)
        {
            outputPath = workspace.PathFor(Path.Combine(name, report.GetProperty("Output").GetProperty("FileName").GetString()!));
        }

        byte[] bytes = File.ReadAllBytes(outputPath);
        Assert.True(report.GetProperty("Output").GetProperty("Committed").GetBoolean());
        Assert.Equal(bytes.LongLength, report.GetProperty("Output").GetProperty("Size").GetInt64());
        Assert.Equal(report.GetProperty("Output").GetProperty("Sha256").GetString(), Hash(bytes));
        return new(bytes, report);
    }

    internal static ByteRange Range(JsonElement range)
    {
        return new(range.GetProperty("Start").GetInt64(), range.GetProperty("Length").GetInt64());
    }

    internal static ByteRange TargetRange(JsonElement report, string operationId)
    {
        JsonElement operation = Assert.Single(report.GetProperty("Operations").EnumerateArray(),
            operation => operation.GetProperty("OperationId").GetString() == operationId);
        Assert.Equal("Succeeded", operation.GetProperty("Status").GetString());
        return Range(operation.GetProperty("TargetRange"));
    }

    internal static void EqualRange(byte[] expected, byte[] actual, ByteRange range)
    {
        Assert.InRange(range.EndExclusive, 1, expected.LongLength);
        Assert.InRange(range.EndExclusive, 1, actual.LongLength);
        Assert.Equal(expected.AsSpan(checked((int)range.Start), checked((int)range.Length)).ToArray(),
            actual.AsSpan(checked((int)range.Start), checked((int)range.Length)).ToArray());
    }

    internal static void EqualGolden(JsonElement goldenCase, byte[] actual)
    {
        JsonElement artifact = CanonicalGoldenTestData.Artifact(goldenCase, "expected-output");
        Assert.Equal(File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(artifact)), actual);
        Assert.Equal(artifact.GetProperty("sha256").GetString(), Hash(actual));
    }

    internal static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    internal sealed record BuildEvidence(byte[] Bytes, JsonElement Report);
}

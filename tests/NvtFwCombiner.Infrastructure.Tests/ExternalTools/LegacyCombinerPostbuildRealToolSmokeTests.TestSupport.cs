using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

public sealed partial class LegacyCombinerPostbuildRealToolSmokeTests
{
    private static List<ByteRange> DecodeRanges(long[] rangeValues)
    {
        if (rangeValues.Length % 2 != 0)
        {
            throw new ArgumentException("Range values must contain start/length pairs.", nameof(rangeValues));
        }

        List<ByteRange> ranges = [];
        for (int index = 0; index < rangeValues.Length; index += 2)
        {
            ranges.Add(new ByteRange(rangeValues[index], rangeValues[index + 1]));
        }

        return ranges;
    }

    private static byte[] CreateSelfReplacementBytes(
        byte[] baseBytes,
        IReadOnlyList<TpFlashMapRegion> selectedRegions)
    {
        byte[] replacementBytes = [.. baseBytes];
        foreach (TpFlashMapRegion region in selectedRegions)
        {
            if (region.Range.EndExclusive > baseBytes.LongLength)
            {
                throw new InvalidOperationException($"Region '{region.RegionId}' exceeds the golden base image.");
            }

            baseBytes.AsSpan((int)region.Range.Start, (int)region.Range.Length)
                .CopyTo(replacementBytes.AsSpan((int)region.Range.Start, (int)region.Range.Length));
        }

        return replacementBytes;
    }

    private static async Task<byte[]> RunPostbuildProcessorAsync(
        LegacyCombinerPostbuildProcessor processor,
        LegacyCombinerPostbuildProfile profile,
        IcNumberSelection selection,
        byte[] postReplacementBytes,
        IReadOnlyList<ByteRange> expectedChangedRanges,
        string runId,
        CancellationToken cancellationToken,
        IReadOnlyList<ExternalProcessorStagedSource>? stagedSources = null,
        bool assertChangedRanges = true)
    {
        var request = new ExternalProcessorRequest(
            runId,
            profile.ProcessorId,
            profile.ToolBindingId,
            postReplacementBytes,
            expectedChangedRanges,
            selection,
            stagedSources);

        ExternalProcessorResult result = await processor.TransformAsync(request, cancellationToken).ConfigureAwait(false);
        Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        if (assertChangedRanges)
        {
            Assert.Equal(expectedChangedRanges, result.ChangedRanges);
        }

        return result.OutputBytes.ToArray();
    }

    private static string FindGoldenExpectedOutput(string goldenRoot, string ic)
    {
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(testCase => testCase.GetProperty("ic").GetString() == ic);
        string relativePath = goldenCase.GetProperty("expectedOutput").GetProperty("path").GetString()!;
        return Path.Combine(goldenRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static ExternalCombinerToolManifest LoadManifest(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        return new ExternalCombinerToolManifest(
            RequiredString(root, "schemaVersion"),
            RequiredString(root, "toolBindingId"),
            RequiredString(root, "toolId"),
            RequiredString(root, "toolVersion"),
            RequiredString(root, "displayName"),
            RequiredString(root, "platform"),
            RequiredString(root, "executableName"),
            RequiredString(root, "sha256"),
            RequiredString(root, "adapterId"),
            RequiredString(root, "inputMode"),
            [.. root.GetProperty("argumentTemplate").EnumerateArray().Select(item => item.GetString()!)],
            RequiredString(root, "workingDirectoryPolicy"),
            root.GetProperty("timeoutSeconds").GetInt32(),
            [.. root.GetProperty("allowedExtraOutputFiles").EnumerateArray().Select(item => item.GetString()!)]);
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        return element.GetProperty(propertyName).GetString()
            ?? throw new InvalidOperationException($"Manifest property '{propertyName}' is null.");
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

}

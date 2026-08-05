using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks the owner-supplied NT51929 single Normal CtrlRAM Andes cross-replacement.</summary>
public sealed class Nt51929AndesNormalCtrlRamGoldenTests
{
    private const string CaseId = "nt51929-andes-normal-cross-110us-to-115us-20260724";
    private const string OutputSha256 = "b426125b966901ee8a0efc49ec598ebb7a6641a4391cc0f7c122d764f9f8464f";
    private const int NormalStart = 0x21B90;
    private const int NormalLength = 0x4A00;

    private static readonly ByteRange[] AllowedCrcDifferenceRanges =
    [
        new(0x7100, 4),
        new(0x7118, 4),
        new(0x27FF0, 4),
        new(0x28008, 4),
    ];

    /// <summary>Replacing the 110us base with 115us Normal bytes differs from the 115us Andes output only at CRC words.</summary>
    [Fact]
    public async Task SingleNormalCrossReplacementDiffersFromAndesGoldenOnlyAtDeclaredCrcWordsAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OwnerCase ownerCase = ReadOwnerCase();
        AssertPhysicalNormalPairs(ownerCase);
        var immutableHashes = ownerCase.Artifacts.ToDictionary(
            static artifact => artifact.Path,
            static artifact => Hash(artifact.Bytes),
            StringComparer.Ordinal);
        using var workspace = TempWorkspace.Create("nfc-nt51929-andes-normal-cross");
        string outputPath = workspace.PathFor("nt51929-andes-normal-cross.bin");
        WorkbenchRunResult result = await CompositionExecutionAdapter.RunReplaceAsync(
            "NT51929",
            "single",
            WorkbenchReplaceModes.CtrlRam,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [WorkbenchSlotIds.ReplaceBase] = ownerCase.RequireRole("replace-base-flashcode-110us").Path,
                ["replace-ctrlram-normal"] = ownerCase.RequireRole("ctrlram-normal-input-115us").Path,
            },
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(OutputSha256, Hash(output));
        AssertCrcOnlyDifference(ownerCase.Expected.Bytes, output);

        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Equal("nt51929-ctrlram-replace-fw200-single", report.RootElement.GetProperty("ProfileId").GetString());
        Assert.Equal("NT51929", report.RootElement.GetProperty("IcId").GetString());
        Assert.All(
            report.RootElement.GetProperty("OutputDifferences").EnumerateArray(),
            static difference => Assert.True(difference.GetProperty("IsAccepted").GetBoolean()));
        Assert.All(
            ownerCase.Artifacts,
            artifact => Assert.Equal(immutableHashes[artifact.Path], Hash(File.ReadAllBytes(artifact.Path))));
    }

    private static void AssertPhysicalNormalPairs(OwnerCase ownerCase)
    {
        OwnerArtifact base110 = ownerCase.RequireRole("replace-base-flashcode-110us");
        OwnerArtifact control110 = ownerCase.RequireRole("ctrlram-normal-control-110us");
        OwnerArtifact replacement115 = ownerCase.RequireRole("ctrlram-normal-input-115us");

        Assert.Equal(
            control110.Bytes.AsSpan(0, NormalLength).ToArray(),
            base110.Bytes.AsSpan(NormalStart, NormalLength).ToArray());
        Assert.Equal(
            replacement115.Bytes.AsSpan(0, NormalLength).ToArray(),
            ownerCase.Expected.Bytes.AsSpan(NormalStart, NormalLength).ToArray());
        Assert.NotEqual(
            Hash(control110.Bytes.AsSpan(0, NormalLength)),
            Hash(replacement115.Bytes.AsSpan(0, NormalLength)));
    }

    private static void AssertCrcOnlyDifference(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        List<int> differences = [];
        for (int index = 0; index < expected.Length; index++)
        {
            if (expected[index] != actual[index])
            {
                differences.Add(index);
            }
        }

        Assert.NotEmpty(differences);
        Assert.All(
            differences,
            index => Assert.Contains(AllowedCrcDifferenceRanges, range => range.Contains(index)));
        Assert.All(
            AllowedCrcDifferenceRanges,
            range => Assert.Contains(differences, index => range.Contains(index)));
        Assert.Equal(16, differences.Count);
        Assert.All(
            AllowedCrcDifferenceRanges,
            range => Assert.Equal(4, differences.Count(index => range.Contains(index))));
    }

    private static OwnerCase ReadOwnerCase()
    {
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", CaseId);
        OwnerArtifact[] artifacts =
        [
            .. goldenCase.GetProperty("artifacts").EnumerateArray().Select(entry =>
            {
                string path = CanonicalGoldenTestData.ArtifactPath(entry);
                byte[] bytes = File.ReadAllBytes(path);
                return new OwnerArtifact(
                    entry.GetProperty("sourceRole").GetString()!,
                    path,
                    bytes);
            }),
        ];
        return new OwnerCase(
            artifacts.Single(static artifact =>
                StringComparer.Ordinal.Equals(artifact.Role, "expected-final-output-115us")),
            artifacts);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed record OwnerArtifact(string Role, string Path, byte[] Bytes);

    private sealed record OwnerCase(OwnerArtifact Expected, IReadOnlyList<OwnerArtifact> Artifacts)
    {
        internal OwnerArtifact RequireRole(string role)
        {
            return Artifacts.Single(artifact => StringComparer.Ordinal.Equals(artifact.Role, role));
        }
    }
}

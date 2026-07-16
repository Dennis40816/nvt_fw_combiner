using System.Security.Cryptography;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

public sealed partial class LegacyCombinerPostbuildRealToolSmokeTests
{
    /// <summary>Locks NT51929 AB first-half evidence to same-product CtrlRAM bytes and allowed CRC drift.</summary>
    [Fact]
    public async Task Nt51929AbFirstHalfIsFactScopedCtrlRamEvidenceNotSingleGolden()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "ab-merge");
        string toolRoot = Path.Combine(repositoryRoot, "external-tools");
        string expectedPath = Path.Combine(
            goldenRoot,
            "expected",
            "nt51929",
            "NT51929ZT_Flashcode_TM_TL150UQAS01-00_Stellantis_V28_D06T05_20260611_AB.bin");
        string tpPath = Path.Combine(goldenRoot, "inputs", "nt51929", "nt51929_TPFW_T05_20260611.bin");

        byte[] abBytes = File.ReadAllBytes(expectedPath);
        byte[] baseBytes = abBytes[..0x40000];
        byte[] tpBytes = File.ReadAllBytes(tpPath);
        Assert.Equal(
            "e257e734a63d0d8a0e471bc7b541366578b9b56c94dd914197508d5af1127c12",
            Convert.ToHexString(SHA256.HashData(baseBytes)).ToLowerInvariant());
        AssertSameBytes(baseBytes, tpBytes, 0x1FC00, 0x1F90);
        AssertSameBytes(baseBytes, tpBytes, 0x21B90, 0x4A00);
        AssertSameBytes(baseBytes, tpBytes, 0x26590, 0x1960);

        ExternalCombinerToolManifest manifest = LoadManifest(
            Path.Combine(toolRoot, "legacy-combiner", "1.13.0", "manifest.json"));
        Assert.True(LegacyCombinerPostbuildCatalog.TryGetDefaultProfile(
            "NT51929",
            out LegacyCombinerPostbuildProfile? profile));
        var selection = new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]);
        List<ByteRange> allowedRanges = DecodeRanges(Nt51929RangeValues());
        List<ByteRange> observedRanges =
        [
            new(0x7100, 4),
            new(0x7118, 3),
            new(0x27FF0, 4),
            new(0x28008, 4),
        ];
        string stagingRoot = Path.Combine(Path.GetTempPath(), $"nfc-nt51929-ab-evidence-{Guid.NewGuid():N}");
        try
        {
            var processor = new LegacyCombinerPostbuildProcessor(
                new ExternalCombinerToolRegistry([manifest]),
                [profile!],
                toolRoot,
                stagingRoot,
                new SystemExternalProcessRunner());
            byte[] output = await RunPostbuildProcessorAsync(
                processor,
                profile!,
                selection,
                baseBytes,
                allowedRanges,
                "nt51929-ab-first-half-single",
                CancellationToken.None,
                assertChangedRanges: false);

            Assert.Equal(observedRanges, ByteDiff.FindChangedRanges(baseBytes, output));
            Assert.Equal(15, observedRanges.Sum(range => range.Length));
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    private static void AssertSameBytes(byte[] left, byte[] right, int start, int length)
    {
        Assert.True(left.AsSpan(start, length).SequenceEqual(right.AsSpan(start, length)));
    }
}

using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class Nt51929CtrlRamFw200SingleEvidenceTests
{
    /// <summary>The shared family remains bound to its reviewed single and cascade route policies.</summary>
    [Theory]
    [InlineData("NT51919", "1-ic", "nt51929-ctrlram-fw200-single-full-flash")]
    [InlineData("NT51919", "2-8-ic", "nt51929-ctrlram-fw1x-cascade-full-flash")]
    [InlineData("NT51929", "1-ic", "nt51929-ctrlram-fw200-single-full-flash")]
    [InlineData("NT51929", "2-8-ic", "nt51929-ctrlram-fw1x-cascade-full-flash")]
    public void SharedBackupFamilyRetainsExactRoutePins(string icId, string count, string mapId)
    {
        var identity = new CapabilityRouteIdentity(icId, ExperienceIds.CtrlRamReplace, count, mapId);
        CanonicalDynamicRoute actual = CanonicalDynamicRouteInventory.Resolve(identity);
        CanonicalCapabilityPolicyRoute expected = Assert.Single(BuiltInCanonicalCapabilityPolicy.Load().Routes,
            route => route.Identity == identity);
        TestContext.Current.TestOutputHelper!.WriteLine($"{identity.RouteId}: {actual.CapabilityFingerprint}");
        Assert.Equal(expected.CapabilityFingerprint, actual.CapabilityFingerprint);
    }

    /// <summary>Real NF replacement also reaches its source-defined 4 KiB Backup copy without touching other bytes.</summary>
    [Theory]
    [InlineData("NT51929")]
    [InlineData("NT51919")]
    public async Task NfReplacementPropagatesIntoCompleteBackupAndPreservesOtherBytes(string icId)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("The Backup regression requires the registered Windows Combiner.");
        }

        OwnerCase evidence = ReadOwnerCase();
        using TempWorkspace workspace = TempWorkspace.Create("nfc-929-nf-backup");
        byte replacement = (byte)(evidence.Expected.Bytes[NfStart] ^ 0x5A);
        string sourcePath = workspace.Write("nf-one-byte.bin", [replacement]);
        string referencePath = workspace.Write("reference.bin", evidence.Expected.Bytes);
        string outputPath = workspace.PathFor("output.bin");
        var slots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = referencePath,
            ["replace-ctrlram-nf"] = sourcePath,
        };
        CompositionRunResult result = await CtrlRamReplaceTestSupport.RunAsync(
            BootstrapTestHost.Canonical, icId, "single", ExperienceIds.CtrlRamReplace,
            slots, true, TestContext.Current.CancellationToken, outputPath);

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(Capacity, output.Length);
        Assert.Equal(replacement, output[NfStart]);
        Assert.Equal(replacement, output[0x2EA00]);
        byte[] expectedBackup = output.AsSpan(0x1F200, 0x1000).ToArray();
        new byte[] { 0x00, 0x4E, 0x56, 0x54 }.CopyTo(expectedBackup, 0xFFC);
        Assert.Equal(expectedBackup, output.AsSpan(0x2E000, 0x1000).ToArray());
        ByteRange[] allowedDifferences =
        [
            new(NfStart, 1), new(0x2EA00, 1),
            new(0x7100, 4), new(0x7118, 4), new(0x27FF0, 4), new(0x28008, 4),
        ];
        Assert.Equal(0, CountDifferencesOutside(evidence.Expected.Bytes, output, allowedDifferences));
        Assert.Equal(evidence.Expected.Bytes, File.ReadAllBytes(referencePath));
        Assert.Equal(new[] { replacement }, File.ReadAllBytes(sourcePath));
    }

    /// <summary>The adjacent bytes remain protected even when the processor claims an empty diff.</summary>
    [Theory]
    [InlineData(0x2DFFF)]
    [InlineData(0x2F000)]
    public async Task WritesImmediatelyOutsideBackupAreRejected(int offset)
    {
        OwnerCase evidence = ReadOwnerCase();
        using TempWorkspace workspace = TempWorkspace.Create("nfc-929-backup-boundary");
        string referencePath = workspace.Write("reference.bin", evidence.Expected.Bytes);
        string outputPath = workspace.PathFor("output.bin");
        var processor = new OutOfBackupProcessor(offset);
        CompositionRunResult result = await CtrlRamReplaceTestSupport.RunWithProcessorAsync(
            BootstrapTestHost.Canonical, "NT51929", "single", CreateSlotPaths(evidence, referencePath),
            true, outputPath, null, processor, TestContext.Current.CancellationToken);

        Assert.Equal(1, processor.CallCount);
        Assert.False(result.Succeeded, CompositionRunReportJson.Serialize(result));
        Assert.Null(result.CommittedOutputId);
        Assert.False(File.Exists(outputPath));
        Assert.Equal(evidence.Expected.Bytes, File.ReadAllBytes(referencePath));
    }

    private sealed class OutOfBackupProcessor(int offset) : IExternalProcessor
    {
        internal int CallCount { get; private set; }

        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            byte[] output = request.InputBytes.ToArray();
            output[offset] ^= 0x5A;
            return ValueTask.FromResult(ExternalProcessorResult.Success(output, [], []));
        }
    }
}

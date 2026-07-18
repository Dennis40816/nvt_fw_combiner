using System.Security.Cryptography;
using NvtFwCombiner.Application.Ports;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <inheritdoc/>
public sealed class WorkbenchFirmwareInspectionSnapshotTests
{
    /// <summary>One snapshot read supplies every shell fact and output-name token without another artifact read.</summary>
    [Fact]
    public async Task OneArtifactReadSuppliesFirmwareInspectionAndOutputNaming()
    {
        byte[] bytes = File.ReadAllBytes(GoldenPath("expected/51926/flash.bin"));
        var reader = new CountingArtifactReader("base.bin", bytes);

        WorkbenchFirmwareArtifactSnapshot? snapshot =
            await WorkbenchCompositionService.TryCaptureFirmwareArtifactAsync(
                reader,
                "base.bin",
                TestContext.Current.CancellationToken);

        Assert.NotNull(snapshot);
        Assert.Equal(1, reader.ReadCount);
        Assert.Equal(bytes.Length, snapshot.Length);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            snapshot.Sha256);

        WorkbenchFirmwareInspection inspection = WorkbenchCompositionService.InspectFirmwareArtifact(
            "NT51926",
            snapshot,
            snapshot);
        WorkbenchOutputFileNameSuggestion outputName = WorkbenchCompositionService.CreateFlashCodeOutputFileNameFromInspections(
            "NT51926",
            [new WorkbenchInspectedOutputNameCandidate(WorkbenchOutputNameCandidateKind.Base, inspection)],
            new DateOnly(2026, 7, 18));

        Assert.Equal(1, reader.ReadCount);
        Assert.Equal("1.4.1", inspection.FirmwareConfig?.CommonFwVersion);
        Assert.Equal("51926_1.4.1", inspection.FirmwareConfig?.PostbuildCategory);
        Assert.Equal("cascade", inspection.ContextSuggestion?.NumberToken);
        Assert.Equal("0102", inspection.DpVersion?.VersionToken);
        Assert.Equal("AUTO_PRJ-597", inspection.CmiDpCode?.JiraBadge);
        Assert.Equal("NT51926_FlashCode_DxxxxT0100_20260718.bin", outputName.FileName);
    }

    private sealed class CountingArtifactReader : IArtifactReader
    {
        private readonly string _artifactId;
        private readonly byte[] _bytes;

        internal CountingArtifactReader(string artifactId, byte[] bytes)
        {
            _artifactId = artifactId;
            _bytes = bytes;
        }

        internal int ReadCount { get; private set; }

        public ValueTask<ReadOnlyMemory<byte>> ReadAsync(
            string artifactId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(_artifactId, artifactId);
            ReadCount++;
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(_bytes);
        }
    }
}

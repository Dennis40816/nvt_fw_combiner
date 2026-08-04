using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.ExternalTools;

/// <summary>Tests the canonical full-record invariants of masked DiffDLM policy.</summary>
public sealed class LegacyCombinerDiffDlmPolicyTests
{
    /// <summary>Every count resolves one complete source prefix and one aligned Backup envelope.</summary>
    [Fact]
    public void ResolvesFullStrideRecordsAndBackupForEverySupportedCount()
    {
        LegacyCombinerDiffDlmPolicy policy = CreatePolicy();

        for (int icCount = 2; icCount <= 8; icCount++)
        {
            Assert.Equal(icCount - 1, policy.GetActiveRecordCount(icCount));
            Assert.Equal((icCount - 1) * 0x1400, policy.GetRequiredSourceLength(icCount));
            Assert.Equal(
                AlignUp(0x2D100 + ((icCount - 1) * 0x1400), 0x1000),
                policy.GetExpectedFirmwareConfigBackupStart(icCount));
            Assert.True(
                policy.GetResolvedFirmwareConfigBackupAuthority(icCount).Contains(
                    new ByteRange(
                        policy.GetExpectedFirmwareConfigBackupStart(icCount),
                        policy.FirmwareConfigBackupLength)));
        }
    }

    /// <summary>Memory disclosure consumes the same active-record and mask geometry as execution.</summary>
    [Fact]
    public void ProjectsCanonicalActiveDiffNfPreservationDetails()
    {
        LegacyCombinerDiffDlmPolicy policy = CreatePolicy();

        IReadOnlyList<Application.MemoryLayout.MemoryLayoutPreservationDetail> details =
            policy.GetPreservationDetails(icCount: 4);

        Assert.Equal(new ByteRange(0x2D100, 0x3C00), policy.GetActiveTargetRange(4));
        Assert.Equal(3, details.Count);
        Assert.Equal(0, details[0].BlockIndex);
        Assert.Equal(new ByteRange(0x0B90, 0x0870), details[0].ArtifactRelativeRange);
        Assert.Equal(new ByteRange(0x2DC90, 0x0870), details[0].ResolvedRange);
        Assert.Equal(2, details[2].BlockIndex);
        Assert.Equal(new ByteRange(0x3390, 0x0870), details[2].ArtifactRelativeRange);
        Assert.Equal(new ByteRange(0x30490, 0x0870), details[2].ResolvedRange);
    }

    /// <summary>One full-record contract cannot silently use different source and target strides.</summary>
    [Fact]
    public void RejectsDifferentSourceAndTargetRecordStrides()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            CreatePolicy(sourceRecordStride: 0x1000));
    }

    /// <summary>Authority must contain the minimum-count Backup, not only the maximum-count case.</summary>
    [Fact]
    public void RejectsAuthorityThatOmitsAnEarlierCountBackup()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            CreatePolicy(
                firmwareConfigBackupAuthority: new ByteRange(0x30000, 0x7000)));
    }

    /// <summary>A fixed owner placement is count-independent and resolves to its exact copy envelope.</summary>
    [Fact]
    public void FixedBackupStartOverridesDynamicPlacementAndNarrowsAuthority()
    {
        LegacyCombinerDiffDlmPolicy policy = CreatePolicy(
            firmwareConfigBackupAuthority: new ByteRange(0x35000, 0x3000),
            fixedFirmwareConfigBackupStart: 0x36000);

        for (int icCount = 2; icCount <= 8; icCount++)
        {
            Assert.Equal(0x36000, policy.GetExpectedFirmwareConfigBackupStart(icCount));
            Assert.Equal(
                new ByteRange(0x36000, 0x1000),
                policy.GetResolvedFirmwareConfigBackupAuthority(icCount));
        }
    }

    /// <summary>A fixed placement outside declared processor authority fails at profile construction.</summary>
    [Fact]
    public void RejectsFixedBackupOutsideAuthority()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            CreatePolicy(
                firmwareConfigBackupAuthority: new ByteRange(0x35000, 0x1000),
                fixedFirmwareConfigBackupStart: 0x36000));
    }

    private static LegacyCombinerDiffDlmPolicy CreatePolicy(
        long sourceRecordStride = 0x1400,
        ByteRange? firmwareConfigBackupAuthority = null,
        long? fixedFirmwareConfigBackupStart = null)
    {
        return new LegacyCombinerDiffDlmPolicy(
            "nt51929-family-dynamic-diffdlm",
            "DiffDLM.bin",
            "postbuild-diffdlm",
            sourceRecordStride,
            targetBase: 0x2D100,
            targetRecordStride: 0x1400,
            new ByteRange(0, 0x0B90),
            [
                new LegacyCombinerDiffDlmMask(
                    LegacyCombinerDiffDlmMaskKind.KeepReference,
                    new ByteRange(0x0B90, 0x0870)),
            ],
            minimumIcCount: 2,
            maximumIcCount: 8,
            activeRecordCountOffset: -1,
            "NF_Ctrlram.bin",
            firmwareConfigBackupAlignment: 0x1000,
            firmwareConfigBackupLength: 0x1000,
            firmwareConfigBackupAuthority ?? new ByteRange(0x2F000, 0x8000),
            ["owner-contract"],
            fixedFirmwareConfigBackupStart);
    }

    private static long AlignUp(long value, int alignment)
    {
        return checked((value + alignment - 1) / alignment * alignment);
    }
}

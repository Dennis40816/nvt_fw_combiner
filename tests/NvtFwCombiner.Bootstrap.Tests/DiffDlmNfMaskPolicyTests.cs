using System.Buffers.Binary;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Focused byte-contract tests for the 0.9.17 Preserve-active-DiffNF hot fix.</summary>
public sealed class DiffDlmNfMaskPolicyTests
{
    /// <summary>51919/29/32 resolve the same owner-approved record geometry.</summary>
    [Theory]
    [InlineData("NT51919")]
    [InlineData("NT51929")]
    [InlineData("NT51932")]
    public void Nt51929LikeFamilyUsesOneSharedGeometry(string icId)
    {
        Assert.True(DiffDlmNfMaskPolicy.TryResolve(
            icId,
            LegacyCombinerPostbuildBranch.Cascade,
            out DiffDlmNfGeometry? geometry));

        Assert.Equal(new ByteRange(0x2D100, 0x8C00), geometry!.MaximumFirmwareRange);
        Assert.Equal(0x1400, geometry.RecordStride);
        Assert.Equal(0xB90, geometry.WritableDlmLength);
        Assert.Equal(0x870, geometry.PreservedNfLength);
        Assert.Equal((2, 8), (geometry.MinimumIcCount, geometry.MaximumIcCount));
        Assert.Equal((0x7120, 0x716C), (geometry.DlmDiffSizeCodeOffset, geometry.DlmDiffStartOffset));
    }

    /// <summary>51950/51 retain the owner-supplied decimal DLM and NF lengths.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void Nt51950LikeFamilyUsesDecimalOwnerGeometry(string icId)
    {
        Assert.True(DiffDlmNfMaskPolicy.TryResolve(
            icId,
            LegacyCombinerPostbuildBranch.Cascade,
            out DiffDlmNfGeometry? geometry));

        Assert.Equal(new ByteRange(0x33200, 0x1400), geometry!.MaximumFirmwareRange);
        Assert.Equal(2320, geometry.WritableDlmLength);
        Assert.Equal(2800, geometry.PreservedNfLength);
        Assert.Equal((2, 2), (geometry.MinimumIcCount, geometry.MaximumIcCount));
        Assert.True(DiffDlmNfMaskPolicy.TryResolveActiveRange(
            geometry,
            icCount: 2,
            new byte[0x40000],
            out ByteRange activeRange,
            out CompositionIssue? issue),
            issue?.Message);
        Assert.Equal(new ByteRange(0x33200, 0x1400), activeRange);
    }

    /// <summary>Source-NF contents do not affect validation because only each active DLM slice is writable.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SourceNfPatternsDoNotAffectActiveDlmValidation(int nfPattern)
    {
        Assert.True(DiffDlmNfMaskPolicy.TryResolve(
            "NT51932",
            LegacyCombinerPostbuildBranch.Cascade,
            out DiffDlmNfGeometry? geometry));
        byte[] reference = Nt51929Reference();
        byte[] selected = Pattern(0x8C00, 17, 11);
        const int activeRecordCount = 3;
        for (int record = 0; record < activeRecordCount; record++)
        {
            Span<byte> nf = selected.AsSpan((record * 0x1400) + 0xB90, 0x870);
            FillNf(nf, nfPattern);
        }

        Assert.True(DiffDlmNfMaskPolicy.TryResolveActiveRange(
            geometry!,
            icCount: 4,
            reference,
            out ByteRange activeRange,
            out CompositionIssue? rangeIssue),
            rangeIssue?.Message);
        Assert.Equal(new ByteRange(0x2D100, activeRecordCount * 0x1400), activeRange);
        Assert.True(DiffDlmNfMaskPolicy.TryValidateSelectedSource(
            geometry!,
            activeRange,
            selected,
            out CompositionIssue? issue),
            issue?.Message);
    }

    /// <summary>Each active record rejects a uniform DLM payload independently.</summary>
    [Fact]
    public void EveryActiveDlmRecordMustBeNonUniform()
    {
        Assert.True(DiffDlmNfMaskPolicy.TryResolve(
            "NT51929",
            LegacyCombinerPostbuildBranch.Cascade,
            out DiffDlmNfGeometry? geometry));
        byte[] reference = Nt51929Reference();
        Assert.True(DiffDlmNfMaskPolicy.TryResolveActiveRange(
            geometry!,
            icCount: 4,
            reference,
            out ByteRange activeRange,
            out _));
        byte[] selected = Pattern(0x8C00, 13, 5);
        selected.AsSpan(0x1400, 0xB90).Fill(0xFF);

        Assert.False(DiffDlmNfMaskPolicy.TryValidateSelectedSource(
            geometry!,
            activeRange,
            selected,
            out CompositionIssue? issue));

        Assert.Equal(WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmSourceInvalid, issue!.Code);
        Assert.Contains("record 1", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>The selected file must contain every complete active slave record.</summary>
    [Fact]
    public void ActivePrefixMustContainEverySlaveRecord()
    {
        Assert.True(DiffDlmNfMaskPolicy.TryResolve(
            "NT51932",
            LegacyCombinerPostbuildBranch.Cascade,
            out DiffDlmNfGeometry? geometry));
        byte[] reference = Nt51929Reference();
        Assert.True(DiffDlmNfMaskPolicy.TryResolveActiveRange(
            geometry!,
            icCount: 4,
            reference,
            out ByteRange activeRange,
            out _));

        Assert.False(DiffDlmNfMaskPolicy.TryValidateSelectedSource(
            geometry!,
            activeRange,
            Pattern((3 * 0x1400) - 1, 23, 3),
            out CompositionIssue? issue));

        Assert.Equal(WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmSourceInvalid, issue!.Code);
        Assert.Contains("at least 15360 bytes", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>The 929-like mask fails closed outside its owner-approved two-to-eight IC range.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public void Nt51929LikeRejectsOutOfRangeIcCounts(int icCount)
    {
        Assert.True(DiffDlmNfMaskPolicy.TryResolve(
            "NT51929",
            LegacyCombinerPostbuildBranch.Cascade,
            out DiffDlmNfGeometry? geometry));

        Assert.False(DiffDlmNfMaskPolicy.TryResolveActiveRange(
            geometry!,
            icCount,
            Nt51929Reference(),
            out _,
            out CompositionIssue? issue));

        Assert.Equal(WorkbenchIssueCodes.ReplaceCtrlRamIcNumberUnsupported, issue!.Code);
    }

    /// <summary>The inclusive eight-IC boundary resolves all seven active records and ends at the declared maximum.</summary>
    [Fact]
    public void Nt51929LikeAcceptsEightIcUpperBoundary()
    {
        Assert.True(DiffDlmNfMaskPolicy.TryResolve(
            "NT51932",
            LegacyCombinerPostbuildBranch.Cascade,
            out DiffDlmNfGeometry? geometry));

        Assert.True(DiffDlmNfMaskPolicy.TryResolveActiveRange(
            geometry!,
            icCount: 8,
            Nt51929Reference(),
            out ByteRange activeRange,
            out CompositionIssue? issue),
            issue?.Message);

        Assert.Equal(new ByteRange(0x2D100, 7 * 0x1400), activeRange);
        Assert.Equal(geometry!.MaximumFirmwareRange.EndExclusive, activeRange.EndExclusive);
    }

    /// <summary>The 950-like mask accepts only its current exact two-IC contract.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Nt51950LikeRejectsNonTwoIcCounts(int icCount)
    {
        Assert.True(DiffDlmNfMaskPolicy.TryResolve(
            "NT51950",
            LegacyCombinerPostbuildBranch.Cascade,
            out DiffDlmNfGeometry? geometry));

        Assert.False(DiffDlmNfMaskPolicy.TryResolveActiveRange(
            geometry!,
            icCount,
            new byte[0x40000],
            out _,
            out CompositionIssue? issue));

        Assert.Equal(WorkbenchIssueCodes.ReplaceCtrlRamIcNumberUnsupported, issue!.Code);
    }

    /// <summary>A symbolic Cascade selector cannot silently fall back when FWConfig has no usable IC Count.</summary>
    [Fact]
    public void SymbolicCascadeRequiresReadablePositiveIcCount()
    {
        Assert.True(DiffDlmNfMaskPolicy.TryResolve(
            "NT51932",
            LegacyCombinerPostbuildBranch.Cascade,
            out DiffDlmNfGeometry? geometry));

        Assert.False(DiffDlmNfMaskPolicy.TryResolveTopologyCount(
            geometry!,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade_2to8"]),
            reportedChipCount: 0,
            out int topologyCount,
            out CompositionIssue? issue));

        Assert.Equal(0, topologyCount);
        Assert.Equal(WorkbenchIssueCodes.ReplaceCtrlRamIcNumberMismatch, issue!.Code);
    }

    /// <summary>An exact numeric selection remains usable when FWConfig is unreadable.</summary>
    [Fact]
    public void ExactNumericSelectionCanProvideTheRequiredIcCount()
    {
        Assert.True(DiffDlmNfMaskPolicy.TryResolve(
            "NT51932",
            LegacyCombinerPostbuildBranch.Cascade,
            out DiffDlmNfGeometry? geometry));

        Assert.True(DiffDlmNfMaskPolicy.TryResolveTopologyCount(
            geometry!,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["4"]),
            reportedChipCount: null,
            out int topologyCount,
            out CompositionIssue? issue),
            issue?.Message);

        Assert.Equal(4, topologyCount);
    }

    /// <summary>The 929-like header must be long enough to contain both geometry fields.</summary>
    [Fact]
    public void TruncatedHeaderCannotResolveActiveRange()
    {
        Assert.True(DiffDlmNfMaskPolicy.TryResolve(
            "NT51929",
            LegacyCombinerPostbuildBranch.Cascade,
            out DiffDlmNfGeometry? geometry));

        Assert.False(DiffDlmNfMaskPolicy.TryResolveActiveRange(
            geometry!,
            icCount: 4,
            new byte[0x716F],
            out _,
            out CompositionIssue? issue));

        Assert.Equal(WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmSourceInvalid, issue!.Code);
        Assert.Contains("too short", issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The header cannot redefine the owner-approved 929-like record stride.</summary>
    [Fact]
    public void WrongHeaderStrideIsRejected()
    {
        Assert.True(DiffDlmNfMaskPolicy.TryResolve(
            "NT51929",
            LegacyCombinerPostbuildBranch.Cascade,
            out DiffDlmNfGeometry? geometry));
        byte[] reference = Nt51929Reference();
        BinaryPrimitives.WriteUInt16LittleEndian(reference.AsSpan(0x7120, sizeof(ushort)), 0x13FE);

        Assert.False(DiffDlmNfMaskPolicy.TryResolveActiveRange(
            geometry!,
            icCount: 4,
            reference,
            out _,
            out CompositionIssue? issue));

        Assert.Equal(WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmSourceInvalid, issue!.Code);
        Assert.Contains("stride", issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The raw header start cannot shift or escape the owner-approved 929-like target range.</summary>
    [Theory]
    [InlineData(0x2D101u)]
    [InlineData(0x35D00u)]
    public void UnapprovedHeaderStartIsRejected(uint start)
    {
        Assert.True(DiffDlmNfMaskPolicy.TryResolve(
            "NT51929",
            LegacyCombinerPostbuildBranch.Cascade,
            out DiffDlmNfGeometry? geometry));
        byte[] reference = Nt51929Reference();
        BinaryPrimitives.WriteUInt32LittleEndian(reference.AsSpan(0x716C, sizeof(uint)), start);

        Assert.False(DiffDlmNfMaskPolicy.TryResolveActiveRange(
            geometry!,
            icCount: 4,
            reference,
            out _,
            out CompositionIssue? issue));

        Assert.Equal(WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmSourceInvalid, issue!.Code);
        Assert.Contains("start", issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Full-artifact families and retired selector families never enter this mask.</summary>
    [Theory]
    [InlineData("NT51917")]
    [InlineData("NT51923")]
    [InlineData("NT51926")]
    [InlineData("NT51927")]
    [InlineData("NT51928")]
    [InlineData("NT51930")]
    [InlineData("NT51931")]
    public void FullArtifactAndRetiredSelectorFamiliesDoNotUseMask(string icId)
    {
        Assert.False(DiffDlmNfMaskPolicy.TryResolve(
            icId,
            LegacyCombinerPostbuildBranch.Cascade,
            out _));
        Assert.False(DiffDlmNfMaskPolicy.TryResolve(
            icId,
            LegacyCombinerPostbuildBranch.SingleChip,
            out _));
    }

    private static byte[] Pattern(int length, int multiplier, int addend)
    {
        return [.. Enumerable.Range(0, length).Select(index => unchecked((byte)((index * multiplier) + addend)))];
    }

    private static byte[] Nt51929Reference()
    {
        byte[] reference = Pattern(0x40000, 29, 7);
        BinaryPrimitives.WriteUInt16LittleEndian(reference.AsSpan(0x7120, sizeof(ushort)), 0x13FF);
        BinaryPrimitives.WriteUInt32LittleEndian(reference.AsSpan(0x716C, sizeof(uint)), 0x2D100);
        return reference;
    }

    private static void FillNf(Span<byte> bytes, int pattern)
    {
        if (pattern is 0 or 1)
        {
            bytes.Fill(pattern == 0 ? (byte)0x00 : (byte)0xFF);
            return;
        }

        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = unchecked((byte)((index * 31) + 9));
        }
    }
}

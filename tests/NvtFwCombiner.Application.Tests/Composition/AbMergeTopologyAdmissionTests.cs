using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Tests.Composition;

/// <summary>Checks pair count equality separately from the optional single/cascade selector.</summary>
public sealed class AbMergeTopologyAdmissionTests
{
    /// <summary>Cascade is a classification, not a replacement for each artifact's actual count.</summary>
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 2, 2)]
    [InlineData(3, 3, 2)]
    [InlineData(255, 255, 2)]
    public void AcceptedCountsRetainTheirExactValues(int a, int b, int selected)
    {
        byte[] tpA = Tp(a);
        byte[] tpB = Tp(b);
        AbMergeTopologyAdmissionResult result = AbMergeTopologyAdmission.Assess(tpA, tpB, Selection(selected), declaredEndFlag: FirmwareNvtEndFlagResolution.LegacyCompatibility);
        Assert.True(result.Succeeded);
        Assert.Empty(result.Issues);
        Assert.Equal((byte)a, result.TpAChipCount);
        Assert.Equal((byte)b, result.TpBChipCount);
        Array.Clear(tpA);
        Array.Clear(tpB);
        Assert.Equal((byte)a, result.TpAChipCount);
        Assert.Equal((byte)b, result.TpBChipCount);
    }

    /// <summary>Every AB pair must have identical counts, including selector-free and mixed cascade pairs.</summary>
    [Theory]
    [InlineData(1, 2, 1)]
    [InlineData(2, 1, 2)]
    [InlineData(2, 3, 2)]
    [InlineData(3, 2, 2)]
    [InlineData(1, 2, 0)]
    [InlineData(2, 3, 0)]
    public void DifferentExactCountsAreRejectedBeforeMapSelection(int a, int b, int selected)
    {
        AbMergeTopologyAdmissionResult result = AbMergeTopologyAdmission.Assess(Tp(a), Tp(b),
            selected == 0 ? null : Selection(selected), declaredEndFlag: FirmwareNvtEndFlagResolution.LegacyCompatibility);
        Assert.False(result.Succeeded);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("AB_TP_TOPOLOGY_MISMATCH", issue.Code);
        Assert.Equal(CompositionIssueSeverity.Error, issue.Severity);
        Assert.Contains($"{a} IC", issue.Message, StringComparison.Ordinal);
        Assert.Contains($"{b} IC", issue.Message, StringComparison.Ordinal);
        Assert.Equal(CompositionAddressSpaceIds.TpBInput, issue.OperationId);
    }

    /// <summary>Invalid Backup has no count; errors retain A/B order and exact wording.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("complement")]
    [InlineData("short")]
    public void InvalidBackupsDoNotInventZeroCounts(string defect)
    {
        byte[] bytes = Tp(2);
        switch (defect)
        {
            case "missing": bytes[0x36FFF] = 0; break;
            case "duplicate": "\0NVT"u8.CopyTo(bytes.AsSpan(0x34000)); break;
            case "complement": bytes[0x36001] = bytes[0x36000]; break;
            case "short": bytes = []; break;
            default: throw new ArgumentOutOfRangeException(nameof(defect));
        }

        AbMergeTopologyAdmissionResult result = AbMergeTopologyAdmission.Assess(bytes, bytes, Selection(2), declaredEndFlag: FirmwareNvtEndFlagResolution.LegacyCompatibility);
        Assert.False(result.Succeeded);
        Assert.Null(result.TpAChipCount);
        Assert.Null(result.TpBChipCount);
        Assert.Collection(result.Issues,
            issue => AssertIssue(issue, "firmware-config.chip-count-unreadable", "tp-a-input: IC Count is unreadable; no unambiguous valid canonical NVT FWConfig Backup.", CompositionAddressSpaceIds.TpAInput),
            issue => AssertIssue(issue, "firmware-config.chip-count-unreadable", "tp-b-input: IC Count is unreadable; no unambiguous valid canonical NVT FWConfig Backup.", CompositionAddressSpaceIds.TpBInput));
    }

    /// <summary>A decoded zero differs from unavailable metadata and precedes topology mismatch.</summary>
    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, 0)]
    [InlineData(0, 0)]
    public void ZeroIsRetainedButBlocksBeforeClassification(int a, int b)
    {
        AbMergeTopologyAdmissionResult result = AbMergeTopologyAdmission.Assess(Tp(a), Tp(b), Selection(1), declaredEndFlag: FirmwareNvtEndFlagResolution.LegacyCompatibility);
        Assert.False(result.Succeeded);
        Assert.Equal((byte)a, result.TpAChipCount);
        Assert.Equal((byte)b, result.TpBChipCount);
        string[] slots = [.. new[] { (a, CompositionAddressSpaceIds.TpAInput), (b, CompositionAddressSpaceIds.TpBInput) }
            .Where(static pair => pair.Item1 == 0).Select(static pair => pair.Item2)];
        Assert.Equal(slots, result.Issues.Select(static issue => issue.OperationId));
        foreach (CompositionIssue issue in result.Issues)
        {
            AssertIssue(issue, "firmware-config.chip-count-required",
                $"IC Count Required: FWConfig Chip_Num at offset 0x17 is 0. {issue.OperationId}: IC Count was read as 0; TP firmware inputs require a positive count. Set Chip_Num correctly before Build.", issue.OperationId!);
        }
    }

    /// <summary>Unreadable and zero remain separate causes on their respective input slots.</summary>
    [Fact]
    public void InvalidPrecedesZeroOnOtherInput()
    {
        AbMergeTopologyAdmissionResult result = AbMergeTopologyAdmission.Assess([], Tp(0), Selection(1), declaredEndFlag: FirmwareNvtEndFlagResolution.LegacyCompatibility);
        Assert.Null(result.TpAChipCount);
        Assert.Equal((byte)0, result.TpBChipCount);
        Assert.Equal(2, result.Issues.Count);
        AssertIssue(result.Issues[0], "firmware-config.chip-count-unreadable",
            "tp-a-input: IC Count is unreadable; no unambiguous valid canonical NVT FWConfig Backup.", CompositionAddressSpaceIds.TpAInput);
        Assert.Equal("firmware-config.chip-count-required", result.Issues[1].Code);
        Assert.Equal(CompositionAddressSpaceIds.TpBInput, result.Issues[1].OperationId);
    }

    /// <summary>Both single/cascade mismatch directions preserve issue priority and subjects.</summary>
    [Theory]
    [InlineData(1, 2, "TPA declares 1 IC but TPB declares 2 IC; AB Merge requires identical TP IC Counts.")]
    [InlineData(3, 1, "TPA declares 3 IC but TPB declares 1 IC; AB Merge requires identical TP IC Counts.")]
    public void InputTopologyMismatchPrecedesSelectedMismatch(int a, int b, string expected)
    {
        AbMergeTopologyAdmissionResult result = AbMergeTopologyAdmission.Assess(Tp(a), Tp(b), Selection(1), declaredEndFlag: FirmwareNvtEndFlagResolution.LegacyCompatibility);
        Assert.False(result.Succeeded);
        AssertIssue(Assert.Single(result.Issues), "AB_TP_TOPOLOGY_MISMATCH", expected, CompositionAddressSpaceIds.TpBInput);
    }

    /// <summary>Selection disagreement preserves the requested label and actual count in its diagnostic.</summary>
    [Theory]
    [InlineData(1, 2, "The selected topology 'test-selection' does not match TPA/TPB FWConfig Backup topology 1 IC.")]
    [InlineData(3, 1, "The selected topology 'test-selection' does not match TPA/TPB FWConfig Backup topology Cascade (3 IC).")]
    public void SelectedMismatchRetainsActualCounts(int observed, int selected, string expected)
    {
        AbMergeTopologyAdmissionResult result = AbMergeTopologyAdmission.Assess(Tp(observed), Tp(observed), Selection(selected), declaredEndFlag: FirmwareNvtEndFlagResolution.LegacyCompatibility);
        Assert.False(result.Succeeded);
        Assert.Equal((byte)observed, result.TpAChipCount);
        AssertIssue(Assert.Single(result.Issues), "AB_TP_TOPOLOGY_SELECTION_MISMATCH", expected, "ab-topology");
    }

    private static void AssertIssue(CompositionIssue issue, string code, string message, string subject)
    {
        Assert.Equal(code, issue.Code);
        Assert.Equal(message, issue.Message);
        Assert.Equal(subject, issue.OperationId);
        Assert.Equal(CompositionIssueSeverity.Error, issue.Severity);
    }

    private static TopologySelection Selection(int count)
    {
        return new(count, "test-selection", TopologySelectionSource.Requested, "ab-topology");
    }

    private static byte[] Tp(int count)
    {
        byte[] bytes = new byte[0x37000];
        bytes[0x36000] = 0x81;
        bytes[0x36001] = 0x7E;
        bytes[0x36017] = checked((byte)count);
        "\0NVT"u8.CopyTo(bytes.AsSpan(0x36FFC));
        return bytes;
    }
}

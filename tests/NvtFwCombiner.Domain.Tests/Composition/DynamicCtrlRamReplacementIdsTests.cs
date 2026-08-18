using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>Tests dynamic CtrlRAM replacement id parsing shared by reports and workbench adapters.</summary>
public sealed class DynamicCtrlRamReplacementIdsTests
{
    /// <summary>Verifies known CtrlRAM replacement ids are rendered consistently for humans.</summary>
    [Theory]
    [InlineData("replace-ctrlram-nf", "NF CtrlRAM")]
    [InlineData("replace-ctrlram-mp-master", "MP CtrlRAM (Master)")]
    [InlineData("replace-ctrlram-normal-slave-r", "Normal CtrlRAM (Slave R)")]
    [InlineData("replace-ctrlram-vn-slave-l", "VN CtrlRAM (Slave L)")]
    public void TryFormatDisplayLabelFormatsKnownDynamicIds(string addressSpaceId, string expectedLabel)
    {
        bool formatted = DynamicCtrlRamReplacementIds.TryFormatDisplayLabel(addressSpaceId, out string label);

        Assert.True(formatted);
        Assert.Equal(expectedLabel, label);
    }

    /// <summary>Topology decoration remains separate from the stable localized title stem.</summary>
    [Theory]
    [InlineData("nf", "NF CtrlRAM")]
    [InlineData("normal-master", "Normal CtrlRAM")]
    [InlineData("vn-slave-l", "VN CtrlRAM")]
    public void FormatRegionBaseDisplayLabelOmitsTopology(string regionId, string expectedLabel)
    {
        Assert.Equal(expectedLabel, DynamicCtrlRamReplacementIds.FormatRegionBaseDisplayLabel(regionId));
    }

    /// <summary>Canonical region ids expose a stable family token without display-label parsing.</summary>
    [Theory]
    [InlineData("nf", "NF")]
    [InlineData("normal-master", "NORMAL")]
    [InlineData("vn-slave-l", "VN")]
    [InlineData("vector-slave-r", "VECTOR")]
    public void GetRegionFamilyTokenIgnoresTopology(string regionId, string expectedToken)
    {
        Assert.Equal(expectedToken, DynamicCtrlRamReplacementIds.GetRegionFamilyToken(regionId));
    }

    /// <summary>Verifies non-CtrlRAM ids are left to the caller's fallback label.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("dp-input")]
    [InlineData("ctrlram-replacement")]
    [InlineData("replace-ctrlram-")]
    public void TryFormatDisplayLabelRejectsNonDynamicIds(string addressSpaceId)
    {
        bool formatted = DynamicCtrlRamReplacementIds.TryFormatDisplayLabel(addressSpaceId, out string label);

        Assert.False(formatted);
        Assert.Empty(label);
    }
}

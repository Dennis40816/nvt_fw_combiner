using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Owner workbook names and the boundary between named and reserved bytes.</summary>
public sealed class FirmwareEventBufferFormatDisplayNamesTests
{
    /// <summary>The workbook's named bytes retain acronym case and readable separators.</summary>
    [Theory]
    [InlineData(0x80, "Common Event Buffer Format")]
    [InlineData(0x81, "Common Event Buffer Format")]
    [InlineData(0x82, "Common Event Buffer Format")]
    [InlineData(0x83, "Common Event Buffer Format")]
    [InlineData(0x84, "Common Event Buffer Format")]
    [InlineData(0x85, "Common Event Buffer Format")]
    [InlineData(0x90, "Auto Ford v1")]
    [InlineData(0x91, "Auto Ford v2")]
    [InlineData(0x92, "Auto Valeo v1")]
    [InlineData(0x93, "Auto INX v1")]
    [InlineData(0x94, "Auto LGE v1")]
    [InlineData(0x95, "Auto INX v2")]
    [InlineData(0x96, "Auto Ford HID")]
    [InlineData(0x97, "Auto Desay")]
    [InlineData(0x98, "Auto TM v1")]
    [InlineData(0x99, "Auto INX v3")]
    [InlineData(0x9A, "Auto INX v4")]
    [InlineData(0x9B, "Auto SGM v1")]
    [InlineData(0x9C, "Auto INX v5")]
    [InlineData(0x9D, "Auto TM Conti v2")]
    [InlineData(0x9E, "Auto TM GM v1")]
    [InlineData(0x9F, "Auto NK v2")]
    [InlineData(0xA0, "Auto INX v6")]
    [InlineData(0xA1, "Auto LGD GM v1")]
    [InlineData(0xA2, "Auto DTEN v1")]
    [InlineData(0xA3, "Auto STLA v1")]
    [InlineData(0xA4, "Auto INX v7")]
    [InlineData(0xA5, "Auto BHTC")]
    [InlineData(0xA6, "Auto Desay Palminfo")]
    [InlineData(0x00, null)]
    [InlineData(0x7F, null)]
    [InlineData(0x86, null)]
    [InlineData(0x8F, null)]
    [InlineData(0xA7, null)]
    [InlineData(0xFF, null)]
    public void ExplicitNamesAndUnlistedValuesRemainDistinct(byte raw, string? expected)
    {
        Assert.Equal(expected, FirmwareEventBufferFormatDisplayNames.GetDisplayName(raw));
    }
}

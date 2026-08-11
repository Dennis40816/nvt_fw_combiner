using NvtFwCombiner.Application.Metadata;

namespace NvtFwCombiner.Application.Tests.Metadata;

/// <summary>Tests the common display projection over canonical field ids.</summary>
public sealed class FirmwareMetadataFieldDisplayNameTests
{
    /// <summary>Indexed header fields retain their semantic acronym and logical index.</summary>
    [Theory]
    [InlineData("header-0-dlm-crc", "DLM CRC 0")]
    [InlineData("header-3-ilm-crc", "ILM CRC 3")]
    [InlineData("common-header-crc", "Common Header CRC")]
    public void FormatsCanonicalHeaderFieldIds(string fieldId, string expected)
    {
        Assert.Equal(expected, FirmwareMetadataFieldDisplayName.Format(fieldId));
    }

    /// <summary>An owner-declared source label remains authoritative.</summary>
    [Fact]
    public void PreservesDeclaredSourceName()
    {
        Assert.Equal(
            "u32HeaderCrc",
            FirmwareMetadataFieldDisplayName.Format(
                "common-header-crc",
                "u32HeaderCrc"));
    }

    /// <summary>Every shared acronym and fallback shape is projected deterministically.</summary>
    [Theory]
    [InlineData("dlm-crc-7", null, "DLM CRC 7")]
    [InlineData("command-mode", null, "Command header mode")]
    [InlineData(
        "bin-crc-ctrlram-diff-dlm-fw-ic-ilm-mp-ov-spi-sram-svn-t12",
        null,
        "BIN CRC CtrlRAM DIFF DLM FW IC ILM MP OV SPI SRAM SVN T12")]
    [InlineData("auto-build-version", null, "Auto-build version")]
    [InlineData("Already formatted", null, "Already formatted")]
    [InlineData("common-header-crc", " ", "Common Header CRC")]
    [InlineData("command-", null, "Command header ")]
    [InlineData("command-fw", null, "Command header fW")]
    [InlineData("-", null, "")]
    public void FormatsSharedTokensAndFallbackShapes(
        string fieldId,
        string? sourceName,
        string expected)
    {
        Assert.Equal(
            expected,
            FirmwareMetadataFieldDisplayName.Format(fieldId, sourceName));
    }
}

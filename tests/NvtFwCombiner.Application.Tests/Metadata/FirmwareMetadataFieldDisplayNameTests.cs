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
}

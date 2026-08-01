namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>
/// Compatibility offsets used by the bounded FWConfig reader and version-write
/// adapter. The canonical all-IC General Parameters field table is declared
/// once in profile data; this class is not a second profile or TP Flash Header
/// definition.
/// </summary>
public static class FirmwareConfigLayout
{
    /// <summary>Offset of <c>u8FWVersion</c> from the start of <c>ST_PUB_FW_CONFIG.gstFwSettings</c>.</summary>
    public const int FirmwareVersionOffset = 0x000;

    /// <summary>Offset of <c>u8FWVersionBar</c>, the bitwise inverse of <c>u8FWVersion</c>.</summary>
    public const int FirmwareVersionBarOffset = 0x001;

    /// <summary>Offset of <c>u8FWSubVersion</c>.</summary>
    public const int FirmwareSubVersionOffset = 0x011;

    /// <summary>
    /// Structure-relative offset of the all-IC <c>u8Chip_Num</c> /
    /// <c>CASCADE_CHIP_NUM</c> source. TP Flash Header fields are unrelated.
    /// </summary>
    public const int ChipNumberOffset = 0x017;

    /// <summary>Offset of <c>u8CommonFwMajorVersion</c>.</summary>
    public const int CommonFwMajorVersionOffset = 0x01A;

    /// <summary>Offset of <c>u8CommonFwMinorVersion</c>.</summary>
    public const int CommonFwMinorVersionOffset = 0x01B;

    /// <summary>Offset of <c>u8CommonFwAdditionalVersion</c>.</summary>
    public const int CommonFwAdditionalVersionOffset = 0x01C;

    /// <summary>Offset of little-endian <c>u16NovaTekProjectID</c>.</summary>
    public const int ProjectIdOffset = 0x022;

    /// <summary>Length of the little-endian project-id field.</summary>
    public const int ProjectIdLength = sizeof(ushort);

    /// <summary>First byte in the common FW hardware-information block.</summary>
    public const int HardwareInfoStartOffset = 0x029;

    /// <summary>Offset of <c>u8FreeRunMode</c>.</summary>
    public const int FreeRunModeOffset = 0x02A;

    /// <summary>Offset of <c>u8SyncType</c>.</summary>
    public const int SyncTypeOffset = 0x02C;

    /// <summary>Offset of <c>u8SenseTermNum</c>.</summary>
    public const int SenseTerminalCountOffset = 0x02D;

    /// <summary>Offset of <c>u8TPTermNumNormal</c>.</summary>
    public const int TouchPanelTerminalCountNormalOffset = 0x02E;

    /// <summary>Offset of <c>u8TPTermNumSelf</c>.</summary>
    public const int TouchPanelTerminalCountSelfOffset = 0x02F;

    /// <summary>Offset of <c>u8I2CDevAddr</c>.</summary>
    public const int I2cDeviceAddressOffset = 0x031;

    /// <summary>Offset of <c>u8InterpolationX</c>.</summary>
    public const int InterpolationStepXOffset = 0x032;

    /// <summary>Offset of <c>u8InterpolationY</c>.</summary>
    public const int InterpolationStepYOffset = 0x033;

    /// <summary>Offset of little-endian <c>u16S2DSensorDots</c>.</summary>
    public const int S2dSensorDotsOffset = 0x034;

    /// <summary>Offset of <c>u8MaxZoneNum</c>.</summary>
    public const int MaxZoneCountOffset = 0x038;

    /// <summary>Offset of signed <c>s8InterpStartOffsetX</c>.</summary>
    public const int InterpolationStartOffsetXOffset = 0x039;

    /// <summary>Offset of signed <c>s8InterpStartOffsetY</c>.</summary>
    public const int InterpolationStartOffsetYOffset = 0x03A;

    /// <summary>Offset of <c>u8MaxFingerNum</c>.</summary>
    public const int MaxFingerCountOffset = 0x03B;

    /// <summary>Byte length of one little-endian GIP table word.</summary>
    public const int GipTableWordLength = sizeof(uint);

    /// <summary>Number of words in each GIP table group.</summary>
    public const int GipTableWordCount = 4;

    /// <summary>Offset of the GIP-before left table group.</summary>
    public const int GipBeforeLeftOffset = 0x03C;

    /// <summary>Offset of the GIP-before right table group.</summary>
    public const int GipBeforeRightOffset = 0x04C;

    /// <summary>Offset of the GIP-after left table group.</summary>
    public const int GipAfterLeftOffset = 0x05C;

    /// <summary>Offset of the GIP-after right table group.</summary>
    public const int GipAfterRightOffset = 0x06C;

    /// <summary>Exclusive end of the common hardware-information block.</summary>
    public const int HardwareInfoEndExclusive = 0x07C;

    /// <summary>Minimum bytes required from the FWConfig start to read all exposed fields.</summary>
    public const int RequiredLength = HardwareInfoEndExclusive;
}

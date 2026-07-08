namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>Reviewed FWConfig byte offsets used for firmware metadata and postbuild category selection.</summary>
public static class FirmwareConfigLayout
{
    /// <summary>Offset of <c>u8FWVersion</c> from the start of <c>ST_PUB_FW_CONFIG.gstFwSettings</c>.</summary>
    public const int FirmwareVersionOffset = 0x000;

    /// <summary>Offset of <c>u8FWVersionBar</c>, the bitwise inverse of <c>u8FWVersion</c>.</summary>
    public const int FirmwareVersionBarOffset = 0x001;

    /// <summary>Offset of <c>u8FWSubVersion</c>.</summary>
    public const int FirmwareSubVersionOffset = 0x011;

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

    /// <summary>Minimum bytes required from the FWConfig start to read all exposed fields.</summary>
    public const int RequiredLength = ProjectIdOffset + ProjectIdLength;
}

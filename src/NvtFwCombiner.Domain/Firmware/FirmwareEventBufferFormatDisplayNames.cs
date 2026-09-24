namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Owner-supplied names for observed bytes; grants no recognition, map or execution authority.</summary>
public static class FirmwareEventBufferFormatDisplayNames
{
    /// <summary>Returns the explicit workbook label, or null for an unlisted byte.</summary>
    public static string? GetDisplayName(byte value)
    {
        return value switch
        {
            0x80 or 0x81 or 0x82 or 0x83 or 0x84 or 0x85 => "Common Event Buffer Format",
            0x90 => "Auto Ford v1",
            0x91 => "Auto Ford v2",
            0x92 => "Auto Valeo v1",
            0x93 => "Auto INX v1",
            0x94 => "Auto LGE v1",
            0x95 => "Auto INX v2",
            0x96 => "Auto Ford HID",
            0x97 => "Auto Desay",
            0x98 => "Auto TM v1",
            0x99 => "Auto INX v3",
            0x9A => "Auto INX v4",
            0x9B => "Auto SGM v1",
            0x9C => "Auto INX v5",
            0x9D => "Auto TM Conti v2",
            0x9E => "Auto TM GM v1",
            0x9F => "Auto NK v2",
            0xA0 => "Auto INX v6",
            0xA1 => "Auto LGD GM v1",
            0xA2 => "Auto DTEN v1",
            0xA3 => "Auto STLA v1",
            0xA4 => "Auto INX v7",
            0xA5 => "Auto BHTC",
            0xA6 => "Auto Desay Palminfo",
            _ => null,
        };
    }
}

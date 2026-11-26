// Copyright (c) 2026 Dennis Liu. All rights reserved.
namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Projects the Common FW version from the canonical located FWConfig definition.</summary>
public static class CommonFirmwareVersionMetadata
{
    /// <summary>Canonical FWConfig structure binding.</summary>
    public const string StructureId = "firmware-config-general-parameters";
    /// <summary>Canonical major byte.</summary>
    public const string MajorFieldId = "common-firmware-major-version";
    /// <summary>Canonical minor byte.</summary>
    public const string MinorFieldId = "common-firmware-minor-version";
    /// <summary>Canonical additional byte.</summary>
    public const string AdditionalFieldId = "common-firmware-additional-version";

    /// <summary>Reads only declared decoded byte fields, with no physical-offset fallback.</summary>
    public static bool TryRead(FirmwareDecodedMetadataStructure structure, out CommonFirmwareVersion version)
    {
        ArgumentNullException.ThrowIfNull(structure);
        version = default;
        if (!TryByte(MajorFieldId, out byte major) || !TryByte(MinorFieldId, out byte minor) ||
            !TryByte(AdditionalFieldId, out byte additional)) { return false; }
        version = new(major, minor, additional);
        return true;

        bool TryByte(string fieldId, out byte value)
        {
            value = 0;
            FirmwareMetadataValue? fact = structure.Facts.SingleOrDefault(fact => fact.FieldId == fieldId)?.Value;
            if (fact?.Kind != FirmwareMetadataValueKind.UnsignedInteger || fact.UnsignedIntegerValue > byte.MaxValue)
            { return false; }
            value = checked((byte)fact.UnsignedIntegerValue!.Value);
            return true;
        }
    }
}

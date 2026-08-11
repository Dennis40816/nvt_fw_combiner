using System.Text.Json;
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.Normalization;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

internal static partial class FirmwareFamilyResolutionNormalizer
{
    private static TopologyRequirement NormalizeTopology(
        FirmwareTopologyRequirementDocument document,
        string path)
    {
        return document.Kind switch
        {
            "none" => TopologyRequirement.NoTopologyConstraint(),
            "single" => TopologyRequirement.RequireSingleChip(),
            "cascade" => TopologyRequirement.RequireCascade(
                ReadInt32(document.MinimumChipCount!.Value, $"{path}.minimumChipCount"),
                document.MaximumChipCount is { } maximum
                    ? ReadInt32(maximum, $"{path}.maximumChipCount")
                    : null),
            "exact-count" => TopologyRequirement.RequireExactCount(
                ReadInt32(document.ChipCount!.Value, $"{path}.chipCount")),
            _ => throw Error($"{path}.kind", "Unknown topology requirement kind."),
        };
    }

    private static FirmwareMetadataValue NormalizeExpectedValue(
        JsonElement document,
        FirmwareMetadataField field,
        string path)
    {
        FirmwareMetadataValue value = field.Encoding switch
        {
            FirmwareMetadataEncoding.Bytes => FirmwareMetadataValue.FromBytes(
                ParseHex(ReadString(document, path), path)),
            FirmwareMetadataEncoding.PrintableAscii => FirmwareMetadataValue.FromText(
                ReadString(document, path)),
            FirmwareMetadataEncoding.UnsignedInteger => FirmwareMetadataValue.FromUnsignedInteger(
                ReadUInt64(document, path)),
            FirmwareMetadataEncoding.SignedInteger => FirmwareMetadataValue.FromSignedInteger(
                ReadInt64(document, path)),
            _ => throw Error(path, "Unknown metadata field encoding."),
        };

        return !field.CanRepresent(value) ? throw Error(path, $"Value is not representable by metadata field '{field.FieldId}'.") : value;
    }

    private static FirmwareRegionOwner NormalizeOwner(string value, string path)
    {
        return value switch
        {
            "system" => FirmwareRegionOwner.System,
            "dp" => FirmwareRegionOwner.Dp,
            "tp" => FirmwareRegionOwner.Tp,
            "ldc" => FirmwareRegionOwner.Ldc,
            "register" => FirmwareRegionOwner.Register,
            "customer" => FirmwareRegionOwner.Customer,
            "shared" => FirmwareRegionOwner.Shared,
            "reserved" => FirmwareRegionOwner.Reserved,
            "unknown" => FirmwareRegionOwner.Unknown,
            _ => throw Error(path, "Unknown firmware region owner."),
        };
    }

    private static FirmwareRegionKind NormalizeRegionKind(string value, string path)
    {
        return value switch
        {
            "image" => FirmwareRegionKind.Image,
            "code" => FirmwareRegionKind.Code,
            "header" => FirmwareRegionKind.Header,
            "data" => FirmwareRegionKind.Data,
            "command" => FirmwareRegionKind.Command,
            "firmware-config" => FirmwareRegionKind.FirmwareConfig,
            "ctrlram" => FirmwareRegionKind.CtrlRam,
            "customer-information" => FirmwareRegionKind.CustomerInformation,
            "checksum" => FirmwareRegionKind.Checksum,
            "padding" => FirmwareRegionKind.Padding,
            "reserved" => FirmwareRegionKind.Reserved,
            "unmapped" => FirmwareRegionKind.Unmapped,
            _ => throw Error(path, "Unknown firmware region kind."),
        };
    }

    private static FirmwareWriteConstraint NormalizeWriteConstraint(string value, string path)
    {
        return value switch
        {
            "forbidden" => FirmwareWriteConstraint.Forbidden,
            "whole-region" => FirmwareWriteConstraint.WholeRegion,
            "declared-subregions" => FirmwareWriteConstraint.DeclaredSubregions,
            "explicit-range" => FirmwareWriteConstraint.ExplicitRange,
            _ => throw Error(path, "Unknown firmware write constraint."),
        };
    }

    private static long ReadInt64(JsonElement value, string path)
    {
        return TranslateInvariant(path, () => (long)ContractJsonValueReader.ReadInteger(value));
    }

    private static int ReadInt32(JsonElement value, string path)
    {
        return TranslateInvariant(path, () => (int)ContractJsonValueReader.ReadInteger(value));
    }

    private static ulong ReadUInt64(JsonElement value, string path)
    {
        return TranslateInvariant(path, () => (ulong)ContractJsonValueReader.ReadInteger(value));
    }

    private static string ReadString(JsonElement value, string path)
    {
        return TranslateInvariant(path, () => ContractJsonValueReader.ReadString(value));
    }

    private static byte[] ParseHex(string value, string path)
    {
        return TranslateInvariant(path, () => ContractJsonValueReader.ParseCanonicalHex(value));
    }
}

using System.Numerics;
using System.Text.Json;
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.Normalization;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

public static partial class FirmwareFamilyResolutionNormalizer
{
    private static TopologyRequirement NormalizeTopology(
        FirmwareTopologyRequirementDocument document,
        string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Kind switch
        {
            "none" => TopologyRequirement.NoTopologyConstraint(),
            "single" => TopologyRequirement.RequireSingleChip(),
            "cascade" => TopologyRequirement.RequireCascade(
                ReadInt32(
                    Require(document.MinimumChipCount, $"{path}.minimumChipCount"),
                    2,
                    int.MaxValue,
                    $"{path}.minimumChipCount"),
                document.MaximumChipCount is { } maximum
                    ? ReadInt32(maximum, 2, int.MaxValue, $"{path}.maximumChipCount")
                    : null),
            "exact-count" => TopologyRequirement.RequireExactCount(
                ReadInt32(
                    Require(document.ChipCount, $"{path}.chipCount"),
                    1,
                    int.MaxValue,
                    $"{path}.chipCount")),
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
                ReadUInt64(document, 0, ulong.MaxValue, path)),
            FirmwareMetadataEncoding.SignedInteger => FirmwareMetadataValue.FromSignedInteger(
                ReadInt64(document, long.MinValue, long.MaxValue, path)),
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

    private static long ReadInt64(JsonElement value, long minimum, long maximum, string path)
    {
        BigInteger integer = ReadInteger(value, path);
        return integer < minimum || integer > maximum
            ? throw Error(path, $"Integer must be between {minimum} and {maximum}.")
            : (long)integer;
    }

    private static int ReadInt32(JsonElement value, int minimum, int maximum, string path)
    {
        BigInteger integer = ReadInteger(value, path);
        return integer < minimum || integer > maximum
            ? throw Error(path, $"Integer must be between {minimum} and {maximum}.")
            : (int)integer;
    }

    private static ulong ReadUInt64(JsonElement value, ulong minimum, ulong maximum, string path)
    {
        BigInteger integer = ReadInteger(value, path);
        return integer < minimum || integer > maximum
            ? throw Error(path, $"Integer must be between {minimum} and {maximum}.")
            : (ulong)integer;
    }

    private static BigInteger ReadInteger(JsonElement value, string path)
    {
        try
        {
            return ContractJsonValueReader.ReadInteger(value);
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
        }
    }

    private static string ReadString(JsonElement value, string path)
    {
        try
        {
            return ContractJsonValueReader.ReadString(value);
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
        }
    }

    private static byte[] ParseHex(string value, string path)
    {
        try
        {
            return ContractJsonValueReader.ParseCanonicalHex(value);
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
        }
    }
}

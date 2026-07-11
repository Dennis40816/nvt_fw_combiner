using System.Globalization;
using System.Numerics;
using System.Text.Json;
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;

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
        if (value.ValueKind != JsonValueKind.Number)
        {
            throw Error(path, "Expected a JSON integer.");
        }

        string raw = value.GetRawText();
        int exponentSeparator = raw.IndexOfAny(['e', 'E']);
        ReadOnlySpan<char> mantissa = exponentSeparator >= 0
            ? raw.AsSpan(0, exponentSeparator)
            : raw.AsSpan();
        BigInteger exponent = exponentSeparator >= 0
            ? BigInteger.Parse(
                raw.AsSpan(exponentSeparator + 1),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture)
            : BigInteger.Zero;
        bool negative = mantissa.StartsWith("-", StringComparison.Ordinal);
        if (negative)
        {
            mantissa = mantissa[1..];
        }

        int decimalPoint = mantissa.IndexOf('.');
        int fractionalDigits = decimalPoint >= 0 ? mantissa.Length - decimalPoint - 1 : 0;
        string digits = decimalPoint >= 0
            ? string.Concat(
                mantissa[..decimalPoint].ToString(),
                mantissa[(decimalPoint + 1)..].ToString())
            : mantissa.ToString();
        digits = digits.TrimStart('0');
        if (digits.Length == 0)
        {
            return BigInteger.Zero;
        }

        BigInteger power = exponent - fractionalDigits;
        if (power < 0)
        {
            var requiredTrailingZeros = BigInteger.Negate(power);
            int availableTrailingZeros = digits.Length - digits.TrimEnd('0').Length;
            if (requiredTrailingZeros > availableTrailingZeros)
            {
                throw Error(path, "Expected a mathematically integral JSON number.");
            }

            digits = digits[..^(int)requiredTrailingZeros];
            power = BigInteger.Zero;
        }

        if (power > 100)
        {
            throw Error(path, "Integer exceeds every supported Domain numeric range.");
        }

        var integer = BigInteger.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
        if (power > 0)
        {
            integer *= BigInteger.Pow(10, (int)power);
        }

        return negative ? BigInteger.Negate(integer) : integer;
    }

    private static string ReadString(JsonElement value, string path)
    {
        return value.ValueKind == JsonValueKind.String && value.GetString() is { } text
            ? text
            : throw Error(path, "Expected a JSON string.");
    }

    private static byte[] ParseHex(string value, string path)
    {
        return string.IsNullOrEmpty(value) ||
            value.Length % 2 != 0 ||
            value.Any(static character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            ? throw Error(path, "Expected non-empty canonical lowercase hexadecimal bytes.")
            : Convert.FromHexString(value);
    }
}

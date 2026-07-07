namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>Version-byte rules ported from the approved gen_flash_bin_v2 IC configuration.</summary>
public static class GenFlashVersionCatalog
{
    private const string GenFlashEvidence = "gen_flash_bin_v2/ic_config.json";
    private static readonly Dictionary<string, GenFlashDpVersionRule> DpVersionRules = BuildDpVersionRules();

    /// <summary>Returns the DP version-byte rule for an IC when gen_flash evidence defines one.</summary>
    public static bool TryGetDpVersionRule(string icId, out GenFlashDpVersionRule rule)
    {
        bool found = DpVersionRules.TryGetValue(NormalizeIcId(icId), out GenFlashDpVersionRule? foundRule);
        rule = foundRule!;
        return found;
    }

    /// <summary>Reads a DP version byte from a selected DP payload using the IC's gen_flash rule.</summary>
    public static bool TryReadDpVersion(
        string icId,
        ReadOnlySpan<byte> image,
        out GenFlashDpVersionMetadata metadata)
    {
        metadata = default;
        if (!TryGetDpVersionRule(icId, out GenFlashDpVersionRule rule))
        {
            return false;
        }

        long readOffset = image.Length >= rule.OutputDpEndExclusive
            ? rule.OutputAbsoluteAddress
            : rule.InputRelativeOffset;
        if (readOffset < 0 || readOffset >= image.Length)
        {
            return false;
        }

        byte value = image[(int)readOffset];
        metadata = new GenFlashDpVersionMetadata(
            rule.IcId,
            rule.Prefix,
            value,
            readOffset,
            rule.OutputAbsoluteAddress,
            rule.EvidenceSource);
        return true;
    }

    private static Dictionary<string, GenFlashDpVersionRule> BuildDpVersionRules()
    {
        Dictionary<string, GenFlashDpVersionRule> rules = new(StringComparer.Ordinal)
        {
            ["51920"] = Rule("51920", 0x3E000, 0x40000, 0x15),
            ["51923"] = Rule("51923", 0x3E000, 0x40000, 0x15),
            ["51926"] = Rule("51926", 0x3E000, 0x40000, 0x15),
            ["51927"] = Rule("51927", 0x3C000, 0x40000, 0x1D),
            ["51928"] = Rule("51928", 0x3C000, 0x40000, 0x1D),
            ["51929"] = Rule("51929", 0x00000, 0x06000, 0x67),
            ["51931"] = Rule("51931", 0x3E000, 0x40000, 0x19),
            ["51932"] = Rule("51932", 0x00000, 0x06000, 0x67),
        };

        rules["51917"] = Alias("51917", rules["51927"], "owner-confirmed NT51917 follows NT51927 gen_flash layout.");
        rules["51919"] = Alias("51919", rules["51929"], "owner-confirmed NT51919 follows NT51929 gen_flash layout.");
        return rules;
    }

    private static GenFlashDpVersionRule Rule(
        string ic,
        long dpStart,
        long dpEndExclusive,
        long relativeOffset)
    {
        return new GenFlashDpVersionRule(
            ic,
            "D",
            relativeOffset,
            dpStart + relativeOffset,
            dpStart,
            dpEndExclusive,
            GenFlashEvidence);
    }

    private static GenFlashDpVersionRule Alias(
        string ic,
        GenFlashDpVersionRule source,
        string evidence)
    {
        return new GenFlashDpVersionRule(
            ic,
            source.Prefix,
            source.InputRelativeOffset,
            source.OutputAbsoluteAddress,
            source.OutputDpStart,
            source.OutputDpEndExclusive,
            $"{source.EvidenceSource}; {evidence}");
    }

    private static string NormalizeIcId(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        string value = icId.Trim();
        return value.StartsWith("NT", StringComparison.OrdinalIgnoreCase)
            ? value[2..]
            : value;
    }
}

/// <summary>DP version-byte location derived from gen_flash_bin_v2.</summary>
public sealed record GenFlashDpVersionRule(
    string IcId,
    string Prefix,
    long InputRelativeOffset,
    long OutputAbsoluteAddress,
    long OutputDpStart,
    long OutputDpEndExclusive,
    string EvidenceSource);

/// <summary>DP version-byte value read from a selected DP payload.</summary>
public readonly record struct GenFlashDpVersionMetadata(
    string IcId,
    string Prefix,
    byte VersionByte,
    long InputReadOffset,
    long OutputAbsoluteAddress,
    string EvidenceSource)
{
    /// <summary>Two uppercase hex digits used by gen_flash naming.</summary>
    public string VersionToken => FormattableString.Invariant($"{VersionByte:X2}");

    /// <summary>Readable DP version token including the configured prefix.</summary>
    public string DisplayVersion => $"{Prefix}{VersionToken}";
}

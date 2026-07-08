namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>Version-byte rules ported from the approved gen_flash_bin_v2 IC configuration.</summary>
public static class GenFlashVersionCatalog
{
    private const string GenFlashEvidence = "gen_flash_bin_v2/ic_config.json";
    private static readonly Dictionary<string, GenFlashDpVersionRule> DpVersionRules = BuildDpVersionRules();

    /// <summary>Returns the DP main/sub version-byte rule for an IC when gen_flash evidence defines one.</summary>
    public static bool TryGetDpVersionRule(string icId, out GenFlashDpVersionRule rule)
    {
        bool found = DpVersionRules.TryGetValue(NormalizeIcId(icId), out GenFlashDpVersionRule? foundRule);
        rule = foundRule!;
        return found;
    }

    /// <summary>Reads contiguous DP main/sub version bytes from a selected DP payload using the IC's gen_flash rule.</summary>
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
            ? rule.OutputMainAbsoluteAddress
            : rule.InputRelativeOffset;
        if (readOffset < 0 || readOffset >= image.Length)
        {
            return false;
        }

        long subReadOffset = readOffset + 1;
        if (subReadOffset < 0 || subReadOffset >= image.Length)
        {
            return false;
        }

        byte mainValue = image[(int)readOffset];
        byte subValue = image[(int)subReadOffset];
        metadata = new GenFlashDpVersionMetadata(
            rule.IcId,
            rule.Prefix,
            mainValue,
            subValue,
            readOffset,
            subReadOffset,
            rule.OutputMainAbsoluteAddress,
            rule.OutputSubAbsoluteAddress,
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
            dpStart + relativeOffset + 1,
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
            source.OutputMainAbsoluteAddress,
            source.OutputSubAbsoluteAddress,
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

/// <summary>Contiguous DP main/sub version-byte location derived from gen_flash_bin_v2.</summary>
public sealed record GenFlashDpVersionRule(
    string IcId,
    string Prefix,
    long InputRelativeOffset,
    long OutputMainAbsoluteAddress,
    long OutputSubAbsoluteAddress,
    long OutputDpStart,
    long OutputDpEndExclusive,
    string EvidenceSource);

/// <summary>Contiguous DP main/sub version-byte value read from a selected DP payload.</summary>
public readonly record struct GenFlashDpVersionMetadata(
    string IcId,
    string Prefix,
    byte MainVersionByte,
    byte SubVersionByte,
    long MainInputReadOffset,
    long SubInputReadOffset,
    long OutputMainAbsoluteAddress,
    long OutputSubAbsoluteAddress,
    string EvidenceSource)
{
    /// <summary>Four uppercase hex digits used by FlashCode naming: DP main byte followed by sub byte.</summary>
    public string VersionToken => FormattableString.Invariant($"{MainVersionByte:X2}{SubVersionByte:X2}");

    /// <summary>Readable DP version token including the configured prefix.</summary>
    public string DisplayVersion => $"{Prefix}{VersionToken}";
}

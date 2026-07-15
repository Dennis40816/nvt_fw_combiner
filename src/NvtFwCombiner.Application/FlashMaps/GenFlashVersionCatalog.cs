using System.Collections.ObjectModel;

namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>Version-byte rules ported from the approved gen_flash_bin_v2 IC configuration.</summary>
public static class GenFlashVersionCatalog
{
    private const string GenFlashEvidence = "gen_flash_bin_v2/ic_config.json";
    private const string Nt51950CmiEvidence =
        "Owner-confirmed CMI DP register map (Reg16h-18h), 2026-07-10; " +
        "standard-merge-gen-flash/51950-dp-256k golden input verifies the 1IC location; " +
        "TP NVT FWConfig ChipNumber selects 1IC 0x3B016 versus cascade 0x05016.";
    private const string Nt51951CmiEvidence =
        "Owner-confirmed CMI DP register map (Reg16h-18h), 2026-07-10; " +
        "standard-merge-gen-flash/51951-dp-512k golden input.";
    private const string Nt51926CmiEvidence =
        "Owner-confirmed CMI DP register map (Reg16h-18h), 2026-07-10; " +
        "ctrlram-replace 20260705 2IC golden base, JIRA0597 / D02 cross-check.";
    private const string Nt51927CmiEvidence =
        "Owner-confirmed CMI DP register map (Reg16h-18h), 2026-07-10; " +
        "ctrlram-replace 20260705 2IC JIRA0251 / D09 and 3IC D08 golden bases; " +
        "standard-merge-gen-flash 2 MiB DP input.";
    private const string Nt51929CmiEvidence =
        "Owner-confirmed CMI DP register map (Reg16h-18h), 2026-07-10; " +
        "private 51929 replacement DP golden (SHA-256 91ce8204d7dc6103a015eba59f9ddb41ef5d1a64c101aa62a4fe7c4517f5cebf) " +
        "cross-checked against the legacy DP major version rule. See owner-handoff/combiner-and-51929/CASE.md.";
    private const string Nt51923CmiEvidence =
        "Owner-confirmed CMI DP register map (Reg16h-18h), 2026-07-10; " +
        "standard-merge-gen-flash expected output cross-checks the retained DP CMI bytes with legacy DP major.";
    private const string Nt51932CmiEvidence =
        "Owner-confirmed CMI DP register map (Reg16h-18h), 2026-07-10; " +
        "standard-merge-gen-flash expected output cross-checks the retained DP CMI bytes with legacy DP major.";
    private static readonly Dictionary<string, GenFlashDpVersionRule> DpVersionRulesByIc = BuildDpVersionRules();
    private static readonly Dictionary<string, CmiDpCodeRule> CmiDpCodeRulesByIc = BuildCmiDpCodeRules();

    /// <summary>All owner-evidenced DP main/sub version-byte rules in stable IC order.</summary>
    public static IReadOnlyList<GenFlashDpVersionRule> AllDpVersionRules { get; } =
    [
        .. DpVersionRulesByIc.Values.OrderBy(rule => rule.IcId, StringComparer.Ordinal),
    ];

    /// <summary>Returns the DP main/sub version-byte rule for an IC when gen_flash evidence defines one.</summary>
    public static bool TryGetDpVersionRule(string icId, out GenFlashDpVersionRule rule)
    {
        bool found = DpVersionRulesByIc.TryGetValue(NormalizeIcId(icId), out GenFlashDpVersionRule? foundRule);
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

    /// <summary>All owner-evidenced CMI DP register rules in stable IC order.</summary>
    public static IReadOnlyList<CmiDpCodeRule> AllCmiDpCodeRules { get; } =
    [
        .. CmiDpCodeRulesByIc.Values.OrderBy(rule => rule.IcId, StringComparer.Ordinal),
    ];

    /// <summary>Returns the CMI DP register rule for an IC when explicit evidence defines one.</summary>
    public static bool TryGetCmiDpCodeRule(string icId, out CmiDpCodeRule rule)
    {
        bool found = CmiDpCodeRulesByIc.TryGetValue(NormalizeIcId(icId), out CmiDpCodeRule? foundRule);
        rule = foundRule!;
        return found;
    }

    /// <summary>
    /// Reads CMI DP Reg16h-18h metadata. The system code is the Jira number and is distinct from
    /// the DP major/minor version.
    /// </summary>
    public static bool TryReadCmiDpCode(
        string icId,
        ReadOnlySpan<byte> image,
        out CmiDpCodeMetadata metadata)
    {
        return TryReadCmiDpCode(icId, image, firmwareConfigChipNumber: null, out metadata);
    }

    /// <summary>
    /// Reads CMI DP Reg16h-18h metadata, using the TP NVT Backup FWConfig chip number when an IC
    /// declares different 1IC and cascade locations.
    /// </summary>
    public static bool TryReadCmiDpCode(
        string icId,
        ReadOnlySpan<byte> image,
        byte? firmwareConfigChipNumber,
        out CmiDpCodeMetadata metadata)
    {
        metadata = default;
        if (!TryGetCmiDpCodeRule(icId, out CmiDpCodeRule rule))
        {
            return false;
        }

        if (!rule.TryResolveRegister16Offset(firmwareConfigChipNumber, out long register16Offset))
        {
            return false;
        }

        long register18Offset = register16Offset + 2;
        if (register16Offset < 0 || register18Offset >= image.Length)
        {
            return false;
        }

        byte systemCodeLowByte = image[(int)register16Offset];
        byte majorVersionByte = image[(int)register16Offset + 1];
        byte register18Value = image[(int)register18Offset];
        byte minorVersionNibble = (byte)(register18Value >> 4);
        ushort jiraNumber = (ushort)(systemCodeLowByte | ((register18Value & 0x0F) << 8));

        metadata = new CmiDpCodeMetadata(
            rule.IcId,
            systemCodeLowByte,
            majorVersionByte,
            minorVersionNibble,
            jiraNumber,
            register16Offset,
            register18Offset,
            rule.EvidenceSource,
            image.Length,
            rule.ExpectedPayloadLengths);
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

    private static Dictionary<string, CmiDpCodeRule> BuildCmiDpCodeRules()
    {
        return new Dictionary<string, CmiDpCodeRule>(StringComparer.Ordinal)
        {
            ["51923"] = new CmiDpCodeRule("51923", Sizes(0x40000), 0x3E014, Nt51923CmiEvidence),
            ["51926"] = new CmiDpCodeRule("51926", Sizes(0x40000), 0x3E014, Nt51926CmiEvidence),
            ["51927"] = new CmiDpCodeRule("51927", Sizes(0x40000, 0x200000), 0x3C01C, Nt51927CmiEvidence),
            ["51929"] = new CmiDpCodeRule("51929", Sizes(0x40000), 0x401A, Nt51929CmiEvidence),
            ["51932"] = new CmiDpCodeRule("51932", Sizes(0x40000), 0x401A, Nt51932CmiEvidence),
            ["51950"] = new CmiDpCodeRule(
                "51950",
                Sizes(0x40000),
                0x3B016,
                Nt51950CmiEvidence,
                CascadeRegister16Offset: 0x05016),
            ["51951"] = new CmiDpCodeRule("51951", Sizes(0x80000), 0x05016, Nt51951CmiEvidence),
        };
    }

    private static ReadOnlyCollection<int> Sizes(params int[] payloadLengths)
    {
        return Array.AsReadOnly(payloadLengths);
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

/// <summary>
/// CMI DP register location. Reg16h stores Jira bits [7:0], Reg17h stores DP major, and Reg18h
/// stores DP minor in its high nibble and Jira bits [11:8] in its low nibble.
/// </summary>
public sealed record CmiDpCodeRule(
    string IcId,
    IReadOnlyList<int> ExpectedPayloadLengths,
    long Register16Offset,
    string EvidenceSource,
    long? CascadeRegister16Offset = null)
{
    /// <summary>Whether the TP NVT Backup FWConfig ChipNumber is required to select the CMI location.</summary>
    public bool RequiresFirmwareConfigChipNumber => CascadeRegister16Offset is not null;

    /// <summary>Returns whether the payload length has golden evidence for this IC's CMI register location.</summary>
    public bool IsExpectedPayloadLength(int payloadLength)
    {
        return ExpectedPayloadLengths.Contains(payloadLength);
    }

    /// <summary>Resolves the 1IC or cascade CMI register location without guessing a missing chip count.</summary>
    public bool TryResolveRegister16Offset(byte? firmwareConfigChipNumber, out long register16Offset)
    {
        if (CascadeRegister16Offset is null)
        {
            register16Offset = Register16Offset;
            return true;
        }

        if (firmwareConfigChipNumber is not { } chipNumber || chipNumber == 0)
        {
            register16Offset = 0;
            return false;
        }

        register16Offset = chipNumber == 1
            ? Register16Offset
            : CascadeRegister16Offset.Value;
        return true;
    }
}

/// <summary>Decoded CMI DP metadata with independent Jira and DP-version fields.</summary>
public readonly record struct CmiDpCodeMetadata(
    string IcId,
    byte SystemCodeLowByte,
    byte MajorVersionByte,
    byte MinorVersionNibble,
    ushort JiraNumber,
    long Register16Offset,
    long Register18Offset,
    string EvidenceSource,
    int PayloadLength,
    IReadOnlyList<int> ExpectedPayloadLengths)
{
    /// <summary>Whether the decoded Jira number has a displayable AUTO_PRJ badge.</summary>
    public bool HasJiraBadge => JiraNumber != 0;

    /// <summary>Technical Jira badge text, or <see langword="null"/> when Reg16h/18h encode Jira zero.</summary>
    public string? JiraBadge => HasJiraBadge ? $"AUTO_PRJ-{JiraNumber}" : null;

    /// <summary>Whether the raw DP payload length has an owner-approved golden observation.</summary>
    public bool IsExpectedPayloadLength => ExpectedPayloadLengths.Contains(PayloadLength);

    /// <summary>Whether UI should warn that CMI was decoded from an unobserved DP payload length.</summary>
    public bool HasPayloadLengthWarning => !IsExpectedPayloadLength;
}

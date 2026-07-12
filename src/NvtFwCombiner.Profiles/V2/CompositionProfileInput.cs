using System.Text.RegularExpressions;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Closed artifact classes accepted by composition profile slots.</summary>
internal enum CompositionProfileArtifactClass
{
    TpFirmware,
    DpFirmware,
    ReferenceImage,
    CtrlRamReplacement,
    Auxiliary,
}

/// <summary>Closed input binding cardinality.</summary>
internal enum CompositionProfileSlotCardinality
{
    ExactlyOne,
    ZeroOrOne,
    OneOrMore,
}

/// <summary>Closed input length rule kind.</summary>
internal enum CompositionProfileLengthRuleKind
{
    ExactBytes,
    ExactResolvedMapCapacity,
    Bounded,
    NormalDpExtractWithWarning,
    TpMaximum256K,
}

/// <summary>Base value for one normalized input length rule.</summary>
internal abstract record CompositionProfileLengthRule
{
    protected CompositionProfileLengthRule(CompositionProfileLengthRuleKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown input length rule kind.");
        }

        Kind = kind;
    }

    internal CompositionProfileLengthRuleKind Kind { get; }
}

/// <summary>Requires one exact positive input length.</summary>
internal sealed record ExactBytesLengthRule : CompositionProfileLengthRule
{
    internal ExactBytesLengthRule(long bytes)
        : base(CompositionProfileLengthRuleKind.ExactBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);
        Bytes = bytes;
    }

    internal long Bytes { get; }
}

/// <summary>Requires the uniquely resolved map capacity.</summary>
internal sealed record ExactResolvedMapCapacityLengthRule()
    : CompositionProfileLengthRule(CompositionProfileLengthRuleKind.ExactResolvedMapCapacity);

/// <summary>Accepts a closed positive input length interval.</summary>
internal sealed record BoundedLengthRule : CompositionProfileLengthRule
{
    internal BoundedLengthRule(long minimumBytes, long maximumBytes)
        : base(CompositionProfileLengthRuleKind.Bounded)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        if (maximumBytes < minimumBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumBytes),
                maximumBytes,
                "Maximum input length cannot be smaller than its minimum.");
        }

        MinimumBytes = minimumBytes;
        MaximumBytes = maximumBytes;
    }

    internal long MinimumBytes { get; }

    internal long MaximumBytes { get; }
}

/// <summary>Extracts declared Normal DP views and warns when the outer file length differs.</summary>
internal sealed record NormalDpExtractWithWarningLengthRule : CompositionProfileLengthRule
{
    internal NormalDpExtractWithWarningLengthRule(string issueCode)
        : base(CompositionProfileLengthRuleKind.NormalDpExtractWithWarning)
    {
        IssueCode = CompositionProfileValueRules.RequireIssueCode(issueCode, nameof(issueCode));
    }

    internal string IssueCode { get; }
}

/// <summary>Rejects TP firmware larger than the fixed 256 KiB owner limit.</summary>
internal sealed record TpMaximum256KLengthRule()
    : CompositionProfileLengthRule(CompositionProfileLengthRuleKind.TpMaximum256K)
{
    internal const long MaximumBytes = 262144;
}

/// <summary>Closed transient input normalization kind.</summary>
internal enum CompositionProfileInputNormalizationKind
{
    None,
    PadShorter,
    TruncateCtrlRam,
}

/// <summary>Base value for one normalized transient input policy.</summary>
internal abstract record CompositionProfileInputNormalization
{
    protected CompositionProfileInputNormalization(CompositionProfileInputNormalizationKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown input normalization kind.");
        }

        Kind = kind;
    }

    internal CompositionProfileInputNormalizationKind Kind { get; }
}

/// <summary>Preserves immutable input bytes without transient normalization.</summary>
internal sealed record NoInputNormalization()
    : CompositionProfileInputNormalization(CompositionProfileInputNormalizationKind.None);

/// <summary>Pads a shorter transient DP replacement buffer with an evidenced byte.</summary>
internal sealed record PadShorterInputNormalization : CompositionProfileInputNormalization
{
    internal PadShorterInputNormalization(byte fillByte, string evidenceRef)
        : base(CompositionProfileInputNormalizationKind.PadShorter)
    {
        EvidenceRef = CompositionProfileValueRules.RequireId(evidenceRef, nameof(evidenceRef));
        FillByte = fillByte;
    }

    internal byte FillByte { get; }

    internal string EvidenceRef { get; }
}

/// <summary>Truncates only a transient CtrlRAM replacement buffer and emits a warning.</summary>
internal sealed record TruncateCtrlRamInputNormalization : CompositionProfileInputNormalization
{
    internal TruncateCtrlRamInputNormalization(string warningIssueCode, string evidenceRef)
        : base(CompositionProfileInputNormalizationKind.TruncateCtrlRam)
    {
        WarningIssueCode = CompositionProfileValueRules.RequireIssueCode(
            warningIssueCode,
            nameof(warningIssueCode));
        EvidenceRef = CompositionProfileValueRules.RequireId(evidenceRef, nameof(evidenceRef));
    }

    internal string WarningIssueCode { get; }

    internal string EvidenceRef { get; }
}

/// <summary>Immutable map-independent profile input slot and acceptance policy.</summary>
internal sealed partial class CompositionProfileInputSlot
{
    private readonly string[] _acceptedExtensions;

    internal CompositionProfileInputSlot(
        string slotId,
        string role,
        CompositionProfileArtifactClass artifactClass,
        bool required,
        CompositionProfileSlotCardinality cardinality,
        IEnumerable<string> acceptedExtensions,
        CompositionProfileLengthRule lengthRule,
        CompositionProfileInputNormalization normalization)
    {
        SlotId = CompositionProfileValueRules.RequireId(slotId, nameof(slotId));
        Role = CompositionProfileValueRules.RequireId(role, nameof(role));
        if (!Enum.IsDefined(artifactClass))
        {
            throw new ArgumentOutOfRangeException(nameof(artifactClass), artifactClass, "Unknown artifact class.");
        }

        if (!Enum.IsDefined(cardinality))
        {
            throw new ArgumentOutOfRangeException(nameof(cardinality), cardinality, "Unknown slot cardinality.");
        }

        ArgumentNullException.ThrowIfNull(lengthRule);
        ArgumentNullException.ThrowIfNull(normalization);
        ValidateFirmwarePolicy(artifactClass, lengthRule, normalization);
        _acceptedExtensions = SnapshotExtensions(acceptedExtensions);

        ArtifactClass = artifactClass;
        Required = required;
        Cardinality = cardinality;
        AcceptedExtensions = Array.AsReadOnly(_acceptedExtensions);
        LengthRule = lengthRule;
        Normalization = normalization;
    }

    internal string SlotId { get; }

    internal string Role { get; }

    internal CompositionProfileArtifactClass ArtifactClass { get; }

    internal bool Required { get; }

    internal CompositionProfileSlotCardinality Cardinality { get; }

    internal IReadOnlyList<string> AcceptedExtensions { get; }

    internal CompositionProfileLengthRule LengthRule { get; }

    internal CompositionProfileInputNormalization Normalization { get; }

    private static void ValidateFirmwarePolicy(
        CompositionProfileArtifactClass artifactClass,
        CompositionProfileLengthRule lengthRule,
        CompositionProfileInputNormalization normalization)
    {
        if (artifactClass == CompositionProfileArtifactClass.TpFirmware &&
            (lengthRule.Kind != CompositionProfileLengthRuleKind.TpMaximum256K ||
             normalization.Kind != CompositionProfileInputNormalizationKind.None))
        {
            throw new ArgumentException("TP firmware requires the fixed 256 KiB rule without normalization.");
        }

        if (lengthRule.Kind == CompositionProfileLengthRuleKind.TpMaximum256K &&
            artifactClass != CompositionProfileArtifactClass.TpFirmware)
        {
            throw new ArgumentException("The fixed 256 KiB rule is restricted to TP firmware.");
        }

        if (artifactClass == CompositionProfileArtifactClass.DpFirmware &&
            lengthRule.Kind is not CompositionProfileLengthRuleKind.ExactResolvedMapCapacity and
                not CompositionProfileLengthRuleKind.NormalDpExtractWithWarning)
        {
            throw new ArgumentException("DP firmware requires an approved DP length rule.");
        }

        if (artifactClass == CompositionProfileArtifactClass.ReferenceImage &&
            (lengthRule.Kind != CompositionProfileLengthRuleKind.ExactResolvedMapCapacity ||
             normalization.Kind != CompositionProfileInputNormalizationKind.None))
        {
            throw new ArgumentException("Reference images require exact map capacity without normalization.");
        }

        if (normalization.Kind == CompositionProfileInputNormalizationKind.PadShorter &&
            artifactClass != CompositionProfileArtifactClass.DpFirmware)
        {
            throw new ArgumentException("Short-input padding is restricted to DP firmware.");
        }

        if (normalization.Kind == CompositionProfileInputNormalizationKind.TruncateCtrlRam &&
            artifactClass != CompositionProfileArtifactClass.CtrlRamReplacement)
        {
            throw new ArgumentException("CtrlRAM truncation requires a CtrlRAM replacement artifact.");
        }

        if (lengthRule.Kind == CompositionProfileLengthRuleKind.NormalDpExtractWithWarning &&
            (artifactClass != CompositionProfileArtifactClass.DpFirmware ||
             normalization.Kind != CompositionProfileInputNormalizationKind.None))
        {
            throw new ArgumentException("Normal DP extraction warnings cannot normalize input bytes.");
        }
    }

    private static string[] SnapshotExtensions(IEnumerable<string> acceptedExtensions)
    {
        ArgumentNullException.ThrowIfNull(acceptedExtensions);
        string[] snapshot = [.. acceptedExtensions];
        if (snapshot.Length == 0)
        {
            throw new ArgumentException("Input slots require an accepted extension.", nameof(acceptedExtensions));
        }

        if (snapshot.Any(extension => extension is null || !ExtensionPattern().IsMatch(extension)))
        {
            throw new ArgumentException(
                "Accepted extensions must use canonical dot-prefixed form.",
                nameof(acceptedExtensions));
        }

        if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Accepted extensions must be ordinally unique.", nameof(acceptedExtensions));
        }

        Array.Sort(snapshot, StringComparer.Ordinal);
        return snapshot;
    }

    [GeneratedRegex("^\\.[A-Za-z0-9]+$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ExtensionPattern();
}

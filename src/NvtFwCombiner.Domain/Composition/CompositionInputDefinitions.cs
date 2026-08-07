namespace NvtFwCombiner.Domain.Composition;

internal static class CanonicalPolicyValueRules
{
    internal static bool IsCanonicalId(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value[0] is >= 'a' and <= 'z' &&
            value[^1] != '-' &&
            value.Where(static character => character != '-')
                .All(static character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9')) &&
            !value.Contains("--", StringComparison.Ordinal);
    }

    internal static string RequireCanonicalId(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return !IsCanonicalId(value)
            ? throw new ArgumentException("Identifier is not in canonical lowercase form.", parameterName)
            : value;
    }

    internal static string[] SnapshotCanonicalIds(
        IEnumerable<string> values,
        string parameterName,
        bool requireValue)
    {
        string[] snapshot = ImmutableStringSnapshot.Create(
            values,
            parameterName,
            requireValue ? "At least one identifier is required." : null,
            "Identifiers must be non-empty values.",
            "Identifiers must be ordinally unique.");
        foreach (string value in snapshot)
        {
            _ = RequireCanonicalId(value, parameterName);
        }

        return snapshot;
    }

    internal static string RequireIssueCode(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Length < 2 || value[0] is < 'A' or > 'Z' ||
            value.Skip(1).Any(static character =>
                character is not (>= 'A' and <= 'Z') and not (>= '0' and <= '9') and not '_')
            ? throw new ArgumentException("Value is not a canonical issue code.", parameterName)
            : value;
    }
}

/// <summary>Base value for one immutable canonical input length definition.</summary>
public abstract record InputLengthRequirementDefinition
{
    private protected InputLengthRequirementDefinition()
    {
    }
}

/// <summary>Requires the physical capacity selected during map resolution.</summary>
internal sealed record ResolvedMapCapacityInputLengthDefinition()
    : InputLengthRequirementDefinition;

/// <summary>Derives the immutable source snapshot from all compiled reads.</summary>
internal sealed record SourceViewCoverageInputLengthDefinition : InputLengthRequirementDefinition
{
    private readonly long[] _expectedOuterLengths;

    internal SourceViewCoverageInputLengthDefinition(
        IReadOnlyList<long>? expectedOuterLengths = null,
        string? unexpectedOuterLengthIssueCode = null)
    {
        DomainInvariant.Reject(
            expectedOuterLengths is not null && unexpectedOuterLengthIssueCode is null,
            "Expected outer lengths and their warning issue code must be declared together.");

        _expectedOuterLengths = expectedOuterLengths is null
            ? []
            : InputLengthPolicyLimits.SnapshotExpectedOuterLengths(
                expectedOuterLengths,
                nameof(expectedOuterLengths));
        if (unexpectedOuterLengthIssueCode is not null)
        {
            _ = CanonicalPolicyValueRules.RequireIssueCode(
                unexpectedOuterLengthIssueCode,
                nameof(unexpectedOuterLengthIssueCode));
        }

        ExpectedOuterLengths = Array.AsReadOnly(_expectedOuterLengths);
        UnexpectedOuterLengthIssueCode = unexpectedOuterLengthIssueCode;
    }

    internal IReadOnlyList<long> ExpectedOuterLengths { get; }

    internal string? UnexpectedOuterLengthIssueCode { get; }

}

/// <summary>Domain-owned canonical input-slot definition before map-dependent lowering.</summary>
internal sealed class CompositionInputSlotDefinition
{
    private readonly string[] _acceptedExtensions;

    internal CompositionInputSlotDefinition(
        string slotId,
        string role,
        CompiledInputArtifactClass artifactClass,
        bool required,
        CompiledInputSlotCardinality cardinality,
        IEnumerable<string> acceptedExtensions,
        InputLengthRequirementDefinition lengthRequirement,
        CompiledInputNormalization normalization,
        string? notApplicableReason = null,
        bool validateCanonicalPolicy = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ClosedEnum.ThrowIfUndefined(artifactClass, "Unknown input artifact class.");
        ClosedEnum.ThrowIfUndefined(cardinality, "Unknown input slot cardinality.");

        ArgumentNullException.ThrowIfNull(lengthRequirement);
        ArgumentNullException.ThrowIfNull(normalization);
        ValidateArtifactPolicy(artifactClass, lengthRequirement, normalization);
        if (validateCanonicalPolicy)
        {
            ValidateCanonicalPolicy(lengthRequirement, normalization);
        }

        _acceptedExtensions = SnapshotExtensions(acceptedExtensions);
        if (notApplicableReason is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(notApplicableReason);
        }

        SlotId = validateCanonicalPolicy
            ? CanonicalPolicyValueRules.RequireCanonicalId(slotId, nameof(slotId))
            : slotId;
        Role = validateCanonicalPolicy
            ? CanonicalPolicyValueRules.RequireCanonicalId(role, nameof(role))
            : role;
        ArtifactClass = artifactClass;
        Required = required;
        Cardinality = cardinality;
        AcceptedExtensions = Array.AsReadOnly(_acceptedExtensions);
        LengthRequirement = lengthRequirement;
        Normalization = normalization;
        NotApplicableReason = notApplicableReason;
    }

    internal string SlotId { get; }

    internal string Role { get; }

    internal CompiledInputArtifactClass ArtifactClass { get; }

    internal bool Required { get; }

    internal CompiledInputSlotCardinality Cardinality { get; }

    internal IReadOnlyList<string> AcceptedExtensions { get; }

    internal InputLengthRequirementDefinition LengthRequirement { get; }

    internal CompiledInputNormalization Normalization { get; }

    internal string? NotApplicableReason { get; }

    private static string[] SnapshotExtensions(IEnumerable<string> acceptedExtensions)
    {
        ArgumentNullException.ThrowIfNull(acceptedExtensions);
        string[] snapshot = [.. acceptedExtensions];
        DomainInvariant.Reject(
            snapshot.Length == 0 || snapshot.Any(static extension =>
            extension.Length < 2 || extension[0] != '.' ||
            extension.Skip(1).Any(static character => !char.IsAsciiLetterOrDigit(character))),
            "Accepted extensions must use canonical dot-prefixed alphanumeric form.",
            nameof(acceptedExtensions));

        DomainInvariant.Reject(
            snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length,
            "Accepted extensions must be ordinally unique.", nameof(acceptedExtensions));

        Array.Sort(snapshot, StringComparer.Ordinal);
        return snapshot;
    }

    private static void ValidateCanonicalPolicy(
        InputLengthRequirementDefinition lengthRequirement,
        CompiledInputNormalization normalization)
    {
        switch (lengthRequirement)
        {
            case CompiledDeclaredPrefixWithWarningInputLengthRequirement declaredPrefix:
                _ = CanonicalPolicyValueRules.RequireIssueCode(
                    declaredPrefix.ShortInputIssueCode,
                    nameof(declaredPrefix.ShortInputIssueCode));
                _ = CanonicalPolicyValueRules.RequireIssueCode(
                    declaredPrefix.UnexpectedOuterLengthIssueCode,
                    nameof(declaredPrefix.UnexpectedOuterLengthIssueCode));
                break;
            case CompiledSourceViewCoverageInputLengthRequirement sourceView
                when sourceView.UnexpectedOuterLengthIssueCode is { } issueCode:
                _ = CanonicalPolicyValueRules.RequireIssueCode(issueCode, nameof(issueCode));
                break;
            default:
                break;
        }

        switch (normalization)
        {
            case CompiledPadShorterInputNormalization padded:
                _ = CanonicalPolicyValueRules.RequireCanonicalId(padded.EvidenceRef, nameof(padded.EvidenceRef));
                break;
            case CompiledTruncateCtrlRamInputNormalization truncated:
                _ = CanonicalPolicyValueRules.RequireIssueCode(
                    truncated.WarningIssueCode,
                    nameof(truncated.WarningIssueCode));
                _ = CanonicalPolicyValueRules.RequireCanonicalId(
                    truncated.EvidenceRef,
                    nameof(truncated.EvidenceRef));
                break;
            default:
                break;
        }
    }

    private static void ValidateArtifactPolicy(
        CompiledInputArtifactClass artifactClass,
        InputLengthRequirementDefinition lengthRequirement,
        CompiledInputNormalization normalization)
    {
        DomainInvariant.Reject(
            artifactClass == CompiledInputArtifactClass.TpFirmware &&
            (!IsApprovedTpLengthRequirement(lengthRequirement) ||
             normalization.Kind != CompiledInputNormalizationKind.None),
            "TP firmware requires one approved unnormalized section or exact length rule.");

        DomainInvariant.Reject(
            lengthRequirement is CompiledTpMaximum256KInputLengthRequirement &&
            artifactClass != CompiledInputArtifactClass.TpFirmware,
            "The fixed 256 KiB rule is restricted to TP firmware.");

        DomainInvariant.Reject(
            artifactClass == CompiledInputArtifactClass.DpFirmware &&
            lengthRequirement is not (ResolvedMapCapacityInputLengthDefinition or
                CompiledExactResolvedMapCapacityInputLengthRequirement or
                CompiledDeclaredPrefixWithWarningInputLengthRequirement or
                SourceViewCoverageInputLengthDefinition or
                CompiledSourceViewCoverageInputLengthRequirement),
            "DP firmware requires an approved DP length rule.");

        DomainInvariant.Reject(
            artifactClass == CompiledInputArtifactClass.ReferenceImage &&
            (lengthRequirement is not (ResolvedMapCapacityInputLengthDefinition or
                 CompiledExactResolvedMapCapacityInputLengthRequirement) ||
             normalization.Kind != CompiledInputNormalizationKind.None),
            "Reference images require exact map capacity without normalization.");

        DomainInvariant.Reject(
            normalization.Kind == CompiledInputNormalizationKind.PadShorter &&
            artifactClass != CompiledInputArtifactClass.DpFirmware,
            "Short-input padding is restricted to DP firmware.");

        DomainInvariant.Reject(
            normalization.Kind == CompiledInputNormalizationKind.PadShorter &&
            lengthRequirement is not (ResolvedMapCapacityInputLengthDefinition or
                CompiledExactResolvedMapCapacityInputLengthRequirement),
            "Short-input padding requires exact resolved-map capacity.");

        DomainInvariant.Reject(
            normalization.Kind == CompiledInputNormalizationKind.TruncateCtrlRam &&
            artifactClass != CompiledInputArtifactClass.CtrlRamReplacement,
            "CtrlRAM truncation requires a CtrlRAM replacement artifact.");

        DomainInvariant.Reject(
            lengthRequirement is CompiledDeclaredPrefixWithWarningInputLengthRequirement &&
            (artifactClass is CompiledInputArtifactClass.ReferenceImage or
                CompiledInputArtifactClass.CtrlRamReplacement ||
             normalization.Kind != CompiledInputNormalizationKind.None),
            "Declared-prefix input authority is restricted to unnormalized immutable Merge sources.");

        DomainInvariant.Reject(
            lengthRequirement is SourceViewCoverageInputLengthDefinition or
                CompiledSourceViewCoverageInputLengthRequirement &&
            (artifactClass is CompiledInputArtifactClass.ReferenceImage or
                CompiledInputArtifactClass.CtrlRamReplacement ||
             normalization.Kind != CompiledInputNormalizationKind.None),
            "Source-view coverage is restricted to unnormalized immutable section sources.");
    }

    private static bool IsApprovedTpLengthRequirement(InputLengthRequirementDefinition lengthRequirement)
    {
        return lengthRequirement is CompiledTpMaximum256KInputLengthRequirement or
            SourceViewCoverageInputLengthDefinition or
            CompiledSourceViewCoverageInputLengthRequirement or
            CompiledDeclaredPrefixWithWarningInputLengthRequirement
        {
            RequiredEndExclusive: <= CompiledTpMaximum256KInputLengthRequirement.MaximumBytes,
        } or
            CompiledExactBytesInputLengthRequirement
        {
            Bytes: <= CompiledTpMaximum256KInputLengthRequirement.MaximumBytes,
        };
    }
}

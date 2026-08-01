using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>One concrete immutable input binding supplied for a map-bound runtime reference-replace request.</summary>
internal sealed class V2RuntimeReferenceReplaceInputBinding
{
    internal V2RuntimeReferenceReplaceInputBinding(string bindingId, string slotId, long exactLengthBytes)
    {
        BindingId = bindingId;
        SlotId = slotId;
        ExactLengthBytes = exactLengthBytes;
    }

    /// <summary>Concrete immutable address-space identity for this compile request.</summary>
    internal string BindingId { get; }

    /// <summary>Profile slot materialized by this concrete binding.</summary>
    internal string SlotId { get; }

    /// <summary>Exact immutable source capacity expected by the resulting plan.</summary>
    internal long ExactLengthBytes { get; }
}

internal sealed record V2RuntimeReferenceReplaceFirmwareVersionEdit(
    ByteRange SourceFirmwareVersionAndBarRange, ByteRange SourceFirmwareSubVersionRange,
    byte FirmwareVersion, byte FirmwareSubVersion, string InvalidOutputIssueCode,
    string MismatchOutputIssueCode);

/// <summary>Count-resolved postbuild authority and FWConfig Backup placement postcondition.</summary>
internal sealed class V2RuntimeReferenceReplacePostbuildPolicy
{
    private readonly ByteRange[] _requiredNonuniformSourceRanges;

    internal V2RuntimeReferenceReplacePostbuildPolicy(
        string policyId,
        string? sourceAddressSpaceId,
        IEnumerable<ByteRange>? requiredNonuniformSourceRanges,
        string? uniformSourceIssueCode,
        ByteRange declaredProcessorAuthority,
        ByteRange resolvedProcessorAuthority,
        long expectedFirmwareConfigBackupStart,
        long firmwareConfigBackupLength,
        string invalidPlacementIssueCode,
        string inactiveMutationIssueCode,
        string unexpectedPlacementIssueCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId);
        bool hasSourceValidation = sourceAddressSpaceId is not null ||
            requiredNonuniformSourceRanges is not null ||
            uniformSourceIssueCode is not null;
        if (hasSourceValidation)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceAddressSpaceId);
            ArgumentException.ThrowIfNullOrWhiteSpace(uniformSourceIssueCode);
            ArgumentNullException.ThrowIfNull(requiredNonuniformSourceRanges);
        }

        _requiredNonuniformSourceRanges =
            requiredNonuniformSourceRanges is null
                ? []
                : [.. requiredNonuniformSourceRanges];
        if (hasSourceValidation && _requiredNonuniformSourceRanges.Length == 0)
        {
            throw new ArgumentException(
                "Selected Dynamic DiffDLM source requires at least one nonuniform range.",
                nameof(requiredNonuniformSourceRanges));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(expectedFirmwareConfigBackupStart);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(firmwareConfigBackupLength);
        ArgumentException.ThrowIfNullOrWhiteSpace(invalidPlacementIssueCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(inactiveMutationIssueCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(unexpectedPlacementIssueCode);
        var expectedEnvelope = new ByteRange(
            expectedFirmwareConfigBackupStart,
            firmwareConfigBackupLength);
        if (!declaredProcessorAuthority.Contains(resolvedProcessorAuthority) ||
            !resolvedProcessorAuthority.Contains(expectedEnvelope))
        {
            throw new ArgumentException(
                "Resolved postbuild authority must be bounded by its declaration and contain the expected Backup.");
        }

        PolicyId = policyId;
        SourceAddressSpaceId = sourceAddressSpaceId;
        RequiredNonuniformSourceRanges = Array.AsReadOnly(_requiredNonuniformSourceRanges);
        UniformSourceIssueCode = uniformSourceIssueCode;
        DeclaredProcessorAuthority = declaredProcessorAuthority;
        ResolvedProcessorAuthority = resolvedProcessorAuthority;
        ExpectedFirmwareConfigBackupStart = expectedFirmwareConfigBackupStart;
        FirmwareConfigBackupLength = firmwareConfigBackupLength;
        InvalidPlacementIssueCode = invalidPlacementIssueCode;
        InactiveMutationIssueCode = inactiveMutationIssueCode;
        UnexpectedPlacementIssueCode = unexpectedPlacementIssueCode;
    }

    internal string PolicyId { get; }

    internal string? SourceAddressSpaceId { get; }

    internal IReadOnlyList<ByteRange> RequiredNonuniformSourceRanges { get; }

    internal string? UniformSourceIssueCode { get; }

    internal ByteRange DeclaredProcessorAuthority { get; }

    internal ByteRange ResolvedProcessorAuthority { get; }

    internal long ExpectedFirmwareConfigBackupStart { get; }

    internal long FirmwareConfigBackupLength { get; }

    internal string InvalidPlacementIssueCode { get; }

    internal string InactiveMutationIssueCode { get; }

    internal string UnexpectedPlacementIssueCode { get; }
}

/// <summary>Typed map-bound General Replace overlay containing only input lengths and explicit half-open mappings.</summary>
internal sealed class V2RuntimeReferenceReplaceCompileRequest
{
    private readonly V2RuntimeReferenceReplaceInputBinding[] _bindings;
    private readonly ExplicitMapping[] _mappings;
    private readonly ExternalProcessorWriteRangeSection[] _postbuildWriteRangeSections;

    internal V2RuntimeReferenceReplaceCompileRequest(
        IEnumerable<V2RuntimeReferenceReplaceInputBinding> bindings,
        IEnumerable<ExplicitMapping> mappings,
        V2RuntimeReferenceReplaceFirmwareVersionEdit? firmwareVersionEdit = null,
        V2RuntimeReferenceReplacePostbuildPolicy? postbuildPolicy = null,
        IEnumerable<ExternalProcessorWriteRangeSection>? postbuildWriteRangeSections = null)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(mappings);
        _bindings = [.. bindings];
        _mappings = [.. mappings];
        _postbuildWriteRangeSections = [.. postbuildWriteRangeSections ?? []];
        Bindings = Array.AsReadOnly(_bindings);
        Mappings = Array.AsReadOnly(_mappings);
        FirmwareVersionEdit = firmwareVersionEdit;
        PostbuildPolicy = postbuildPolicy;
        PostbuildWriteRangeSections = Array.AsReadOnly(_postbuildWriteRangeSections);
    }

    /// <summary>Concrete immutable inputs with no host paths, source bytes, or process authority.</summary>
    internal IReadOnlyList<V2RuntimeReferenceReplaceInputBinding> Bindings { get; }

    /// <summary>Explicit source-to-output mappings lowered through the shared composition plan algebra.</summary>
    internal IReadOnlyList<ExplicitMapping> Mappings { get; }

    internal V2RuntimeReferenceReplaceFirmwareVersionEdit? FirmwareVersionEdit { get; }

    internal V2RuntimeReferenceReplacePostbuildPolicy? PostbuildPolicy { get; }

    /// <summary>Compiler input annotations for the exact resolved postbuild processor plan.</summary>
    internal IReadOnlyList<ExternalProcessorWriteRangeSection> PostbuildWriteRangeSections { get; }
}

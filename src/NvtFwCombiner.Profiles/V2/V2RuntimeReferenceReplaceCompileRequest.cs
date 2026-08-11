using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

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
internal sealed class V2RuntimeReferenceReplaceCompileRequest : V2ExplicitMappingCompileRequest
{
    internal V2RuntimeReferenceReplaceCompileRequest(
        IEnumerable<V2ExplicitMappingInputBinding> bindings,
        IEnumerable<ExplicitMapping> mappings,
        V2RuntimeReferenceReplaceFirmwareVersionEdit? firmwareVersionEdit = null,
        V2RuntimeReferenceReplacePostbuildPolicy? postbuildPolicy = null,
        IEnumerable<ExternalProcessorWriteRangeSection>? postbuildWriteRangeSections = null,
        ExternalProcessorProtocolPlan? processorProtocolPlan = null)
        : base(bindings, mappings)
    {
        PostbuildWriteRangeSections = Array.AsReadOnly(
            [.. postbuildWriteRangeSections ?? []]);
        FirmwareVersionEdit = firmwareVersionEdit;
        PostbuildPolicy = postbuildPolicy;
        ProcessorProtocolPlan = processorProtocolPlan;
    }

    internal V2RuntimeReferenceReplaceFirmwareVersionEdit? FirmwareVersionEdit { get; }

    internal V2RuntimeReferenceReplacePostbuildPolicy? PostbuildPolicy { get; }

    /// <summary>Compiler input annotations for the exact resolved postbuild processor plan.</summary>
    internal IReadOnlyList<ExternalProcessorWriteRangeSection> PostbuildWriteRangeSections { get; }

    /// <summary>Exact already-selected adapter protocol plan bound into the compiled processor invocation.</summary>
    internal ExternalProcessorProtocolPlan? ProcessorProtocolPlan { get; }
}

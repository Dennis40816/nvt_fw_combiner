using System.Security.Cryptography;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.InputInspection;

/// <summary>Stable generic issue codes emitted by compiled input inspection.</summary>
public static class InputArtifactInspectionIssueCodes
{
    /// <summary>The source matches one compiler-owned expected outer length.</summary>
    public const string Ready = "input.inspection.ready";

    /// <summary>The selected source could not be materialized for inspection.</summary>
    public const string SourceUnreadable = "input.inspection.source-unreadable";

    /// <summary>An accepted AB input has no readable informational version metadata.</summary>
    public const string AbVersionMetadataUnknown = "ab.input.version-unknown";
}

/// <summary>Stable health priority for one input inspected against a compiled contract.</summary>
public enum CompiledInputArtifactInspectionSeverity
{
    /// <summary>The selected source satisfies the compiled input policy.</summary>
    Valid,

    /// <summary>The source is accepted, but the diagnostic must remain visible.</summary>
    Warning,

    /// <summary>The source cannot be used by Build.</summary>
    Blocking,
}

/// <summary>Typed corrective action for a compiled input diagnostic.</summary>
public enum CompiledInputArtifactInspectionNextAction
{
    /// <summary>No corrective action is required.</summary>
    None,

    /// <summary>Select a readable local input source.</summary>
    SelectReadableInput,

    /// <summary>Select an input that reaches the compiled required end.</summary>
    SelectCompatibleInput,

    /// <summary>Review the ignored half-open trailing range before Build.</summary>
    ReviewIgnoredTrailingBytes,

    /// <summary>Review an unexpected accepted outer length.</summary>
    ReviewUnexpectedOuterLength,

    /// <summary>Review informational version metadata that could not be decoded.</summary>
    ReviewUnknownVersion,
}

/// <summary>
/// Path-free diagnostic for one immutable source inspected against one compiled input-space binding.
/// A prior inspection is display evidence only; Build re-reads and revalidates its own binding.
/// </summary>
public sealed record CompiledInputArtifactInspectionResult(
    string AddressSpaceId,
    string SlotId,
    long ActualLength,
    string ActualSha256,
    long RequiredEndExclusive,
    IReadOnlyList<long> ExpectedOuterLengths,
    ByteRange? AcceptedSnapshotRange,
    string? AcceptedSnapshotSha256,
    ByteRange? IgnoredTrailingRange,
    CompiledInputArtifactInspectionSeverity Severity,
    string IssueCode,
    bool BlocksBuild,
    CompiledInputArtifactInspectionNextAction NextAction)
{
    /// <summary>Number of immutable source bytes excluded from the execution snapshot.</summary>
    public long IgnoredTrailingBytes => IgnoredTrailingRange?.Length ?? 0;
}

/// <summary>
/// Inspects an immutable source using only one compiler-owned input contract. Informational IC,
/// filename, PID, version, and hash values cannot select or modify the policy.
/// </summary>
public static class CompiledInputArtifactInspectionService
{
    /// <summary>
    /// Inspects one immutable source using its complete compiled contract and compiler-derived
    /// address-space projection.
    /// </summary>
    public static CompiledInputArtifactInspectionResult Inspect(
        CompiledComposition composition,
        string addressSpaceId,
        ReadOnlyMemory<byte> sourceBytes)
    {
        ArgumentNullException.ThrowIfNull(composition);
        V2CompiledCompositionDetails details = composition.V2Details;
        (CompiledInputSpaceBinding binding, CompiledInputSlotRequirement slot) =
            ResolveBinding(details.InputContract, addressSpaceId);
        AddressSpace addressSpace = composition.Plan.AddressSpaces.Single(candidate =>
            StringComparer.Ordinal.Equals(candidate.AddressSpaceId, binding.AddressSpaceId));
        CompiledInputArtifactInspectionResult inspection =
            slot.Normalization is CompiledTruncateCtrlRamInputNormalization truncation &&
            slot.LengthRequirement is
                CompiledBoundedInputLengthRequirement or
                CompiledExactBytesInputLengthRequirement
            ? InspectTruncatedCtrlRam(
                binding,
                slot,
                truncation,
                addressSpace,
                sourceBytes)
            : slot.LengthRequirement switch
            {
                CompiledSourceViewCoverageInputLengthRequirement { RequiredEndExclusive: not null } sourceView =>
                    InspectDeclaredPrefix(binding, slot, sourceView, sourceBytes),
                CompiledSourceViewCoverageInputLengthRequirement sourceView =>
                    InspectSourceView(binding, slot, sourceView, addressSpace, sourceBytes),
                CompiledExactBytesInputLengthRequirement exact =>
                    InspectExact(binding, slot, exact.Bytes, sourceBytes),
                CompiledExactResolvedMapCapacityInputLengthRequirement exact =>
                    InspectExact(binding, slot, exact.Bytes, sourceBytes),
                _ => throw new ArgumentException(
                    $"Compiled input address space '{addressSpaceId}' has no supported inspection projection.",
                    nameof(addressSpaceId)),
            };
        return ApplyInputLoadValidation(composition, addressSpaceId, sourceBytes, inspection);
    }

    private static CompiledInputArtifactInspectionResult InspectDeclaredPrefix(
        CompiledInputSpaceBinding binding,
        CompiledInputSlotRequirement slot,
        CompiledSourceViewCoverageInputLengthRequirement requirement,
        ReadOnlyMemory<byte> sourceBytes)
    {
        return InspectDeclaredPrefix(
            binding,
            slot,
            requirement.RequiredEndExclusive!.Value,
            requirement.ExpectedOuterLengths,
            requirement.ShortInputIssueCode!,
            requirement.UnexpectedOuterLengthIssueCode!,
            sourceBytes);
    }

    private static CompiledInputArtifactInspectionResult InspectTruncatedCtrlRam(
        CompiledInputSpaceBinding binding,
        CompiledInputSlotRequirement slot,
        CompiledTruncateCtrlRamInputNormalization truncation,
        AddressSpace addressSpace,
        ReadOnlyMemory<byte> sourceBytes)
    {
        return slot.ArtifactClass != CompiledInputArtifactClass.CtrlRamReplacement ||
            addressSpace.InputOversizePolicy != InputOversizePolicy.TruncateWithWarning
                ? throw new ArgumentException(
                    "CtrlRAM prefix inspection requires the compiled CtrlRAM truncation contract.",
                    nameof(addressSpace))
                : InspectDeclaredPrefix(
                    binding,
                    slot,
                    addressSpace.Length,
                    [addressSpace.Length],
                    CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                    truncation.WarningIssueCode,
                    sourceBytes);
    }

    private static CompiledInputArtifactInspectionResult InspectDeclaredPrefix(
        CompiledInputSpaceBinding binding,
        CompiledInputSlotRequirement slot,
        long requiredEndExclusive,
        IEnumerable<long> expectedOuterLengths,
        string shortInputIssueCode,
        string unexpectedOuterLengthIssueCode,
        ReadOnlyMemory<byte> sourceBytes)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(requiredEndExclusive, int.MaxValue);
        byte[] actualSnapshot = sourceBytes.ToArray();
        string actualSha256 = Convert.ToHexStringLower(SHA256.HashData(actualSnapshot));
        IReadOnlyList<long> expectedOuterLengthSnapshot =
            Array.AsReadOnly([.. expectedOuterLengths]);
        if (actualSnapshot.LongLength < requiredEndExclusive)
        {
            return new CompiledInputArtifactInspectionResult(
                binding.AddressSpaceId,
                slot.SlotId,
                actualSnapshot.LongLength,
                actualSha256,
                requiredEndExclusive,
                expectedOuterLengthSnapshot,
                AcceptedSnapshotRange: null,
                AcceptedSnapshotSha256: null,
                IgnoredTrailingRange: null,
                CompiledInputArtifactInspectionSeverity.Blocking,
                shortInputIssueCode,
                BlocksBuild: true,
                CompiledInputArtifactInspectionNextAction.SelectCompatibleInput);
        }

        int acceptedLength = checked((int)requiredEndExclusive);
        var acceptedRange = new ByteRange(0, requiredEndExclusive);
        string acceptedSha256 = Convert.ToHexStringLower(SHA256.HashData(
            actualSnapshot.AsSpan(0, acceptedLength)));
        ByteRange? ignoredTrailingRange = actualSnapshot.LongLength > requiredEndExclusive
            ? ByteRange.FromStartEndExclusive(requiredEndExclusive, actualSnapshot.LongLength)
            : null;
        bool expectedOuterLength = expectedOuterLengthSnapshot.Contains(actualSnapshot.LongLength);
        return new CompiledInputArtifactInspectionResult(
            binding.AddressSpaceId,
            slot.SlotId,
            actualSnapshot.LongLength,
            actualSha256,
            requiredEndExclusive,
            expectedOuterLengthSnapshot,
            acceptedRange,
            acceptedSha256,
            ignoredTrailingRange,
            expectedOuterLength
                ? CompiledInputArtifactInspectionSeverity.Valid
                : CompiledInputArtifactInspectionSeverity.Warning,
            expectedOuterLength
                ? InputArtifactInspectionIssueCodes.Ready
                : unexpectedOuterLengthIssueCode,
            BlocksBuild: false,
            expectedOuterLength
                ? CompiledInputArtifactInspectionNextAction.None
                : ignoredTrailingRange.HasValue
                    ? CompiledInputArtifactInspectionNextAction.ReviewIgnoredTrailingBytes
                    : CompiledInputArtifactInspectionNextAction.ReviewUnexpectedOuterLength);
    }

    private static CompiledInputArtifactInspectionResult ApplyInputLoadValidation(
        CompiledComposition composition,
        string addressSpaceId,
        ReadOnlyMemory<byte> sourceBytes,
        CompiledInputArtifactInspectionResult inspection)
    {
        if (inspection.BlocksBuild)
        {
            return inspection;
        }

        CompiledUniformInputRangeValidation? failed = composition.V2Details.Provenance.ValidationRequirements
            .OfType<CompiledUniformInputRangeValidation>()
            .Where(requirement => StringComparer.Ordinal.Equals(
                requirement.AddressSpaceId,
                addressSpaceId))
            .Where(static requirement => requirement.Severity != CompiledValidationSeverity.Info)
            .OrderByDescending(static requirement => requirement.Severity)
            .FirstOrDefault(requirement =>
                CompiledInputLoadValidationEvaluator.Evaluate(sourceBytes.Span, requirement) is not null);
        bool blocksBuild = failed?.Severity == CompiledValidationSeverity.Error;
        return failed is null || (!blocksBuild &&
                inspection.Severity != CompiledInputArtifactInspectionSeverity.Valid)
            ? inspection
            : inspection with
            {
                Severity = blocksBuild
                    ? CompiledInputArtifactInspectionSeverity.Blocking
                    : CompiledInputArtifactInspectionSeverity.Warning,
                IssueCode = failed.IssueCode,
                BlocksBuild = blocksBuild,
                NextAction = CompiledInputArtifactInspectionNextAction.None,
            };
    }

    private static (CompiledInputSpaceBinding Binding, CompiledInputSlotRequirement Slot) ResolveBinding(
        CompiledInputContract inputContract,
        string addressSpaceId)
    {
        ArgumentNullException.ThrowIfNull(inputContract);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        CompiledInputSpaceBinding binding = inputContract.SpaceBindings.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.AddressSpaceId, addressSpaceId)) ??
            throw new ArgumentException(
                $"Compiled input contract does not declare address space '{addressSpaceId}'.",
                nameof(addressSpaceId));
        CompiledInputSlotRequirement slot = inputContract.Slots.Single(candidate =>
            StringComparer.Ordinal.Equals(candidate.SlotId, binding.SlotId));
        return (binding, slot);
    }

    private static CompiledInputArtifactInspectionResult InspectExact(
        CompiledInputSpaceBinding binding,
        CompiledInputSlotRequirement slot,
        long expectedLength,
        ReadOnlyMemory<byte> sourceBytes)
    {
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(sourceBytes.Span));
        bool matches = sourceBytes.Length == expectedLength;
        return new CompiledInputArtifactInspectionResult(
            binding.AddressSpaceId,
            slot.SlotId,
            sourceBytes.Length,
            sha256,
            expectedLength,
            [expectedLength],
            matches ? new ByteRange(0, expectedLength) : null,
            matches ? sha256 : null,
            IgnoredTrailingRange: null,
            matches
                ? CompiledInputArtifactInspectionSeverity.Valid
                : CompiledInputArtifactInspectionSeverity.Blocking,
            matches
                ? InputArtifactInspectionIssueCodes.Ready
                : CompositionIssueCodes.InputAddressSpaceLengthMismatch,
            BlocksBuild: !matches,
            matches
                ? CompiledInputArtifactInspectionNextAction.None
                : CompiledInputArtifactInspectionNextAction.SelectCompatibleInput);
    }

    private static CompiledInputArtifactInspectionResult InspectSourceView(
        CompiledInputSpaceBinding binding,
        CompiledInputSlotRequirement slot,
        CompiledSourceViewCoverageInputLengthRequirement requirement,
        AddressSpace addressSpace,
        ReadOnlyMemory<byte> sourceBytes)
    {
        string actualSha256 = Convert.ToHexStringLower(SHA256.HashData(sourceBytes.Span));
        long requiredEndExclusive = addressSpace.Length;
        bool tooLong = requirement.MaximumBytes is { } maximumBytes && sourceBytes.Length > maximumBytes;
        if (sourceBytes.Length < requiredEndExclusive || tooLong)
        {
            return new CompiledInputArtifactInspectionResult(
                binding.AddressSpaceId,
                slot.SlotId,
                sourceBytes.Length,
                actualSha256,
                requiredEndExclusive,
                requirement.ExpectedOuterLengths,
                AcceptedSnapshotRange: null,
                AcceptedSnapshotSha256: null,
                IgnoredTrailingRange: null,
                CompiledInputArtifactInspectionSeverity.Blocking,
                tooLong
                    ? CompositionIssueCodes.InputAddressSpaceLengthMismatch
                    : CompositionIssueCodes.InputSourceViewIncomplete,
                BlocksBuild: true,
                CompiledInputArtifactInspectionNextAction.SelectCompatibleInput);
        }

        var acceptedRange = new ByteRange(0, requiredEndExclusive);
        string acceptedSha256 = Convert.ToHexStringLower(SHA256.HashData(
            sourceBytes.Span[..checked((int)requiredEndExclusive)]));
        ByteRange? ignoredTrailingRange = sourceBytes.Length > requiredEndExclusive
            ? new ByteRange(requiredEndExclusive, sourceBytes.Length - requiredEndExclusive)
            : null;
        bool unexpectedOuterLength = requirement.ExpectedOuterLengths.Count > 0 &&
            !requirement.ExpectedOuterLengths.Contains(sourceBytes.Length);
        return new CompiledInputArtifactInspectionResult(
            binding.AddressSpaceId,
            slot.SlotId,
            sourceBytes.Length,
            actualSha256,
            requiredEndExclusive,
            requirement.ExpectedOuterLengths,
            acceptedRange,
            acceptedSha256,
            ignoredTrailingRange,
            unexpectedOuterLength
                ? CompiledInputArtifactInspectionSeverity.Warning
                : CompiledInputArtifactInspectionSeverity.Valid,
            unexpectedOuterLength
                ? requirement.UnexpectedOuterLengthIssueCode!
                : InputArtifactInspectionIssueCodes.Ready,
            BlocksBuild: false,
            unexpectedOuterLength
                ? CompiledInputArtifactInspectionNextAction.ReviewUnexpectedOuterLength
                : CompiledInputArtifactInspectionNextAction.None);
    }

}

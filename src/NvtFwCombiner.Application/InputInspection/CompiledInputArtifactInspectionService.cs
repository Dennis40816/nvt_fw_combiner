using System.Security.Cryptography;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.InputInspection;

/// <summary>Stable generic issue codes emitted by compiled input inspection.</summary>
public static class InputArtifactInspectionIssueCodes
{
    /// <summary>A canonical TP FWConfig declares a zero IC Count.</summary>
    public const string TpChipCountRequired = FirmwareConfigChipCountDiagnostics.RequiredIssueCode;

    /// <summary>A canonical TP FWConfig IC Count cannot be read.</summary>
    public const string TpChipCountUnreadable = FirmwareConfigChipCountDiagnostics.UnreadableIssueCode;

    /// <summary>The source matches one compiler-owned expected outer length.</summary>
    public const string Ready = "input.inspection.ready";

    /// <summary>The selected source could not be materialized for inspection.</summary>
    public const string SourceUnreadable = "input.inspection.source-unreadable";

    /// <summary>The selected file name does not satisfy the compiler-owned extension contract.</summary>
    public const string ExtensionNotAccepted = "input.inspection.extension-not-accepted";

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
/// Fixed-workflow Build revalidates this accepted immutable snapshot through its exact compiled binding.
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
    /// <summary>Optional path-free evidence from the same compiled validation evaluation.</summary>
    public InputDiagnosticEvidence? DiagnosticEvidence { get; init; }

    /// <summary>Typed blocking TP admission cause, retaining source geometry for diagnostics only.</summary>
    public CompositionIssue? AdmissionIssue { get; init; }

    /// <summary>Number of immutable source bytes excluded from the execution snapshot.</summary>
    public long IgnoredTrailingBytes => IgnoredTrailingRange?.Length ?? 0;
}

/// <summary>
/// Inspects an immutable source using only one compiler-owned input contract. Informational IC,
/// filename, PID, version, and hash values cannot select or modify the policy.
/// </summary>
public static class CompiledInputArtifactInspectionService
{
    /// <summary>Inclusive fixed-workflow complete-file read ceiling (decimal 100 MB).</summary>
    public const long MaximumContentReadBytes = 100_000_000;

    /// <summary>
    /// Resolves the inclusive complete-file read ceiling from one compiler-owned input binding.
    /// Resource admission remains distinct from firmware geometry and normalization.
    /// </summary>
    public static long ResolveMaximumContentReadBytes(
        CompiledComposition composition,
        string addressSpaceId)
    {
        ArgumentNullException.ThrowIfNull(composition);
        (CompiledInputSpaceBinding _, CompiledInputSlotRequirement slot) = ResolveBinding(
            composition.V2Details.InputContract,
            addressSpaceId);
        if (slot.Normalization is CompiledTruncateCtrlRamInputNormalization)
        {
            return MaximumContentReadBytes;
        }

        long declaredMaximum = slot.LengthRequirement switch
        {
            CompiledExactBytesInputLengthRequirement exact => exact.Bytes,
            CompiledExactResolvedMapCapacityInputLengthRequirement exact => exact.Bytes,
            CompiledBoundedInputLengthRequirement bounded => bounded.MaximumBytes,
            CompiledSourceViewCoverageInputLengthRequirement { MaximumBytes: { } maximum } => maximum,
            CompiledSourceViewCoverageInputLengthRequirement => MaximumContentReadBytes,
            _ => throw new InvalidOperationException(
                "Unknown compiled input length requirement for content-read admission."),
        };
        return Math.Min(MaximumContentReadBytes, declaredMaximum);
    }

    /// <summary>
    /// Returns whether one original file name satisfies the compiled slot's extension contract.
    /// This is admission policy; picker filters remain presentation-only guidance.
    /// </summary>
    internal static bool AcceptsOriginalFileName(
        CompiledComposition composition,
        string addressSpaceId,
        string originalFileName)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        (_, CompiledInputSlotRequirement slot) = ResolveBinding(
            composition.V2Details.InputContract,
            addressSpaceId);
        string extension = Path.GetExtension(originalFileName);
        return slot.AcceptedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

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
        inspection = ApplySourceEnvelopeWarning(details, slot, inspection);
        return !inspection.BlocksBuild && slot.ArtifactClass == CompiledInputArtifactClass.TpFirmware &&
            inspection.AcceptedSnapshotRange is { } accepted &&
            FirmwareConfigChipCountDiagnostics.AssessPositive(
                sourceBytes.Span.Slice(checked((int)accepted.Start), checked((int)accepted.Length)), addressSpaceId, out _) is { } countIssue
            ? inspection with
            {
                Severity = CompiledInputArtifactInspectionSeverity.Blocking,
                IssueCode = countIssue.Code,
                AdmissionIssue = countIssue,
                BlocksBuild = true,
                NextAction = CompiledInputArtifactInspectionNextAction.SelectCompatibleInput,
            }
            : CompiledReferenceBankInspection.Inspect(composition, addressSpaceId, sourceBytes,
                ApplyInputLoadValidation(composition, addressSpaceId, sourceBytes, inspection));
    }

    private static CompiledInputArtifactInspectionResult ApplySourceEnvelopeWarning(
        V2CompiledCompositionDetails details,
        CompiledInputSlotRequirement slot,
        CompiledInputArtifactInspectionResult inspection)
    {
        SourceEnvelopeExtent? envelope = details.Provenance.Context switch
        {
            ResolvedMapV2CompilationContext resolved => resolved.SourceEnvelope,
            RuntimeReferenceReplaceV2CompilationContext runtime => runtime.SourceEnvelope,
            _ => null,
        };
        return envelope is null ||
            !StringComparer.Ordinal.Equals(slot.SlotId, envelope.SourceSlotId) ||
            inspection.Severity != CompiledInputArtifactInspectionSeverity.Valid ||
            envelope.ExpectedOuterLengths.Contains(inspection.ActualLength)
            ? inspection
            : inspection with
            {
                ExpectedOuterLengths = envelope.ExpectedOuterLengths,
                Severity = CompiledInputArtifactInspectionSeverity.Warning,
                IssueCode = envelope.UnexpectedLengthIssueCode,
                NextAction = CompiledInputArtifactInspectionNextAction.ReviewUnexpectedOuterLength,
            };
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
                CompiledInputArtifactInspectionNextAction.SelectCompatibleInput)
            {
                DiagnosticEvidence = new InputDiagnosticEvidence(
                    binding.AddressSpaceId,
                    actualSnapshot.LongLength,
                    requiredEndExclusive,
                    sourceRange: null,
                    repeatedByte: null),
            };
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

        InputLoadValidationEvaluationResult? failed = composition.V2Details.Provenance.ValidationRequirements
            .OfType<CompiledUniformInputRangeValidation>()
            .Where(requirement => StringComparer.Ordinal.Equals(
                requirement.AddressSpaceId,
                addressSpaceId))
            .Where(static requirement => requirement.Severity != CompiledValidationSeverity.Info)
            .OrderByDescending(static requirement => requirement.Severity)
            .Select(requirement => CompiledInputLoadValidationEvaluator.Evaluate(sourceBytes.Span, requirement))
            .FirstOrDefault(static evaluation => evaluation.Issue is not null);
        bool blocksBuild = failed?.Issue?.Severity == CompositionIssueSeverity.Error;
        return failed is null || (!blocksBuild &&
                inspection.Severity != CompiledInputArtifactInspectionSeverity.Valid)
            ? inspection
            : inspection with
            {
                Severity = blocksBuild
                    ? CompiledInputArtifactInspectionSeverity.Blocking
                    : CompiledInputArtifactInspectionSeverity.Warning,
                IssueCode = failed.Issue!.Code,
                AdmissionIssue = blocksBuild ? failed.Issue : null,
                BlocksBuild = blocksBuild,
                NextAction = CompiledInputArtifactInspectionNextAction.None,
                DiagnosticEvidence = failed.DiagnosticEvidence,
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
                CompiledInputArtifactInspectionNextAction.SelectCompatibleInput)
            {
                DiagnosticEvidence = tooLong
                    ? null
                    : new InputDiagnosticEvidence(
                        binding.AddressSpaceId,
                        sourceBytes.Length,
                        requiredEndExclusive,
                        sourceRange: null,
                        repeatedByte: null),
            };
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

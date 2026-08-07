using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private async ValueTask<BoundInputs> ReadInputsAsync(
        CompositionRunRequest request,
        CancellationToken cancellationToken)
    {
        Dictionary<string, byte[]> inputBytes = new(StringComparer.Ordinal);
        Dictionary<string, ArtifactReadSnapshot> artifactSnapshots = new(StringComparer.Ordinal);
        List<InputArtifactSummary> inputSummaries = [];
        List<CompositionIssue> issues = ValidateArtifactBindings(request);
        if (issues.Count > 0)
        {
            return new BoundInputs(
                inputBytes,
                inputSummaries,
                issues,
                [],
                CreateSkippedInputLoadValidations(request.CompiledComposition));
        }

        foreach (string addressSpaceId in request.CompiledComposition.Plan.RequiredInputAddressSpaceIds)
        {
            await ReadRequiredBindingAsync(
                    request,
                    addressSpaceId,
                    "input.binding.missing",
                    inputBytes,
                    inputSummaries,
                    artifactSnapshots,
                    issues,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        ValidateV2InputLengthRequirements(request, inputBytes, issues);
        List<InputLoadValidationEvaluation> inputLoadValidations =
            issues.Count == 0
                ? EvaluateInputLoad(request.CompiledComposition, inputBytes)
                : CreateSkippedInputLoadValidations(request.CompiledComposition);
        List<CompositionIssue> advisoryIssues =
        [
            .. inputLoadValidations
                .Where(static evaluation =>
                    evaluation.Issue is { Severity: not CompositionIssueSeverity.Error })
                .Select(static evaluation => evaluation.Issue!),
        ];
        issues.AddRange(inputLoadValidations
            .Where(static evaluation =>
                evaluation.Issue is { Severity: CompositionIssueSeverity.Error })
            .Select(static evaluation => evaluation.Issue!));
        if (issues.Count == 0)
        {
            ValidateAbMergeTopologyMetadata(request, inputBytes, issues);
        }

        return new BoundInputs(
            inputBytes,
            inputSummaries,
            issues,
            advisoryIssues,
            inputLoadValidations);
    }

    private static void ValidateV2InputLengthRequirements(
        CompositionRunRequest request,
        Dictionary<string, byte[]> inputBytes,
        List<CompositionIssue> issues)
    {
        V2CompiledCompositionDetails details = request.CompiledComposition.V2Details;

        if (details.Provenance.Context is LogicalOutputV2CompilationContext)
        {
            var addressSpaces = request.CompiledComposition.Plan.AddressSpaces.ToDictionary(
                static addressSpace => addressSpace.AddressSpaceId,
                StringComparer.Ordinal);
            foreach (CompiledInputSpaceBinding binding in details.InputContract.SpaceBindings)
            {
                if (!inputBytes.TryGetValue(binding.AddressSpaceId, out byte[]? bytes) ||
                    bytes.LongLength == addressSpaces[binding.AddressSpaceId].Length)
                {
                    continue;
                }

                issues.Add(new CompositionIssue(
                    CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                    $"Input bytes for logical binding '{binding.AddressSpaceId}' must exactly match its compiled length (actual {bytes.LongLength} bytes, expected {addressSpaces[binding.AddressSpaceId].Length} bytes).",
                    binding.AddressSpaceId));
            }

            return;
        }

        var slots = details.InputContract.Slots.ToDictionary(
            static slot => slot.SlotId,
            StringComparer.Ordinal);
        var compiledAddressSpaces = request.CompiledComposition.Plan.AddressSpaces.ToDictionary(
            static addressSpace => addressSpace.AddressSpaceId,
            StringComparer.Ordinal);
        foreach (CompiledInputSpaceBinding binding in details.InputContract.SpaceBindings)
        {
            if (!inputBytes.TryGetValue(binding.AddressSpaceId, out byte[]? bytes))
            {
                continue;
            }

            CompiledInputSlotRequirement slot = slots[binding.SlotId];
            switch (slot.LengthRequirement)
            {
                case CompiledExactBytesInputLengthRequirement exact
                    when slot.Normalization is CompiledNoInputNormalization &&
                         bytes.LongLength != exact.Bytes:
                    issues.Add(new CompositionIssue(
                        CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                        $"Input bytes for address space '{binding.AddressSpaceId}' must exactly match the compiled length (actual {bytes.LongLength} bytes, expected {exact.Bytes} bytes).",
                        binding.AddressSpaceId));
                    break;
                case CompiledExactResolvedMapCapacityInputLengthRequirement exact
                    when slot.Normalization is CompiledNoInputNormalization &&
                         bytes.LongLength != exact.Bytes:
                    issues.Add(new CompositionIssue(
                        CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                        $"Input bytes for address space '{binding.AddressSpaceId}' must exactly match the resolved-map capacity (actual {bytes.LongLength} bytes, expected {exact.Bytes} bytes).",
                        binding.AddressSpaceId));
                    break;
                case CompiledSourceViewCoverageInputLengthRequirement { MaximumBytes: { } maximumBytes }
                    when bytes.LongLength > maximumBytes:
                    issues.Add(new CompositionIssue(
                        CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                        $"Input bytes for address space '{binding.AddressSpaceId}' exceed the 256 KiB maximum (actual {bytes.LongLength} bytes, maximum {maximumBytes} bytes).",
                        binding.AddressSpaceId));
                    break;
                case CompiledSourceViewCoverageInputLengthRequirement
                { RequiredEndExclusive: { } declaredEnd } declaredPrefix when bytes.LongLength < declaredEnd:
                    issues.Add(new CompositionIssue(
                        declaredPrefix.ShortInputIssueCode!,
                        $"Input bytes for address space '{binding.AddressSpaceId}' end at 0x{bytes.LongLength:X}, before required end 0x{declaredEnd:X}; no padding is authorized.",
                        binding.AddressSpaceId));
                    break;
                case CompiledSourceViewCoverageInputLengthRequirement
                    when bytes.LongLength < compiledAddressSpaces[binding.AddressSpaceId].Length:
                    long requiredEndExclusive = compiledAddressSpaces[binding.AddressSpaceId].Length;
                    issues.Add(new CompositionIssue(
                        CompositionIssueCodes.InputSourceViewIncomplete,
                        $"Input bytes for address space '{binding.AddressSpaceId}' end at " +
                        $"0x{bytes.LongLength:X}, before the final compiled source read at " +
                        $"0x{requiredEndExclusive:X}; select a section or compatible FlashCode " +
                        "that covers the complete source view.",
                        binding.AddressSpaceId));
                    break;
                default:
                    break;
            }
        }
    }

    private static List<CompositionIssue> ValidateArtifactBindings(CompositionRunRequest request)
    {
        var addressSpaces = request.CompiledComposition.Plan.AddressSpaces.ToDictionary(
            addressSpace => addressSpace.AddressSpaceId,
            StringComparer.Ordinal);
        List<CompositionIssue> issues = [];
        foreach (InputArtifactBinding binding in request.ArtifactBindings.Values.OrderBy(
                     binding => binding.AddressSpaceId,
                     StringComparer.Ordinal))
        {
            if (!addressSpaces.TryGetValue(binding.AddressSpaceId, out AddressSpace? addressSpace))
            {
                issues.Add(new CompositionIssue(
                    CompositionIssueCodes.InputAddressSpaceUnknown,
                    $"Artifact binding '{binding.BindingId}' targets undeclared address space '{binding.AddressSpaceId}'.",
                    binding.AddressSpaceId));
            }
            else if (addressSpace.Mutability == AddressSpaceMutability.Mutable)
            {
                issues.Add(new CompositionIssue(
                    CompositionIssueCodes.InputMutableAddressSpaceNotAllowed,
                    $"Artifact binding '{binding.BindingId}' targets engine-owned mutable address space '{binding.AddressSpaceId}'.",
                    binding.AddressSpaceId));
            }
        }

        return issues;
    }

    private async ValueTask ReadRequiredBindingAsync(
        CompositionRunRequest request,
        string addressSpaceId,
        string missingIssueCode,
        Dictionary<string, byte[]> inputBytes,
        List<InputArtifactSummary> inputSummaries,
        Dictionary<string, ArtifactReadSnapshot> artifactSnapshots,
        List<CompositionIssue> issues,
        CancellationToken cancellationToken)
    {
        if (!request.ArtifactBindings.TryGetValue(addressSpaceId, out InputArtifactBinding? binding))
        {
            issues.Add(new CompositionIssue(
                missingIssueCode,
                $"No artifact binding was supplied for required address space '{addressSpaceId}'."));
            return;
        }

        await TryReadBindingAsync(
                binding,
                request.CompiledComposition,
                inputBytes,
                inputSummaries,
                artifactSnapshots,
                issues,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask TryReadBindingAsync(
        InputArtifactBinding binding,
        CompiledComposition composition,
        Dictionary<string, byte[]> inputBytes,
        List<InputArtifactSummary> inputSummaries,
        Dictionary<string, ArtifactReadSnapshot> artifactSnapshots,
        List<CompositionIssue> issues,
        CancellationToken cancellationToken)
    {
        try
        {
            byte[] buffer;
            string sha256;
            if (artifactSnapshots.TryGetValue(binding.ArtifactId, out ArtifactReadSnapshot? snapshot))
            {
                buffer = [.. snapshot.Bytes];
                sha256 = snapshot.Sha256;
            }
            else
            {
                ReadOnlyMemory<byte> bytes = await _artifactReader
                    .ReadAsync(binding.ArtifactId, cancellationToken)
                    .ConfigureAwait(false);
                buffer = bytes.ToArray();
                sha256 = ToSha256Hex(buffer);
                artifactSnapshots.Add(binding.ArtifactId, new ArtifactReadSnapshot(buffer, sha256));
            }

            if (binding.AcceptedContentStamp is { } acceptedStamp &&
                acceptedStamp != new FileStamp(buffer.LongLength, sha256))
            {
                issues.Add(new CompositionIssue(
                    CompositionRunIssueCodes.InputArtifactContentSnapshotMismatch,
                    $"Artifact binding '{binding.BindingId}' no longer matches its accepted length and SHA-256.",
                    binding.AddressSpaceId));
                return;
            }

            inputBytes.Add(binding.AddressSpaceId, buffer);
            InputArtifactExecutionSnapshotSummary? executionSnapshot =
                TryCreateExecutionSnapshotSummary(composition, binding.AddressSpaceId, buffer);

            inputSummaries.Add(new InputArtifactSummary(
                binding.AddressSpaceId,
                binding.BindingId,
                buffer.LongLength,
                sha256,
                binding.OriginalFileName,
                executionSnapshot));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            issues.Add(new CompositionIssue(
                "input.artifact.read-failed",
                $"Unable to read artifact binding '{binding.BindingId}' ({exception.GetType().Name}).",
                binding.AddressSpaceId));
        }
    }

    private static InputArtifactExecutionSnapshotSummary? TryCreateExecutionSnapshotSummary(
        CompiledComposition composition,
        string addressSpaceId,
        ReadOnlyMemory<byte> sourceBytes)
    {
        V2CompiledCompositionDetails details = composition.V2Details;
        if (details.Provenance.Context is LogicalOutputV2CompilationContext)
        {
            return null;
        }

        CompiledInputSpaceBinding? spaceBinding = details.InputContract.SpaceBindings.SingleOrDefault(
            binding => StringComparer.Ordinal.Equals(binding.AddressSpaceId, addressSpaceId));
        if (spaceBinding is null)
        {
            return null;
        }

        CompiledInputSlotRequirement? slot = details.InputContract.Slots.SingleOrDefault(
            candidate => StringComparer.Ordinal.Equals(candidate.SlotId, spaceBinding.SlotId));
        if (slot?.LengthRequirement is not (
                CompiledSourceViewCoverageInputLengthRequirement or
                CompiledExactBytesInputLengthRequirement or
                CompiledExactResolvedMapCapacityInputLengthRequirement))
        {
            return null;
        }

        CompiledInputArtifactInspectionResult inspection =
            CompiledInputArtifactInspectionService.Inspect(
                composition,
                addressSpaceId,
                sourceBytes);
        return inspection.AcceptedSnapshotRange is { } acceptedRange &&
               inspection.AcceptedSnapshotSha256 is { } acceptedSha256
            ? new InputArtifactExecutionSnapshotSummary(
                acceptedRange,
                acceptedSha256,
                inspection.IgnoredTrailingRange)
            : null;
    }

    private sealed record BoundInputs(
        IReadOnlyDictionary<string, byte[]> InputBytes,
        IReadOnlyList<InputArtifactSummary> InputSummaries,
        IReadOnlyList<CompositionIssue> Issues,
        IReadOnlyList<CompositionIssue> AdvisoryIssues,
        IReadOnlyList<InputLoadValidationEvaluation> InputLoadValidations);

    private sealed record ArtifactReadSnapshot(byte[] Bytes, string Sha256);
}

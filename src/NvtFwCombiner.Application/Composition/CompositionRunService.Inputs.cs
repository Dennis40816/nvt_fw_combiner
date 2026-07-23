using NvtFwCombiner.Application.InputInspection;
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
            return new BoundInputs(inputBytes, inputSummaries, issues);
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
        if (issues.Count == 0)
        {
            ValidateAbMergeTopologyMetadata(request, inputBytes, issues);
        }

        return new BoundInputs(inputBytes, inputSummaries, issues);
    }

    private static void ValidateV2InputLengthRequirements(
        CompositionRunRequest request,
        Dictionary<string, byte[]> inputBytes,
        List<CompositionIssue> issues)
    {
        if (request.CompiledComposition.V2Details is not { } details)
        {
            return;
        }

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
                case CompiledTpMaximum256KInputLengthRequirement
                    when bytes.LongLength > CompiledTpMaximum256KInputLengthRequirement.MaximumBytes:
                    issues.Add(new CompositionIssue(
                        CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                        $"Input bytes for address space '{binding.AddressSpaceId}' exceed the 256 KiB maximum (actual {bytes.LongLength} bytes, maximum {CompiledTpMaximum256KInputLengthRequirement.MaximumBytes} bytes).",
                        binding.AddressSpaceId));
                    break;
                case CompiledDeclaredPrefixWithWarningInputLengthRequirement declaredPrefix
                    when bytes.LongLength < declaredPrefix.RequiredEndExclusive:
                    issues.Add(new CompositionIssue(
                        declaredPrefix.ShortInputIssueCode,
                        $"Input bytes for address space '{binding.AddressSpaceId}' end at 0x{bytes.LongLength:X}, before required end 0x{declaredPrefix.RequiredEndExclusive:X}; no padding is authorized.",
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
                TryCreateDeclaredPrefixInspectionPolicy(request, addressSpaceId),
                inputBytes,
                inputSummaries,
                artifactSnapshots,
                issues,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask TryReadBindingAsync(
        InputArtifactBinding binding,
        DeclaredPrefixInputInspectionPolicy? inspectionPolicy,
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

            inputBytes.Add(binding.AddressSpaceId, buffer);
            InputArtifactExecutionSnapshotSummary? executionSnapshot = null;
            if (inspectionPolicy is not null)
            {
                InputArtifactInspection inspection = DeclaredPrefixInputInspector.Inspect(
                    inspectionPolicy,
                    buffer);
                if (inspection.AcceptedSnapshot is { } accepted &&
                    inspection.AcceptedSnapshotRange is { } acceptedRange)
                {
                    executionSnapshot = new InputArtifactExecutionSnapshotSummary(
                        acceptedRange,
                        accepted.Sha256,
                        inspection.IgnoredTrailingRange);
                }
            }

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

    private static DeclaredPrefixInputInspectionPolicy? TryCreateDeclaredPrefixInspectionPolicy(
        CompositionRunRequest request,
        string addressSpaceId)
    {
        if (request.CompiledComposition.V2Details is not { } details ||
            details.Provenance.Context is LogicalOutputV2CompilationContext)
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
        return slot?.LengthRequirement is CompiledDeclaredPrefixWithWarningInputLengthRequirement declaredPrefix
            ? new DeclaredPrefixInputInspectionPolicy(
                declaredPrefix.RequiredEndExclusive,
                declaredPrefix.ExpectedOuterLengths,
                declaredPrefix.ShortInputIssueCode,
                declaredPrefix.UnexpectedOuterLengthIssueCode)
            : null;
    }

    private sealed record BoundInputs(
        IReadOnlyDictionary<string, byte[]> InputBytes,
        IReadOnlyList<InputArtifactSummary> InputSummaries,
        IReadOnlyList<CompositionIssue> Issues);

    private sealed record ArtifactReadSnapshot(byte[] Bytes, string Sha256);
}

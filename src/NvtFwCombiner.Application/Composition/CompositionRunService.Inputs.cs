using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private async ValueTask<BoundInputs> ReadInputsAsync(
        CompositionRunRequest request,
        CancellationToken cancellationToken)
    {
        Dictionary<string, byte[]> inputBytes = new(StringComparer.Ordinal);
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
                    issues,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        ValidateV2InputLengthRequirements(request, inputBytes, issues);
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

        var slots = details.InputContract.Slots.ToDictionary(
            static slot => slot.SlotId,
            StringComparer.Ordinal);
        foreach (CompiledInputSpaceBinding binding in details.InputContract.SpaceBindings)
        {
            if (slots[binding.SlotId].LengthRequirement is not CompiledTpMaximum256KInputLengthRequirement ||
                !inputBytes.TryGetValue(binding.AddressSpaceId, out byte[]? bytes) ||
                bytes.LongLength <= CompiledTpMaximum256KInputLengthRequirement.MaximumBytes)
            {
                continue;
            }

            issues.Add(new CompositionIssue(
                CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                $"Input bytes for address space '{binding.AddressSpaceId}' exceed the 256 KiB maximum (actual {bytes.LongLength} bytes, maximum {CompiledTpMaximum256KInputLengthRequirement.MaximumBytes} bytes).",
                binding.AddressSpaceId));
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
                inputBytes,
                inputSummaries,
                issues,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask TryReadBindingAsync(
        InputArtifactBinding binding,
        Dictionary<string, byte[]> inputBytes,
        List<InputArtifactSummary> inputSummaries,
        List<CompositionIssue> issues,
        CancellationToken cancellationToken)
    {
        try
        {
            ReadOnlyMemory<byte> bytes = await _artifactReader
                .ReadAsync(binding.ArtifactId, cancellationToken)
                .ConfigureAwait(false);
            byte[] buffer = bytes.ToArray();
            inputBytes.Add(binding.AddressSpaceId, buffer);
            inputSummaries.Add(new InputArtifactSummary(
                binding.AddressSpaceId,
                binding.BindingId,
                buffer.LongLength,
                ToSha256Hex(buffer),
                binding.OriginalFileName));
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

    private sealed record BoundInputs(
        IReadOnlyDictionary<string, byte[]> InputBytes,
        IReadOnlyList<InputArtifactSummary> InputSummaries,
        IReadOnlyList<CompositionIssue> Issues);
}

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
        List<CompositionIssue> issues = [];
        HashSet<string> readAddressSpaces = new(StringComparer.Ordinal);

        foreach (string addressSpaceId in request.Plan.RequiredInputAddressSpaceIds)
        {
            await ReadRequiredBindingAsync(
                    request,
                    addressSpaceId,
                    "input.binding.missing",
                    inputBytes,
                    inputSummaries,
                    issues,
                    readAddressSpaces,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (string addressSpaceId in request.Plan.RequiredSeededMutableAddressSpaceIds)
        {
            await ReadRequiredBindingAsync(
                    request,
                    addressSpaceId,
                    "input.mutable-binding.missing",
                    inputBytes,
                    inputSummaries,
                    issues,
                    readAddressSpaces,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (InputArtifactBinding binding in request.ArtifactBindings.Values.OrderBy(
                     binding => binding.AddressSpaceId,
                     StringComparer.Ordinal))
        {
            if (readAddressSpaces.Contains(binding.AddressSpaceId) ||
                !IsSuppliedMutablePlanSpace(request.Plan, binding.AddressSpaceId))
            {
                continue;
            }

            await TryReadBindingAsync(
                    binding,
                    inputBytes,
                    inputSummaries,
                    issues,
                    readAddressSpaces,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return new BoundInputs(inputBytes, inputSummaries, issues);
    }

    private async ValueTask ReadRequiredBindingAsync(
        CompositionRunRequest request,
        string addressSpaceId,
        string missingIssueCode,
        Dictionary<string, byte[]> inputBytes,
        List<InputArtifactSummary> inputSummaries,
        List<CompositionIssue> issues,
        HashSet<string> readAddressSpaces,
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
                readAddressSpaces,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask TryReadBindingAsync(
        InputArtifactBinding binding,
        Dictionary<string, byte[]> inputBytes,
        List<InputArtifactSummary> inputSummaries,
        List<CompositionIssue> issues,
        HashSet<string> readAddressSpaces,
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
                ToSha256Hex(buffer)));
            _ = readAddressSpaces.Add(binding.AddressSpaceId);
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

    private static bool IsSuppliedMutablePlanSpace(CompositionPlan plan, string addressSpaceId)
    {
        return plan.AddressSpaces.Any(addressSpace =>
            string.Equals(addressSpace.AddressSpaceId, addressSpaceId, StringComparison.Ordinal) &&
            addressSpace.Mutability == AddressSpaceMutability.Mutable &&
            !string.Equals(addressSpace.AddressSpaceId, plan.Initialization.TargetSpaceId, StringComparison.Ordinal));
    }

    private sealed record BoundInputs(
        IReadOnlyDictionary<string, byte[]> InputBytes,
        IReadOnlyList<InputArtifactSummary> InputSummaries,
        IReadOnlyList<CompositionIssue> Issues);
}

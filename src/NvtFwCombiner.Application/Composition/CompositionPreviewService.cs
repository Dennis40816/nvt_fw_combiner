using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Runs preview semantics through the shared domain composition engine.</summary>
public sealed class CompositionPreviewService
{
    private readonly IArtifactReader _artifactReader;

    /// <summary>Creates a preview service from application ports and the shared engine.</summary>
    public CompositionPreviewService(IArtifactReader artifactReader)
    {
        ArgumentNullException.ThrowIfNull(artifactReader);

        _artifactReader = artifactReader;
    }

    /// <summary>Reads bound artifacts and executes the compiled plan without committing output.</summary>
    public async ValueTask<CompositionExecutionResult> PreviewAsync(
        CompositionPreviewRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Dictionary<string, byte[]> inputBytes = new(StringComparer.Ordinal);
        List<CompositionIssue> issues = [];
        foreach (string addressSpaceId in request.Plan.RequiredInputAddressSpaceIds)
        {
            if (!request.ArtifactBindings.TryGetValue(addressSpaceId, out string? artifactId) ||
                string.IsNullOrWhiteSpace(artifactId))
            {
                issues.Add(new CompositionIssue(
                    "preview.binding.missing",
                    $"No artifact binding was supplied for required address space '{addressSpaceId}'."));
                continue;
            }

            ReadOnlyMemory<byte> bytes = await _artifactReader
                .ReadAsync(artifactId, cancellationToken)
                .ConfigureAwait(false);
            inputBytes.Add(addressSpaceId, bytes.ToArray());
        }

        if (issues.Count > 0)
        {
            return CompositionExecutionResult.Failed(issues);
        }

        var executionInput = new CompositionExecutionInput(inputBytes);
        return CompositionEngine.Execute(request.Plan, executionInput);
    }
}

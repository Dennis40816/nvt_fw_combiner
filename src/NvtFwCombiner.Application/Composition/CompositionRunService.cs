using System.Security.Cryptography;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Runs preview and build through the shared composition engine.</summary>
public sealed class CompositionRunService
{
    private static readonly string EmptySha256 = ToSha256Hex([]);

    private readonly IArtifactReader _artifactReader;
    private readonly ISystemClock _clock;
    private readonly ICompositionOutputWriter? _outputWriter;

    /// <summary>Creates a run service with read-only preview support.</summary>
    public CompositionRunService(IArtifactReader artifactReader, ISystemClock clock)
        : this(artifactReader, clock, null)
    {
    }

    /// <summary>Creates a run service with preview and build support.</summary>
    public CompositionRunService(
        IArtifactReader artifactReader,
        ISystemClock clock,
        ICompositionOutputWriter? outputWriter)
    {
        ArgumentNullException.ThrowIfNull(artifactReader);
        ArgumentNullException.ThrowIfNull(clock);

        _artifactReader = artifactReader;
        _clock = clock;
        _outputWriter = outputWriter;
    }

    /// <summary>Executes the request without committing output.</summary>
    public async ValueTask<CompositionRunResult> PreviewAsync(
        CompositionRunRequest request,
        CancellationToken cancellationToken)
    {
        return await RunAsync(request, commitOutput: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Executes the request and commits output when execution succeeds.</summary>
    public async ValueTask<CompositionRunResult> BuildAsync(
        CompositionRunRequest request,
        CancellationToken cancellationToken)
    {
        _ = _outputWriter ?? throw new InvalidOperationException("Build requires an output writer.");

        return await RunAsync(request, commitOutput: true, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<CompositionRunResult> RunAsync(
        CompositionRunRequest request,
        bool commitOutput,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        DateTimeOffset startedAtUtc = _clock.UtcNow;
        BoundInputs boundInputs = await ReadInputsAsync(request, cancellationToken).ConfigureAwait(false);
        CompositionExecutionResult execution = boundInputs.Issues.Count == 0
            ? CompositionEngine.Execute(request.Plan, new CompositionExecutionInput(boundInputs.InputBytes))
            : CompositionExecutionResult.Failed(boundInputs.Issues);

        string? committedOutputId = null;
        if (commitOutput && execution.Status == CompositionExecutionStatus.Succeeded)
        {
            committedOutputId = await _outputWriter!
                .CommitAsync(request.OutputFileName, execution.OutputBytes, cancellationToken)
                .ConfigureAwait(false);
        }

        DateTimeOffset completedAtUtc = _clock.UtcNow;
        CompositionRunReport report = CreateReport(
            request,
            execution,
            boundInputs.InputSummaries,
            startedAtUtc,
            completedAtUtc,
            committedOutputId is not null);

        return new CompositionRunResult(
            execution.Status,
            execution.OutputBytes.ToArray(),
            report,
            committedOutputId);
    }

    private async ValueTask<BoundInputs> ReadInputsAsync(
        CompositionRunRequest request,
        CancellationToken cancellationToken)
    {
        Dictionary<string, byte[]> inputBytes = new(StringComparer.Ordinal);
        List<InputArtifactSummary> inputSummaries = [];
        List<CompositionIssue> issues = [];

        foreach (string addressSpaceId in request.Plan.RequiredInputAddressSpaceIds)
        {
            if (!request.ArtifactBindings.TryGetValue(addressSpaceId, out string? artifactId) ||
                string.IsNullOrWhiteSpace(artifactId))
            {
                issues.Add(new CompositionIssue(
                    "input.binding.missing",
                    $"No artifact binding was supplied for required address space '{addressSpaceId}'."));
                continue;
            }

            ReadOnlyMemory<byte> bytes = await _artifactReader
                .ReadAsync(artifactId, cancellationToken)
                .ConfigureAwait(false);
            byte[] buffer = bytes.ToArray();
            inputBytes.Add(addressSpaceId, buffer);
            inputSummaries.Add(new InputArtifactSummary(
                addressSpaceId,
                artifactId,
                buffer.LongLength,
                ToSha256Hex(buffer)));
        }

        return new BoundInputs(inputBytes, inputSummaries, issues);
    }

    private static CompositionRunReport CreateReport(
        CompositionRunRequest request,
        CompositionExecutionResult execution,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        bool committed)
    {
        OperationRunStatus status = execution.Status == CompositionExecutionStatus.Succeeded
            ? OperationRunStatus.Succeeded
            : OperationRunStatus.Skipped;
        OperationRunSummary[] operations = [
            .. request.Plan.OrderedOperations.Select(operation => new OperationRunSummary(
                operation.OperationId,
                operation.Sequence,
                operation.Kind,
                status)),
        ];

        byte[] outputBytes = execution.OutputBytes.ToArray();
        var output = new OutputArtifactSummary(
            request.OutputFileName,
            outputBytes.LongLength,
            outputBytes.Length == 0 ? EmptySha256 : ToSha256Hex(outputBytes),
            committed);

        return new CompositionRunReport(
            request.RunId,
            request.Profile.ProfileId,
            request.Profile.ProfileVersion,
            request.Profile.IcId,
            request.Profile.ModeId,
            request.Profile.ExperienceId,
            request.Profile.CompositionKind,
            startedAtUtc,
            completedAtUtc,
            inputSummaries,
            operations,
            execution.Mutations,
            execution.Issues,
            output);
    }

    private static string ToSha256Hex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed record BoundInputs(
        IReadOnlyDictionary<string, byte[]> InputBytes,
        IReadOnlyList<InputArtifactSummary> InputSummaries,
        IReadOnlyList<CompositionIssue> Issues);
}

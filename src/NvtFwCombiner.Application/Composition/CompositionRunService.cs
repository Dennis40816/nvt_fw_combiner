using System.Security.Cryptography;
using System.Text;
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
        if (commitOutput && request.ApprovedPreviewToken is null)
        {
            CompositionExecutionResult previewRequired = CompositionExecutionResult.Failed([
                new CompositionIssue(
                    "build.preview-token.required",
                    "Build requires an approved preview token before output can be committed."),
            ]);
            DateTimeOffset failedAtUtc = _clock.UtcNow;
            CompositionRunReport failedReport = CreateReport(
                request,
                previewRequired,
                [],
                startedAtUtc,
                failedAtUtc,
                committed: false);
            return new CompositionRunResult(
                previewRequired.Status,
                [],
                failedReport,
                committedOutputId: null);
        }

        BoundInputs boundInputs = await ReadInputsAsync(request, cancellationToken).ConfigureAwait(false);
        CompositionExecutionResult execution = boundInputs.Issues.Count == 0
            ? CompositionEngine.Execute(request.Plan, new CompositionExecutionInput(boundInputs.InputBytes))
            : CompositionExecutionResult.Failed(boundInputs.Issues);
        string? previewToken = execution.Status == CompositionExecutionStatus.Succeeded
            ? CalculatePreviewToken(request, execution, boundInputs.InputSummaries)
            : null;

        string? committedOutputId = null;
        if (commitOutput && execution.Status == CompositionExecutionStatus.Succeeded)
        {
            if (!string.Equals(request.ApprovedPreviewToken, previewToken, StringComparison.Ordinal))
            {
                execution = CompositionExecutionResult.Failed([
                    new CompositionIssue(
                        "build.preview-token.mismatch",
                        "Build request does not match the approved preview token."),
                ]);
            }
            else
            {
                committedOutputId = await _outputWriter!
                    .CommitAsync(request.OutputFileName, execution.OutputBytes, cancellationToken)
                    .ConfigureAwait(false);
            }
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
            committedOutputId,
            commitOutput ? null : previewToken);
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
            if (!request.ArtifactBindings.TryGetValue(addressSpaceId, out InputArtifactBinding? binding))
            {
                issues.Add(new CompositionIssue(
                    "input.binding.missing",
                    $"No artifact binding was supplied for required address space '{addressSpaceId}'."));
                continue;
            }

            ReadOnlyMemory<byte> bytes = await _artifactReader
                .ReadAsync(binding.ArtifactId, cancellationToken)
                .ConfigureAwait(false);
            byte[] buffer = bytes.ToArray();
            inputBytes.Add(addressSpaceId, buffer);
            inputSummaries.Add(new InputArtifactSummary(
                addressSpaceId,
                binding.BindingId,
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

    private static string CalculatePreviewToken(
        CompositionRunRequest request,
        CompositionExecutionResult execution,
        IReadOnlyList<InputArtifactSummary> inputSummaries)
    {
        var builder = new StringBuilder();
        AppendTokenLine(builder, request.Profile.ProfileId);
        AppendTokenLine(builder, request.Profile.ProfileVersion);
        AppendTokenLine(builder, request.Profile.IcId);
        AppendTokenLine(builder, request.Profile.ModeId);
        AppendTokenLine(builder, request.Profile.ExperienceId);
        AppendTokenLine(builder, request.Profile.CompositionKind.ToString());
        AppendTokenLine(builder, request.OutputFileName);
        foreach (InputArtifactSummary input in inputSummaries.OrderBy(item => item.AddressSpaceId, StringComparer.Ordinal))
        {
            AppendTokenLine(builder, input.AddressSpaceId);
            AppendTokenLine(builder, input.ArtifactId);
            AppendTokenLine(builder, input.Size.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendTokenLine(builder, input.Sha256);
        }

        foreach (CompositionOperation operation in request.Plan.OrderedOperations)
        {
            AppendTokenLine(builder, operation.OperationId);
            AppendTokenLine(builder, operation.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendTokenLine(builder, operation.Kind.ToString());
            AppendTokenLine(builder, operation.SourceSpaceId ?? string.Empty);
            AppendTokenLine(builder, FormatRange(operation.SourceRange));
            AppendTokenLine(builder, operation.TargetSpaceId);
            AppendTokenLine(builder, FormatRange(operation.TargetRange));
            AppendTokenLine(builder, operation.OverlapPolicy.ToString());
        }

        AppendTokenLine(builder, execution.Status.ToString());
        AppendTokenLine(builder, ToSha256Hex(execution.OutputBytes.Span));
        return ToSha256Hex(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static void AppendTokenLine(StringBuilder builder, string value)
    {
        _ = builder.AppendLine(value);
    }

    private static string FormatRange(ByteRange? range)
    {
        return range is { } value
            ? FormattableString.Invariant($"{value.Start}:{value.Length}")
            : string.Empty;
    }

    private sealed record BoundInputs(
        IReadOnlyDictionary<string, byte[]> InputBytes,
        IReadOnlyList<InputArtifactSummary> InputSummaries,
        IReadOnlyList<CompositionIssue> Issues);
}

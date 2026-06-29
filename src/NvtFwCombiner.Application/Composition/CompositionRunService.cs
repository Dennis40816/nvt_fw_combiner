using System.Security.Cryptography;
using System.Text;
using System.Globalization;
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
            var previewRequired = CompositionExecutionResult.Failed([
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

        MutationRunSummary[] mutations = [
            .. execution.Mutations.Select(ToMutationSummary),
        ];

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
            mutations,
            execution.Issues,
            output);
    }

    private static MutationRunSummary ToMutationSummary(MutationRecord mutation)
    {
        long changedByteCount = mutation.ChangedRanges.Sum(range => range.Length);
        return new MutationRunSummary(
            mutation.OperationId,
            mutation.OperationKind,
            mutation.TargetSpaceId,
            mutation.TargetRange,
            changedByteCount,
            mutation.BeforeSha256.ToLowerInvariant(),
            mutation.AfterSha256.ToLowerInvariant(),
            mutation.Reason);
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
        AppendTokenField(builder, "profile.id", request.Profile.ProfileId);
        AppendTokenField(builder, "profile.version", request.Profile.ProfileVersion);
        AppendTokenField(builder, "profile.ic", request.Profile.IcId);
        AppendTokenField(builder, "profile.mode", request.Profile.ModeId);
        AppendTokenField(builder, "profile.experience", request.Profile.ExperienceId);
        AppendTokenField(builder, "profile.kind", request.Profile.CompositionKind.ToString());
        AppendTokenField(builder, "output.name", request.OutputFileName);
        AppendPlanFingerprint(builder, request.Plan);
        foreach (InputArtifactSummary input in inputSummaries.OrderBy(item => item.AddressSpaceId, StringComparer.Ordinal))
        {
            AppendTokenField(builder, "input.address-space", input.AddressSpaceId);
            AppendTokenField(builder, "input.artifact", input.ArtifactId);
            AppendTokenField(builder, "input.size", input.Size.ToString(CultureInfo.InvariantCulture));
            AppendTokenField(builder, "input.sha256", input.Sha256);
        }

        AppendTokenField(builder, "execution.status", execution.Status.ToString());
        AppendTokenField(builder, "execution.output.sha256", ToSha256Hex(execution.OutputBytes.Span));
        return ToSha256Hex(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static void AppendPlanFingerprint(StringBuilder builder, CompositionPlan plan)
    {
        AppendTokenField(builder, "plan.init.kind", plan.Initialization.Kind.ToString());
        AppendTokenField(builder, "plan.init.target", plan.Initialization.TargetSpaceId);
        AppendTokenField(builder, "plan.init.reference", plan.Initialization.ReferenceSpaceId ?? string.Empty);
        AppendTokenField(builder, "plan.init.capacity", plan.Initialization.Capacity.ToString(CultureInfo.InvariantCulture));
        AppendTokenField(builder, "plan.init.fill", plan.Initialization.FillByte.ToString(CultureInfo.InvariantCulture));
        foreach (AddressSpace addressSpace in plan.AddressSpaces)
        {
            AppendTokenField(builder, "plan.space.id", addressSpace.AddressSpaceId);
            AppendTokenField(builder, "plan.space.length", addressSpace.Length.ToString(CultureInfo.InvariantCulture));
            AppendTokenField(builder, "plan.space.mutability", addressSpace.Mutability.ToString());
        }

        foreach (CompositionOperation operation in plan.OrderedOperations)
        {
            AppendTokenField(builder, "plan.operation.id", operation.OperationId);
            AppendTokenField(builder, "plan.operation.sequence", operation.Sequence.ToString(CultureInfo.InvariantCulture));
            AppendTokenField(builder, "plan.operation.kind", operation.Kind.ToString());
            AppendTokenField(builder, "plan.operation.source-space", operation.SourceSpaceId ?? string.Empty);
            AppendTokenField(builder, "plan.operation.source-range", FormatRange(operation.SourceRange));
            AppendTokenField(builder, "plan.operation.target-space", operation.TargetSpaceId);
            AppendTokenField(builder, "plan.operation.target-range", FormatRange(operation.TargetRange));
            AppendTokenField(builder, "plan.operation.overlap", operation.OverlapPolicy.ToString());
            AppendTokenField(builder, "plan.operation.fill-byte", operation.FillByte?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            AppendTokenField(builder, "plan.operation.patch-bytes", ToHex(operation.PatchBytes.Span));
            AppendTokenField(builder, "plan.operation.reason", operation.Reason);
        }
    }

    private static void AppendTokenField(StringBuilder builder, string fieldName, string value)
    {
        _ = builder
            .Append(fieldName.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(fieldName)
            .Append('=')
            .Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value)
            .Append('\n');
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
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

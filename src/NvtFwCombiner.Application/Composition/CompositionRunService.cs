using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Runs preview and build through the shared composition engine.</summary>
public sealed class CompositionRunService
{
    private static readonly string EmptySha256 = ToSha256Hex([]);
    private const string DifferenceDeclaredReplacement = "DeclaredReplacement";
    private const string DifferencePostbuildCrcHeader = "PostbuildCrcHeader";
    private const string DifferencePreservedReference = "PreservedReference";
    private const string DifferenceUnexpected = "Unexpected";

    private readonly IArtifactReader _artifactReader;
    private readonly ISystemClock _clock;
    private readonly ICompositionOutputWriter? _outputWriter;
    private readonly IExternalProcessor? _externalProcessor;

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
        : this(artifactReader, clock, outputWriter, null)
    {
    }

    /// <summary>Creates a run service with preview, build, and external processor support.</summary>
    public CompositionRunService(
        IArtifactReader artifactReader,
        ISystemClock clock,
        ICompositionOutputWriter? outputWriter,
        IExternalProcessor? externalProcessor)
    {
        ArgumentNullException.ThrowIfNull(artifactReader);
        ArgumentNullException.ThrowIfNull(clock);

        _artifactReader = artifactReader;
        _clock = clock;
        _outputWriter = outputWriter;
        _externalProcessor = externalProcessor;
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
                new Dictionary<string, byte[]>(StringComparer.Ordinal),
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
            ? await ExecutePlanAsync(request, boundInputs, cancellationToken).ConfigureAwait(false)
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
            boundInputs.InputBytes,
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

    private async ValueTask<CompositionExecutionResult> ExecutePlanAsync(
        CompositionRunRequest request,
        BoundInputs boundInputs,
        CancellationToken cancellationToken)
    {
        var input = new CompositionExecutionInput(boundInputs.InputBytes);
        return _externalProcessor is null
            ? CompositionEngine.Execute(request.Plan, input)
            : await CompositionEngine.ExecuteAsync(
                request.Plan,
                input,
                (operation, inputBytes, stagedSources, token) =>
                    TransformExternalProcessorAsync(request, operation, inputBytes, stagedSources, token),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<CompositionExternalProcessorResult> TransformExternalProcessorAsync(
        CompositionRunRequest request,
        CompositionOperation operation,
        ReadOnlyMemory<byte> inputBytes,
        IReadOnlyList<ExternalProcessorStagedSource> stagedSources,
        CancellationToken cancellationToken)
    {
        ExternalProcessorInvocation invocation = operation.ExternalProcessorInvocation!;
        ExternalProcessorRequest processorRequest;
        try
        {
            processorRequest = new ExternalProcessorRequest(
                $"{request.RunId}.{operation.OperationId}",
                invocation.ProcessorId,
                invocation.ToolBindingId,
                inputBytes,
                invocation.AllowedWriteRanges,
                request.IcNumberSelection,
                stagedSources);
        }
        catch (ArgumentException exception)
        {
            return CompositionExternalProcessorResult.Failed([
                new CompositionIssue(
                    "external-processor.request.invalid",
                    exception.Message,
                    operation.OperationId),
            ]);
        }

        ExternalProcessorResult processorResult = await _externalProcessor!
            .TransformAsync(processorRequest, cancellationToken)
            .ConfigureAwait(false);
        return processorResult.Succeeded
            ? CompositionExternalProcessorResult.Success(processorResult.OutputBytes)
            : CompositionExternalProcessorResult.Failed(processorResult.Issues);
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
        IReadOnlyDictionary<string, byte[]> inputBytes,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        bool committed)
    {
        OperationRunStatus status = execution.Status == CompositionExecutionStatus.Succeeded
            ? OperationRunStatus.Succeeded
            : OperationRunStatus.Skipped;
        OperationRunSummary[] operations = [
            .. request.Plan.OrderedOperations.Select(operation => ToOperationSummary(operation, status)),
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
        OutputDifferenceSummary[] outputDifferences = [
            .. CreateOutputDifferences(request, execution, inputBytes, outputBytes),
        ];
        CompositionIssue[] issues = [
            .. execution.Issues,
            .. CreateOutputDifferenceIssues(outputDifferences),
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
            issues,
            output,
            outputDifferences);
    }

    private static List<OutputDifferenceSummary> CreateOutputDifferences(
        CompositionRunRequest request,
        CompositionExecutionResult execution,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        byte[] outputBytes)
    {
        if (execution.Status != CompositionExecutionStatus.Succeeded ||
            request.Profile.CompositionKind != CompositionKind.Replace ||
            request.Plan.Initialization.Kind != ImageInitializationKind.Reference ||
            request.Plan.Initialization.ReferenceSpaceId is not { } referenceSpaceId ||
            !inputBytes.TryGetValue(referenceSpaceId, out byte[]? referenceBytes) ||
            referenceBytes.Length != outputBytes.Length)
        {
            return [];
        }

        IReadOnlyList<ByteRange> changedRanges = ByteDiff.FindChangedRanges(referenceBytes, outputBytes);
        if (changedRanges.Count == 0)
        {
            return [];
        }

        OutputDifferenceExpectation[] expectations = [.. CreateOutputDifferenceExpectations(request)];
        List<OutputDifferenceSummary> rows = [];
        int rowIndex = 0;
        foreach (ByteRange changedRange in changedRanges)
        {
            foreach (ByteRange segment in SplitRangeByExpectations(changedRange, expectations))
            {
                OutputDifferenceExpectation expectation = ClassifyDifferenceSegment(segment, expectations);
                rows.Add(new OutputDifferenceSummary(
                    FormattableString.Invariant($"diff-{++rowIndex:D3}"),
                    segment,
                    segment.Length,
                    expectation.Classification,
                    expectation.IsAccepted,
                    expectation.Evidence,
                    expectation.Explanation,
                    ToSliceSha256Hex(referenceBytes, segment),
                    ToSliceSha256Hex(outputBytes, segment)));
            }
        }

        return rows;
    }

    private static IEnumerable<OutputDifferenceExpectation> CreateOutputDifferenceExpectations(
        CompositionRunRequest request)
    {
        string targetSpaceId = request.Plan.Initialization.TargetSpaceId;
        string? referenceSpaceId = request.Plan.Initialization.ReferenceSpaceId;
        string icNumber = request.IcNumberSelection?.ToStableToken() ?? "unspecified";
        foreach (CompositionOperation operation in request.Plan.OrderedOperations)
        {
            if (!string.Equals(operation.TargetSpaceId, targetSpaceId, StringComparison.Ordinal))
            {
                continue;
            }

            if (operation.Kind == CompositionOperationKind.RunExternalProcessor &&
                operation.ExternalProcessorInvocation is { } invocation)
            {
                ByteRange[] stagedRanges = [.. invocation.StagedSourceBindings.Select(binding => binding.FirmwareRange)];
                foreach (ExternalProcessorStagedSourceBinding binding in invocation.StagedSourceBindings)
                {
                    yield return new OutputDifferenceExpectation(
                        binding.FirmwareRange,
                        DifferenceDeclaredReplacement,
                        true,
                        operation.OperationId,
                        $"Accepted: staged replacement source '{binding.SourceSpaceId}' is pasted back by postbuild for {request.Profile.IcId} / {icNumber}.");
                }

                foreach (ByteRange allowedWriteRange in invocation.AllowedWriteRanges)
                {
                    foreach (ByteRange processorOnlyRange in SubtractRanges(allowedWriteRange, stagedRanges))
                    {
                        yield return new OutputDifferenceExpectation(
                            processorOnlyRange,
                            DifferencePostbuildCrcHeader,
                            true,
                            $"{operation.OperationId}: {invocation.ProcessorId}",
                            $"Accepted: this range is inside the {request.Profile.IcId} / {icNumber} approved postbuild CRC/header write ranges.");
                    }
                }

                continue;
            }

            if (IsReferencePreserveOperation(operation, referenceSpaceId))
            {
                yield return new OutputDifferenceExpectation(
                    operation.TargetRange,
                    DifferencePreservedReference,
                    false,
                    operation.OperationId,
                    "Unexpected: this range is declared to be restored from the reference base.");
                continue;
            }

            yield return new OutputDifferenceExpectation(
                operation.TargetRange,
                DifferenceDeclaredReplacement,
                true,
                operation.OperationId,
                $"Accepted: declared Replace mapping '{operation.OperationId}' writes this final range.");
        }
    }

    private static bool IsReferencePreserveOperation(CompositionOperation operation, string? referenceSpaceId)
    {
        return referenceSpaceId is not null &&
            operation.Kind is CompositionOperationKind.CopyRange or CompositionOperationKind.ReplaceRange &&
            string.Equals(operation.SourceSpaceId, referenceSpaceId, StringComparison.Ordinal) &&
            operation.SourceRange == operation.TargetRange;
    }

    private static IEnumerable<ByteRange> SplitRangeByExpectations(
        ByteRange changedRange,
        IReadOnlyList<OutputDifferenceExpectation> expectations)
    {
        SortedSet<long> points = [changedRange.Start, changedRange.EndExclusive];
        foreach (OutputDifferenceExpectation expectation in expectations)
        {
            ByteRange? overlap = changedRange.Intersect(expectation.Range);
            if (overlap is null)
            {
                continue;
            }

            _ = points.Add(overlap.Value.Start);
            _ = points.Add(overlap.Value.EndExclusive);
        }

        long[] ordered = [.. points];
        for (int index = 0; index < ordered.Length - 1; index++)
        {
            yield return ByteRange.FromStartEndExclusive(ordered[index], ordered[index + 1]);
        }
    }

    private static OutputDifferenceExpectation ClassifyDifferenceSegment(
        ByteRange segment,
        IReadOnlyList<OutputDifferenceExpectation> expectations)
    {
        for (int index = expectations.Count - 1; index >= 0; index--)
        {
            OutputDifferenceExpectation expectation = expectations[index];
            if (!expectation.Range.Contains(segment))
            {
                continue;
            }

            if (string.Equals(expectation.Classification, DifferencePreservedReference, StringComparison.Ordinal))
            {
                break;
            }

            return expectation;
        }

        return new OutputDifferenceExpectation(
            segment,
            DifferenceUnexpected,
            false,
            "not-declared",
            "Unexpected: final output differs from the reference base outside declared replacement and IC-number-specific postbuild CRC/header ranges.");
    }

    private static List<ByteRange> SubtractRanges(ByteRange source, IReadOnlyList<ByteRange> removedRanges)
    {
        ByteRange[] overlaps =
        [
            .. removedRanges
                .Select(source.Intersect)
                .Where(overlap => overlap is not null)
                .Select(overlap => overlap!.Value),
        ];
        if (overlaps.Length == 0)
        {
            return [source];
        }

        SortedSet<long> splitPoints = [source.Start, source.EndExclusive];
        foreach (ByteRange overlap in overlaps)
        {
            _ = splitPoints.Add(overlap.Start);
            _ = splitPoints.Add(overlap.EndExclusive);
        }

        long[] points = [.. splitPoints];
        List<ByteRange> ranges = [];
        for (int index = 0; index < points.Length - 1; index++)
        {
            var segment = ByteRange.FromStartEndExclusive(points[index], points[index + 1]);
            if (!overlaps.Any(overlap => overlap.Overlaps(segment)))
            {
                ranges.Add(segment);
            }
        }

        return ranges;
    }

    private static IEnumerable<CompositionIssue> CreateOutputDifferenceIssues(
        IEnumerable<OutputDifferenceSummary> outputDifferences)
    {
        return outputDifferences
            .Where(difference => !difference.IsAccepted)
            .Select(difference => new CompositionIssue(
                "report.output-difference.unexpected",
                $"Replace output difference '{difference.DifferenceId}' is outside declared replacement ranges and IC-number-specific postbuild CRC/header ranges.",
                difference.DifferenceId));
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

    private static OperationRunSummary ToOperationSummary(
        CompositionOperation operation,
        OperationRunStatus status)
    {
        ExternalProcessorInvocation? invocation = operation.ExternalProcessorInvocation;
        return new OperationRunSummary(
            operation.OperationId,
            operation.Sequence,
            operation.Kind,
            status,
            operation.SourceSpaceId,
            operation.SourceRange,
            operation.TargetSpaceId,
            operation.TargetRange,
            operation.OverlapPolicy,
            invocation?.ProcessorId,
            invocation?.ToolBindingId,
            invocation?.AllowedReadRanges ?? [],
            invocation?.AllowedWriteRanges ?? [],
            operation.Reason);
    }

    private static string ToSha256Hex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string ToSliceSha256Hex(byte[] bytes, ByteRange range)
    {
        return ToSha256Hex(bytes.AsSpan(checked((int)range.Start), checked((int)range.Length)));
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
        AppendTokenField(builder, "profile.ic-number-mode", request.Profile.IcNumberInputMode?.ToString() ?? string.Empty);
        AppendTokenField(builder, "run.ic-number", request.IcNumberSelection?.ToStableToken() ?? string.Empty);
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
            AppendTokenField(
                builder,
                "plan.space.input-padding",
                addressSpace.InputPaddingByte?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            AppendTokenField(builder, "plan.space.input-oversize-policy", addressSpace.InputOversizePolicy.ToString());
            AppendTokenField(
                builder,
                "plan.space.allowed-input-lengths",
                string.Join(",", addressSpace.AllowedInputLengths.Select(length => length.ToString(CultureInfo.InvariantCulture))));
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
            AppendProcessorFingerprint(builder, operation);
            AppendTokenField(builder, "plan.operation.reason", operation.Reason);
        }
    }

    private static void AppendProcessorFingerprint(StringBuilder builder, CompositionOperation operation)
    {
        if (operation.ExternalProcessorInvocation is not { } invocation)
        {
            AppendTokenField(builder, "plan.operation.processor.id", string.Empty);
            AppendTokenField(builder, "plan.operation.processor.tool-binding", string.Empty);
            return;
        }

        AppendTokenField(builder, "plan.operation.processor.id", invocation.ProcessorId);
        AppendTokenField(builder, "plan.operation.processor.tool-binding", invocation.ToolBindingId);
        foreach (ByteRange range in invocation.AllowedReadRanges)
        {
            AppendTokenField(builder, "plan.operation.processor.read-range", FormatRange(range));
        }

        foreach (ByteRange range in invocation.AllowedWriteRanges)
        {
            AppendTokenField(builder, "plan.operation.processor.write-range", FormatRange(range));
        }

        foreach (ExternalProcessorStagedSourceBinding binding in invocation.StagedSourceBindings)
        {
            AppendTokenField(builder, "plan.operation.processor.staged-source.source-space", binding.SourceSpaceId);
            AppendTokenField(builder, "plan.operation.processor.staged-source.source-range", FormatRange(binding.SourceRange));
            AppendTokenField(builder, "plan.operation.processor.staged-source.firmware-range", FormatRange(binding.FirmwareRange));
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

    private sealed record OutputDifferenceExpectation(
        ByteRange Range,
        string Classification,
        bool IsAccepted,
        string Evidence,
        string Explanation);
}

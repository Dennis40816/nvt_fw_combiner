using System.Security.Cryptography;

namespace NvtFwCombiner.Domain.Composition;

/// <summary>Executes validated composition plans against immutable input bytes.</summary>
public static class CompositionEngine
{
    /// <summary>Executes <paramref name="plan"/> and returns output bytes, mutation trace, or structured issues.</summary>
    public static CompositionExecutionResult Execute(CompositionPlan plan, CompositionExecutionInput input)
    {
        return ExecuteAsync(plan, input, externalProcessor: null, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>Executes <paramref name="plan"/> and invokes an application-owned hook for external operations.</summary>
    public static async ValueTask<CompositionExecutionResult> ExecuteAsync(
        CompositionPlan plan,
        CompositionExecutionInput input,
        CompositionExternalProcessor? externalProcessor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(input);

        List<CompositionIssue> issues = ValidateExecutionInputs(plan, input);
        if (issues.Count > 0)
        {
            return CompositionExecutionResult.Failed(issues);
        }

        NormalizedExecutionInputs normalizedInputs = NormalizeExecutionInputs(plan, input);
        Dictionary<string, byte[]> mutableBuffers = InitializeMutableBuffers(plan, normalizedInputs.InputBytes);
        List<MutationRecord> mutations = [];

        foreach (CompositionOperation operation in plan.OrderedOperations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] targetBuffer = mutableBuffers[operation.TargetSpaceId];
            byte[] before = ReadSlice(targetBuffer, operation.TargetRange);

            if (operation.Kind == CompositionOperationKind.RunExternalProcessor)
            {
                CompositionExecutionResult? externalFailure = await ApplyExternalProcessorAsync(
                        operation,
                        targetBuffer,
                        normalizedInputs.InputBytes,
                        externalProcessor,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (externalFailure is not null)
                {
                    return PrependIssues(externalFailure, normalizedInputs.Issues);
                }
            }
            else
            {
                ApplyHostOperation(operation, normalizedInputs.InputBytes, mutableBuffers);
            }

            byte[] after = ReadSlice(targetBuffer, operation.TargetRange);
            mutations.Add(CreateMutationRecord(operation, before, after));
        }

        return CompositionExecutionResult.Succeeded(
            mutableBuffers[plan.Initialization.TargetSpaceId],
            mutations,
            normalizedInputs.Issues);
    }

    private static List<CompositionIssue> ValidateExecutionInputs(CompositionPlan plan, CompositionExecutionInput input)
    {
        List<CompositionIssue> issues = [];
        foreach (AddressSpace addressSpace in plan.AddressSpaces)
        {
            if (addressSpace.Mutability == AddressSpaceMutability.Mutable &&
                string.Equals(addressSpace.AddressSpaceId, plan.Initialization.TargetSpaceId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!input.TryGetBytes(addressSpace.AddressSpaceId, out ReadOnlyMemory<byte> bytes))
            {
                if (addressSpace.Mutability == AddressSpaceMutability.Immutable)
                {
                    issues.Add(new CompositionIssue(
                        "input.address-space.missing",
                        $"Input bytes for address space '{addressSpace.AddressSpaceId}' are missing."));
                }
                else if (RequiresMutableSeed(plan, addressSpace))
                {
                    issues.Add(new CompositionIssue(
                        "input.mutable-address-space.missing",
                        $"Mutable address space '{addressSpace.AddressSpaceId}' requires seed bytes before execution."));
                }

                continue;
            }

            if (addressSpace.AllowedInputLengths.Count > 0 &&
                !addressSpace.AllowedInputLengths.Contains(bytes.Length))
            {
                issues.Add(new CompositionIssue(
                    "input.address-space.length-mismatch",
                    $"Input bytes for address space '{addressSpace.AddressSpaceId}' must match one of the declared lengths ({FormatAllowedLengths(addressSpace.AllowedInputLengths)}) but actual length is {bytes.Length} bytes."));
            }
            else if (bytes.Length > addressSpace.Length && addressSpace.InputOversizePolicy == InputOversizePolicy.Reject)
            {
                issues.Add(new CompositionIssue(
                    "input.address-space.length-mismatch",
                    $"Input bytes for address space '{addressSpace.AddressSpaceId}' exceed declared length (actual {bytes.Length} bytes, declared {addressSpace.Length} bytes)."));
            }
            else if (bytes.Length > addressSpace.Length && addressSpace.Length > int.MaxValue)
            {
                issues.Add(new CompositionIssue(
                    "execution.capacity.unsupported",
                    $"Truncated input bytes for address space '{addressSpace.AddressSpaceId}' exceed the supported runtime array length."));
            }
            else if (bytes.Length < addressSpace.Length && addressSpace.InputPaddingByte is null)
            {
                issues.Add(new CompositionIssue(
                    "input.address-space.length-mismatch",
                    $"Input bytes for address space '{addressSpace.AddressSpaceId}' are shorter than declared length and no padding byte is declared (actual {bytes.Length} bytes, declared {addressSpace.Length} bytes)."));
            }
            else if (bytes.Length < addressSpace.Length && addressSpace.Length > int.MaxValue)
            {
                issues.Add(new CompositionIssue(
                    "execution.capacity.unsupported",
                    $"Padded input bytes for address space '{addressSpace.AddressSpaceId}' exceed the supported runtime array length."));
            }
        }

        if (plan.Initialization.Capacity > int.MaxValue)
        {
            issues.Add(new CompositionIssue(
                "execution.capacity.unsupported",
                "In-memory composition capacity exceeds the supported runtime array length."));
        }

        return issues;
    }

    private static string FormatAllowedLengths(IReadOnlyList<long> lengths)
    {
        return string.Join(
            ", ",
            lengths.Select(length => FormattableString.Invariant($"0x{length:X}")));
    }

    private static NormalizedExecutionInputs NormalizeExecutionInputs(CompositionPlan plan, CompositionExecutionInput input)
    {
        Dictionary<string, byte[]> normalizedInputs = new(StringComparer.Ordinal);
        List<CompositionIssue> issues = [];
        foreach (AddressSpace addressSpace in plan.AddressSpaces)
        {
            if (string.Equals(addressSpace.AddressSpaceId, plan.Initialization.TargetSpaceId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!input.TryGetBytes(addressSpace.AddressSpaceId, out ReadOnlyMemory<byte> bytes))
            {
                continue;
            }

            byte[] buffer = bytes.ToArray();
            if (buffer.LongLength > addressSpace.Length)
            {
                long discardedByteCount = buffer.LongLength - addressSpace.Length;
                buffer = [.. buffer.AsSpan(0, checked((int)addressSpace.Length))];
                issues.Add(new CompositionIssue(
                    "input.address-space.truncated",
                    $"Input bytes for address space '{addressSpace.AddressSpaceId}' exceed declared length and were truncated from {bytes.Length} to {addressSpace.Length} bytes; {discardedByteCount} trailing bytes were discarded.",
                    addressSpace.AddressSpaceId));
            }
            else if (buffer.LongLength < addressSpace.Length)
            {
                byte[] padded = new byte[checked((int)addressSpace.Length)];
                buffer.CopyTo(padded, 0);
                Array.Fill(
                    padded,
                    addressSpace.InputPaddingByte!.Value,
                    buffer.Length,
                    padded.Length - buffer.Length);
                buffer = padded;
            }

            normalizedInputs.Add(addressSpace.AddressSpaceId, buffer);
        }

        return new NormalizedExecutionInputs(normalizedInputs, issues);
    }

    private static bool RequiresMutableSeed(CompositionPlan plan, AddressSpace addressSpace)
    {
        return plan.OrderedOperations.Any(operation =>
            string.Equals(operation.TargetSpaceId, addressSpace.AddressSpaceId, StringComparison.Ordinal) ||
            string.Equals(operation.SourceSpaceId, addressSpace.AddressSpaceId, StringComparison.Ordinal));
    }

    private static Dictionary<string, byte[]> InitializeMutableBuffers(
        CompositionPlan plan,
        Dictionary<string, byte[]> input)
    {
        Dictionary<string, byte[]> mutableBuffers = new(StringComparer.Ordinal);
        foreach (AddressSpace addressSpace in plan.AddressSpaces.Where(item => item.Mutability == AddressSpaceMutability.Mutable))
        {
            if (string.Equals(addressSpace.AddressSpaceId, plan.Initialization.TargetSpaceId, StringComparison.Ordinal))
            {
                mutableBuffers.Add(addressSpace.AddressSpaceId, InitializeOutput(plan.Initialization, input));
                continue;
            }

            if (input.TryGetValue(addressSpace.AddressSpaceId, out byte[]? seedBytes))
            {
                mutableBuffers.Add(addressSpace.AddressSpaceId, [.. seedBytes]);
            }
        }

        return mutableBuffers;
    }

    private static byte[] InitializeOutput(
        ImageInitialization initialization,
        Dictionary<string, byte[]> input)
    {
        if (initialization.Kind == ImageInitializationKind.Blank)
        {
            byte[] output = new byte[checked((int)initialization.Capacity)];
            Array.Fill(output, initialization.FillByte);
            return output;
        }

        return [.. input[initialization.ReferenceSpaceId!]];
    }

    private static void ApplyHostOperation(
        CompositionOperation operation,
        Dictionary<string, byte[]> input,
        Dictionary<string, byte[]> mutableBuffers)
    {
        byte[] targetBuffer = mutableBuffers[operation.TargetSpaceId];
        Span<byte> targetSpan = targetBuffer.AsSpan((int)operation.TargetRange.Start, (int)operation.TargetRange.Length);

        if (operation.Kind is CompositionOperationKind.CopyRange or CompositionOperationKind.ReplaceRange)
        {
            ReadOperationSource(operation, input, mutableBuffers).CopyTo(targetSpan);
            return;
        }

        if (operation.Kind == CompositionOperationKind.FillRange)
        {
            targetSpan.Fill(operation.FillByte!.Value);
            return;
        }

        if (operation.Kind == CompositionOperationKind.PatchScalar)
        {
            operation.PatchBytes.Span.CopyTo(targetSpan);
            return;
        }

        throw new InvalidOperationException($"Unsupported operation kind '{operation.Kind}'.");
    }

    private static async ValueTask<CompositionExecutionResult?> ApplyExternalProcessorAsync(
        CompositionOperation operation,
        byte[] targetBuffer,
        Dictionary<string, byte[]> input,
        CompositionExternalProcessor? externalProcessor,
        CancellationToken cancellationToken)
    {
        if (externalProcessor is null)
        {
            return CompositionExecutionResult.Failed([
                new CompositionIssue(
                    "execution.external-processor.unavailable",
                    $"Operation '{operation.OperationId}' requires an external processor adapter.",
                    operation.OperationId),
            ]);
        }

        byte[] processorInput = ReadSlice(targetBuffer, operation.TargetRange);
        List<ExternalProcessorStagedSource> stagedSources = BuildStagedSources(operation, input);
        CompositionExternalProcessorResult processorResult;
        try
        {
            processorResult = await externalProcessor(operation, processorInput, stagedSources, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return CompositionExecutionResult.Failed([
                new CompositionIssue(
                    "execution.external-processor.failed",
                    $"Operation '{operation.OperationId}' external processor failed ({exception.GetType().Name}).",
                    operation.OperationId),
            ]);
        }

        if (!processorResult.Succeeded)
        {
            return CompositionExecutionResult.Failed(processorResult.Issues);
        }

        if (processorResult.OutputBytes.Length != processorInput.Length)
        {
            return CompositionExecutionResult.Failed([
                new CompositionIssue(
                    "execution.external-processor.length-mismatch",
                    $"Operation '{operation.OperationId}' external processor changed the staged byte length.",
                    operation.OperationId),
            ]);
        }

        processorResult.OutputBytes.Span.CopyTo(targetBuffer.AsSpan(
            (int)operation.TargetRange.Start,
            (int)operation.TargetRange.Length));
        return null;
    }

    private static List<ExternalProcessorStagedSource> BuildStagedSources(
        CompositionOperation operation,
        Dictionary<string, byte[]> input)
    {
        ExternalProcessorInvocation invocation = operation.ExternalProcessorInvocation!;
        if (invocation.StagedSourceBindings.Count == 0)
        {
            return [];
        }

        List<ExternalProcessorStagedSource> stagedSources = [];
        foreach (ExternalProcessorStagedSourceBinding binding in invocation.StagedSourceBindings)
        {
            byte[] sourceBuffer = input[binding.SourceSpaceId];
            byte[] sourceBytes = ReadSlice(sourceBuffer, binding.SourceRange);
            stagedSources.Add(new ExternalProcessorStagedSource(binding.FirmwareRange, sourceBytes));
        }

        return stagedSources;
    }

    private static ReadOnlySpan<byte> ReadOperationSource(
        CompositionOperation operation,
        Dictionary<string, byte[]> input,
        Dictionary<string, byte[]> mutableBuffers)
    {
        ByteRange sourceRange = operation.SourceRange!.Value;
        if (mutableBuffers.TryGetValue(operation.SourceSpaceId!, out byte[]? mutableSource))
        {
            return mutableSource.AsSpan((int)sourceRange.Start, (int)sourceRange.Length);
        }

        byte[] sourceBytes = input[operation.SourceSpaceId!];
        return sourceBytes.AsSpan((int)sourceRange.Start, (int)sourceRange.Length);
    }

    private static MutationRecord CreateMutationRecord(CompositionOperation operation, byte[] before, byte[] after)
    {
        IReadOnlyList<ByteRange> changedRanges = [
            .. ByteDiff.FindChangedRanges(before, after)
                .Select(range => new ByteRange(checked(operation.TargetRange.Start + range.Start), range.Length)),
        ];

        return new MutationRecord(
            operation.OperationId,
            operation.Kind,
            operation.TargetSpaceId,
            operation.TargetRange,
            changedRanges,
            FormatSha256(before),
            FormatSha256(after),
            operation.Reason);
    }

    private static string FormatSha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static byte[] ReadSlice(byte[] buffer, ByteRange range)
    {
        return [.. buffer.AsSpan((int)range.Start, (int)range.Length)];
    }

    private static CompositionExecutionResult PrependIssues(
        CompositionExecutionResult result,
        IReadOnlyList<CompositionIssue> issues)
    {
        return issues.Count == 0
            ? result
            : CompositionExecutionResult.Failed([.. issues, .. result.Issues]);
    }

    private sealed record NormalizedExecutionInputs(
        Dictionary<string, byte[]> InputBytes,
        IReadOnlyList<CompositionIssue> Issues);
}

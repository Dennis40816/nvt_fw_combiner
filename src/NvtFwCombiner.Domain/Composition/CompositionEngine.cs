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

        Dictionary<string, byte[]> mutableBuffers = InitializeMutableBuffers(plan, input);
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
                        externalProcessor,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (externalFailure is not null)
                {
                    return externalFailure;
                }
            }
            else
            {
                ApplyHostOperation(operation, input, mutableBuffers);
            }

            byte[] after = ReadSlice(targetBuffer, operation.TargetRange);
            mutations.Add(CreateMutationRecord(operation, before, after));
        }

        return CompositionExecutionResult.Succeeded(mutableBuffers[plan.Initialization.TargetSpaceId], mutations);
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

            if (bytes.Length != addressSpace.Length)
            {
                issues.Add(new CompositionIssue(
                    "input.address-space.length-mismatch",
                    $"Input bytes for address space '{addressSpace.AddressSpaceId}' do not match declared length."));
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

    private static bool RequiresMutableSeed(CompositionPlan plan, AddressSpace addressSpace)
    {
        return plan.OrderedOperations.Any(operation =>
            string.Equals(operation.TargetSpaceId, addressSpace.AddressSpaceId, StringComparison.Ordinal) ||
            string.Equals(operation.SourceSpaceId, addressSpace.AddressSpaceId, StringComparison.Ordinal));
    }

    private static Dictionary<string, byte[]> InitializeMutableBuffers(CompositionPlan plan, CompositionExecutionInput input)
    {
        Dictionary<string, byte[]> mutableBuffers = new(StringComparer.Ordinal);
        foreach (AddressSpace addressSpace in plan.AddressSpaces.Where(item => item.Mutability == AddressSpaceMutability.Mutable))
        {
            if (string.Equals(addressSpace.AddressSpaceId, plan.Initialization.TargetSpaceId, StringComparison.Ordinal))
            {
                mutableBuffers.Add(addressSpace.AddressSpaceId, InitializeOutput(plan.Initialization, input));
                continue;
            }

            if (input.TryGetBytes(addressSpace.AddressSpaceId, out ReadOnlyMemory<byte> seedBytes))
            {
                mutableBuffers.Add(addressSpace.AddressSpaceId, seedBytes.ToArray());
            }
        }

        return mutableBuffers;
    }

    private static byte[] InitializeOutput(ImageInitialization initialization, CompositionExecutionInput input)
    {
        if (initialization.Kind == ImageInitializationKind.Blank)
        {
            byte[] output = new byte[checked((int)initialization.Capacity)];
            Array.Fill(output, initialization.FillByte);
            return output;
        }

        _ = input.TryGetBytes(initialization.ReferenceSpaceId!, out ReadOnlyMemory<byte> referenceBytes);
        return referenceBytes.ToArray();
    }

    private static void ApplyHostOperation(
        CompositionOperation operation,
        CompositionExecutionInput input,
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
        CompositionExternalProcessorResult processorResult;
        try
        {
            processorResult = await externalProcessor(operation, processorInput, cancellationToken).ConfigureAwait(false);
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

    private static ReadOnlySpan<byte> ReadOperationSource(
        CompositionOperation operation,
        CompositionExecutionInput input,
        Dictionary<string, byte[]> mutableBuffers)
    {
        ByteRange sourceRange = operation.SourceRange!.Value;
        if (mutableBuffers.TryGetValue(operation.SourceSpaceId!, out byte[]? mutableSource))
        {
            return mutableSource.AsSpan((int)sourceRange.Start, (int)sourceRange.Length);
        }

        _ = input.TryGetBytes(operation.SourceSpaceId!, out ReadOnlyMemory<byte> sourceBytes);
        return sourceBytes.Span[(int)sourceRange.Start..(int)sourceRange.EndExclusive];
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
}

namespace NvtFwCombiner.Domain.Composition;

public static partial class CompositionEngine
{
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
                        CompositionIssueCodes.InputAddressSpaceMissing,
                        $"Input bytes for address space '{addressSpace.AddressSpaceId}' are missing."));
                }
                else if (RequiresMutableSeed(plan, addressSpace))
                {
                    issues.Add(new CompositionIssue(
                        CompositionIssueCodes.InputMutableAddressSpaceMissing,
                        $"Mutable address space '{addressSpace.AddressSpaceId}' requires seed bytes before execution."));
                }

                continue;
            }

            if (addressSpace.AllowedInputLengths.Count > 0 &&
                !addressSpace.AllowedInputLengths.Contains(bytes.Length))
            {
                issues.Add(new CompositionIssue(
                    CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                    $"Input bytes for address space '{addressSpace.AddressSpaceId}' must match one of the declared lengths ({FormatAllowedLengths(addressSpace.AllowedInputLengths)}) but actual length is {bytes.Length} bytes."));
            }
            else if (bytes.Length > addressSpace.Length && addressSpace.InputOversizePolicy == InputOversizePolicy.Reject)
            {
                issues.Add(new CompositionIssue(
                    CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                    $"Input bytes for address space '{addressSpace.AddressSpaceId}' exceed declared length (actual {bytes.Length} bytes, declared {addressSpace.Length} bytes)."));
            }
            else if (bytes.Length > addressSpace.Length && addressSpace.Length > int.MaxValue)
            {
                issues.Add(new CompositionIssue(
                    CompositionIssueCodes.ExecutionCapacityUnsupported,
                    $"Normalized input bytes for address space '{addressSpace.AddressSpaceId}' exceed the supported runtime array length."));
            }
            else if (bytes.Length < addressSpace.Length && addressSpace.InputPaddingByte is null)
            {
                issues.Add(new CompositionIssue(
                    CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                    $"Input bytes for address space '{addressSpace.AddressSpaceId}' are shorter than declared length and no padding byte is declared (actual {bytes.Length} bytes, declared {addressSpace.Length} bytes)."));
            }
            else if (bytes.Length < addressSpace.Length && addressSpace.Length > int.MaxValue)
            {
                issues.Add(new CompositionIssue(
                    CompositionIssueCodes.ExecutionCapacityUnsupported,
                    $"Padded input bytes for address space '{addressSpace.AddressSpaceId}' exceed the supported runtime array length."));
            }
        }

        if (plan.Initialization.Capacity > int.MaxValue)
        {
            issues.Add(new CompositionIssue(
                CompositionIssueCodes.ExecutionCapacityUnsupported,
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

            byte[] buffer = bytes.Length > addressSpace.Length &&
                addressSpace.InputOversizePolicy == InputOversizePolicy.ExtractDeclaredRange
                ? [.. bytes.Span[..checked((int)addressSpace.Length)]]
                : bytes.ToArray();
            if (buffer.LongLength > addressSpace.Length)
            {
                if (addressSpace.InputOversizePolicy == InputOversizePolicy.TruncateWithWarning)
                {
                    long discardedByteCount = buffer.LongLength - addressSpace.Length;
                    buffer = [.. buffer.AsSpan(0, checked((int)addressSpace.Length))];
                    issues.Add(new CompositionIssue(
                        CompositionIssueCodes.InputAddressSpaceTruncated,
                        $"Input bytes for address space '{addressSpace.AddressSpaceId}' exceed declared length and were truncated from {bytes.Length} to {addressSpace.Length} bytes; {discardedByteCount} trailing bytes were discarded.",
                        addressSpace.AddressSpaceId,
                        CompositionIssueSeverity.Warning));
                }
                else if (addressSpace.InputOversizePolicy == InputOversizePolicy.ExtractDeclaredRange)
                {
                    buffer = [.. buffer.AsSpan(0, checked((int)addressSpace.Length))];
                }
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

            if (addressSpace.ExpectedInputLengths.Count > 0 &&
                !addressSpace.ExpectedInputLengths.Contains(bytes.Length))
            {
                issues.Add(new CompositionIssue(
                    CompositionIssueCodes.InputAddressSpaceLengthUnexpected,
                    $"Input bytes for address space '{addressSpace.AddressSpaceId}' have unexpected length {bytes.Length} bytes; expected {FormatAllowedLengths(addressSpace.ExpectedInputLengths)}. Execution uses only the declared source range [0x0, 0x{addressSpace.Length:X}).",
                    addressSpace.AddressSpaceId,
                    CompositionIssueSeverity.Warning));
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

    private sealed record NormalizedExecutionInputs(
        Dictionary<string, byte[]> InputBytes,
        IReadOnlyList<CompositionIssue> Issues);
}

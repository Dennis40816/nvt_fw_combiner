namespace NvtFwCombiner.Domain.Composition;

public static partial class CompositionEngine
{
    private static List<CompositionIssue> ValidateExecutionInputs(CompositionPlan plan, CompositionExecutionInput input)
    {
        List<CompositionIssue> issues = [];
        foreach (string addressSpaceId in input.AddressSpaceIds)
        {
            if (!plan.TryGetAddressSpace(addressSpaceId, out AddressSpace? suppliedSpace) || suppliedSpace is null)
            {
                issues.Add(new CompositionIssue(
                    CompositionIssueCodes.InputAddressSpaceUnknown,
                    $"Input bytes were supplied for undeclared address space '{addressSpaceId}'.",
                    addressSpaceId));
                continue;
            }

            if (suppliedSpace.Mutability == AddressSpaceMutability.Mutable)
            {
                issues.Add(new CompositionIssue(
                    CompositionIssueCodes.InputMutableAddressSpaceNotAllowed,
                    $"Caller bytes are not allowed for engine-owned mutable address space '{addressSpaceId}'.",
                    addressSpaceId));
            }
        }

        foreach (AddressSpace addressSpace in plan.AddressSpaces.Where(static space =>
                     space.Mutability == AddressSpaceMutability.Immutable))
        {
            if (!input.TryGetImmutableBuffer(addressSpace.AddressSpaceId, out byte[] bytes))
            {
                issues.Add(new CompositionIssue(
                    CompositionIssueCodes.InputAddressSpaceMissing,
                    $"Input bytes for address space '{addressSpace.AddressSpaceId}' are missing."));
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

        foreach (ImageInitialization initialization in plan.Initializations.Where(static item =>
                     item.Capacity > int.MaxValue))
        {
            issues.Add(new CompositionIssue(
                CompositionIssueCodes.ExecutionCapacityUnsupported,
                $"Mutable address space '{initialization.TargetSpaceId}' exceeds the supported runtime array length.",
                initialization.TargetSpaceId));
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
        foreach (AddressSpace addressSpace in plan.AddressSpaces.Where(static space =>
                     space.Mutability == AddressSpaceMutability.Immutable))
        {
            if (!input.TryGetImmutableBuffer(addressSpace.AddressSpaceId, out byte[] immutableBytes))
            {
                continue;
            }

            byte[] buffer;
            if (immutableBytes.LongLength > addressSpace.Length &&
                addressSpace.InputOversizePolicy == InputOversizePolicy.Reject)
            {
                buffer = immutableBytes;
            }
            else if (immutableBytes.LongLength > addressSpace.Length)
            {
                buffer = [.. immutableBytes.AsSpan(0, checked((int)addressSpace.Length))];
                if (addressSpace.InputOversizePolicy == InputOversizePolicy.TruncateWithWarning)
                {
                    long discardedByteCount = immutableBytes.LongLength - addressSpace.Length;
                    issues.Add(new CompositionIssue(
                        CompositionIssueCodes.InputAddressSpaceTruncated,
                        $"Input bytes for address space '{addressSpace.AddressSpaceId}' exceed declared length and were truncated from {immutableBytes.Length} to {addressSpace.Length} bytes; {discardedByteCount} trailing bytes were discarded.",
                        addressSpace.AddressSpaceId,
                        CompositionIssueSeverity.Warning));
                }
            }
            else if (immutableBytes.LongLength < addressSpace.Length)
            {
                buffer = new byte[checked((int)addressSpace.Length)];
                immutableBytes.CopyTo(buffer, 0);
                Array.Fill(
                    buffer,
                    addressSpace.InputPaddingByte!.Value,
                    immutableBytes.Length,
                    buffer.Length - immutableBytes.Length);
            }
            else
            {
                buffer = immutableBytes;
            }

            if (addressSpace.ExpectedInputLengths.Count > 0 &&
                !addressSpace.ExpectedInputLengths.Contains(immutableBytes.Length))
            {
                issues.Add(new CompositionIssue(
                    addressSpace.UnexpectedInputLengthIssueCode ?? CompositionIssueCodes.InputAddressSpaceLengthUnexpected,
                    $"Input bytes for address space '{addressSpace.AddressSpaceId}' have unexpected length {immutableBytes.Length} bytes; expected {FormatAllowedLengths(addressSpace.ExpectedInputLengths)}. Execution uses only the declared source range [0x0, 0x{addressSpace.Length:X}).",
                    addressSpace.AddressSpaceId,
                    CompositionIssueSeverity.Warning));
            }

            normalizedInputs.Add(addressSpace.AddressSpaceId, buffer);
        }

        return new NormalizedExecutionInputs(normalizedInputs, issues);
    }

    private static Dictionary<string, byte[]> InitializeMutableBuffers(
        CompositionPlan plan,
        Dictionary<string, byte[]> input)
    {
        Dictionary<string, byte[]> mutableBuffers = new(StringComparer.Ordinal);
        foreach (ImageInitialization initialization in plan.Initializations)
        {
            mutableBuffers.Add(initialization.TargetSpaceId, InitializeBuffer(initialization, input));
        }

        return mutableBuffers;
    }

    private static byte[] InitializeBuffer(
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

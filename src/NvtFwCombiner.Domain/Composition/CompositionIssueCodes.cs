namespace NvtFwCombiner.Domain.Composition;

/// <summary>Stable issue codes emitted by the shared composition engine.</summary>
public static class CompositionIssueCodes
{
    /// <summary>Required immutable input bytes are missing.</summary>
    public const string InputAddressSpaceMissing = "input.address-space.missing";

    /// <summary>Caller bytes targeted an engine-owned mutable address space.</summary>
    public const string InputMutableAddressSpaceNotAllowed = "execution.input.mutable-not-allowed";

    /// <summary>Caller bytes targeted an undeclared address space.</summary>
    public const string InputAddressSpaceUnknown = "execution.input.address-space-unknown";

    /// <summary>Input bytes do not satisfy the declared address-space length policy.</summary>
    public const string InputAddressSpaceLengthMismatch = "input.address-space.length-mismatch";

    /// <summary>Input bytes were truncated by an explicitly declared oversize policy.</summary>
    public const string InputAddressSpaceTruncated = "input.address-space.truncated";

    /// <summary>Input bytes cover the declared range but do not match the profile's non-blocking expected artifact lengths.</summary>
    public const string InputAddressSpaceLengthUnexpected = "input.address-space.length-unexpected";

    /// <summary>The requested runtime capacity exceeds the in-memory executor limit.</summary>
    public const string ExecutionCapacityUnsupported = "execution.capacity.unsupported";

    /// <summary>An operation requires an external processor but none was supplied.</summary>
    public const string ExecutionExternalProcessorUnavailable = "execution.external-processor.unavailable";

    /// <summary>An external processor adapter failed while running an operation.</summary>
    public const string ExecutionExternalProcessorFailed = "execution.external-processor.failed";

    /// <summary>An external processor changed the staged byte length.</summary>
    public const string ExecutionExternalProcessorLengthMismatch = "execution.external-processor.length-mismatch";
}

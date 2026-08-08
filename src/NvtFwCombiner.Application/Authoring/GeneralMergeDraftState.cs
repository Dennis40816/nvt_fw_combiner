using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>Stable General Merge initializer validation codes shared by adapters.</summary>
public static class GeneralMergeInitializerIssueCodes
{
    /// <summary>Capacity text is missing, invalid, or non-positive.</summary>
    public const string CapacityInvalid = "ui.general-merge.capacity-invalid";

    /// <summary>Capacity exceeds the supported in-memory output domain.</summary>
    public const string CapacityUnsupported =
        "ui.general-merge.capacity-unsupported";

    /// <summary>Fill text does not identify exactly one byte.</summary>
    public const string FillByteInvalid =
        "ui.general-merge.fill-byte-invalid";
}

/// <summary>
/// UI/CLI-facing text boundary for the canonical General Merge initializer.
/// Omitted fill text resolves to <c>0x00</c>.
/// </summary>
public sealed record GeneralMergeInitializerInput(
    string? CapacityText,
    string? FillByteText = null)
{
    /// <summary>Resolves editable invariant text into one typed initializer.</summary>
    public bool TryResolve(
        out GeneralMergeOutputInitializer? initializer,
        out CompositionIssue? issue)
    {
        if (!AuthoringByteRangeCodec.TryParseNonNegativeLong(
                CapacityText,
                out long capacity) ||
            capacity <= 0)
        {
            initializer = null;
            issue = new CompositionIssue(
                GeneralMergeInitializerIssueCodes.CapacityInvalid,
                "General Merge output length must be a positive byte count.",
                "output-length");
            return false;
        }

        if (capacity > GeneralMergeOutputInitializer.MaximumCapacity)
        {
            initializer = null;
            issue = new CompositionIssue(
                GeneralMergeInitializerIssueCodes.CapacityUnsupported,
                "General Merge output length exceeds the supported in-memory composition size.",
                "output-length");
            return false;
        }

        byte fillByte = GeneralMergeOutputInitializer.DefaultFillByte;
        bool hasFillByte = !string.IsNullOrWhiteSpace(FillByteText);
        long parsedFillByte = 0;
        if (hasFillByte &&
            (!AuthoringByteRangeCodec.TryParseNonNegativeLong(
                    FillByteText,
                    out parsedFillByte) ||
             parsedFillByte > byte.MaxValue))
        {
            initializer = null;
            issue = new CompositionIssue(
                GeneralMergeInitializerIssueCodes.FillByteInvalid,
                "General Merge fill byte must be a hexadecimal or decimal value from 0x00 through 0xFF.",
                "fill-byte");
            return false;
        }

        if (hasFillByte)
        {
            fillByte = checked((byte)parsedFillByte);
        }

        initializer = new GeneralMergeOutputInitializer(capacity, fillByte);
        issue = null;
        return true;
    }
}

/// <summary>
/// Complete immutable General Merge authoring draft. General Replace retains
/// the shared mapping draft but cannot carry this blank-output initializer.
/// </summary>
public sealed record GeneralMergeDraftState : AuthoringDraftState
{
    /// <summary>Creates one exact initializer plus ordered mapping draft.</summary>
    public GeneralMergeDraftState(
        GeneralMergeOutputInitializer outputInitializer,
        GeneralMappingDraftState mappings)
        : base(AuthoringDraftKind.GeneralMerge)
    {
        ArgumentNullException.ThrowIfNull(outputInitializer);
        ArgumentNullException.ThrowIfNull(mappings);
        OutputInitializer = outputInitializer;
        Mappings = mappings;
    }

    /// <summary>Exact blank output initialization for this authoring revision.</summary>
    public GeneralMergeOutputInitializer OutputInitializer { get; }

    /// <summary>Shared typed mapping rows compiled after initialization.</summary>
    public GeneralMappingDraftState Mappings { get; }

    internal override AuthoringDraftState CreateImmutableSnapshot()
    {
        return this;
    }

    internal override bool HasSameValue(AuthoringDraftState other)
    {
        return other is GeneralMergeDraftState merge &&
            Equals(OutputInitializer, merge.OutputInitializer) &&
            Mappings.HasSameValue(merge.Mappings);
    }
}

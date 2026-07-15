namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Closed profile-selected Combiner invocations that differ from a tool package default command.</summary>
public static class ExternalCombinerInvocationCatalog
{
    private const string ToolBindingId = "legacy-combiner-1.13.0";

    /// <summary>Exact NT51950 AB merge command extracted from Combiner 1.13.0 source.</summary>
    public static ExternalCombinerInvocationProfile Nt51950AbMerge { get; } = new(
        "nfc-nt51950-ab-merge-combiner-v1",
        ToolBindingId,
        "input-output-file",
        [
            "NT51950BASED_MERGE_AB_MODE",
            "CRC8",
            "{staging.artifact.a-bank}",
            "{staging.artifact.b-bank}",
            "{staging.outputBin}",
            "0x40000",
        ]);

    /// <summary>All profile-selected invocation contracts in deterministic processor-id order.</summary>
    public static IReadOnlyList<ExternalCombinerInvocationProfile> All => [Nt51950AbMerge];
}

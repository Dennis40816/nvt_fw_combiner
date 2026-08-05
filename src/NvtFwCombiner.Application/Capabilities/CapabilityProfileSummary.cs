using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>One authorable canonical profile projected for UI and CLI discovery.</summary>
public sealed record CapabilityProfileSummary(
    string ProfileId,
    string IcId,
    CompositionKind CompositionKind,
    IReadOnlyList<string> RequiredInputAddressSpaceIds,
    string DefaultOutputFileName,
    CompiledIcNumberPolicy? IcNumberPolicy,
    bool CompileSucceeded,
    IReadOnlyList<string> IssueCodes);

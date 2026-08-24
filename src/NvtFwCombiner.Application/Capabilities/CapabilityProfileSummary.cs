using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>One authorable canonical profile projected for UI and CLI discovery.</summary>
public sealed record CapabilityProfileSummary(
    string ProfileId,
    string IcId,
    CompositionKind CompositionKind,
    IReadOnlyList<string> RequiredInputAddressSpaceIds,
    string DefaultOutputFileName,
    IcNumberInputMode? IcNumberInputMode,
    bool CompileSucceeded,
    IReadOnlyList<string> IssueCodes)
{
    /// <summary>Projects one compiled composition into authoring disclosure.</summary>
    public static CapabilityProfileSummary FromCompiled(
        CompiledComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        return new CapabilityProfileSummary(
            composition.V2Details.ProfileId,
            composition.V2Details.Provenance.Context.MemberId,
            composition.V2Details.CompositionKind,
            Array.AsReadOnly(
                composition.Plan.RequiredInputAddressSpaceIds.ToArray()),
            composition.V2Details.OutputNamingRequirement.FileNameTemplate,
            composition.V2Details.IcNumberInputMode,
            CompileSucceeded: true,
            []);
    }
}

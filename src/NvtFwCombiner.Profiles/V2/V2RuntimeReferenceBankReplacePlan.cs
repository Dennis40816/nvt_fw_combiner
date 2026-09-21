using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

// Preparation only: this is deliberately not a CompiledComposition or runtime capability.
// Application must discharge each parent's validation obligations before publishing output.
internal sealed record V2RuntimeReferenceBankReplaceRequest(
    string BankInstanceId,
    V2RuntimeReferenceReplaceCompileRequest Replace);

internal sealed record V2RuntimeReferenceBankReplaceBinding(
    string BankInstanceId,
    string WorkspaceId,
    ByteRange OutputRange,
    FirmwareArtifactPayload Reference,
    CompiledComposition LocalComposition);

internal sealed class V2RuntimeReferenceBankReplacePlan(
    CompiledComposition abLayout,
    FirmwareArtifactPayload reference,
    CompositionPlan plan,
    IEnumerable<V2RuntimeReferenceBankReplaceBinding> banks)
{
    internal CompiledComposition AbLayout { get; } = abLayout;

    internal FirmwareArtifactPayload Reference { get; } = reference;

    internal CompositionPlan Plan { get; } = plan;

    internal IReadOnlyList<V2RuntimeReferenceBankReplaceBinding> Banks { get; } = Array.AsReadOnly(banks.ToArray());

    internal CompositionExecutionInput CreateExecutionInput(IReadOnlyDictionary<string, byte[]> sources)
    {
        var inputs = sources.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        inputs.Add(Reference.ArtifactId, Reference.Bytes.ToArray());
        foreach (V2RuntimeReferenceBankReplaceBinding bank in Banks)
        {
            inputs.Add(bank.Reference.ArtifactId, bank.Reference.Bytes.ToArray());
        }

        return new CompositionExecutionInput(inputs);
    }
}

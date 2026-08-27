using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionRunRequestV2Tests
{
    /// <summary>A General Replace definition cannot publish without its compiler-produced choice.</summary>
    [Fact]
    public void DynamicGeneralReplaceDefinitionRequiresTypedNumberChoice()
    {
        CompiledComposition composition = CreateRuntimeReferenceCandidate();
        RuntimeReferenceReplaceV2CompilationContext context =
            Assert.IsType<RuntimeReferenceReplaceV2CompilationContext>(
                composition.V2Details.Provenance.Context);
        var identity = new CapabilityRouteIdentity(
            context.MemberId,
            ExperienceIds.GeneralReplace,
            "1-ic",
            context.ResolvedMap.ImageMap.MapId);
        CanonicalCapabilityCompilationContract contract =
            CanonicalCapabilityCompilationContract.FromCompiled(identity, composition);
        string fingerprint = CapabilityDefinitionFingerprint.Compute(
            identity,
            contract.ProfileId,
            contract.ProfileVersion,
            contract.TrustedDefinitionSha256,
            contract.AllowedMapVariantIds,
            contract.CompilerSemanticId,
            contract.SemanticBindingIds);

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new CanonicalDynamicCapabilityDefinition(
                identity,
                fingerprint,
                contract,
                Decision("authoring", CapabilityAuthoringAvailability.Available),
                Decision("publication", CapabilityPublicationStatus.Candidate),
                Decision("evidence", CapabilityEvidenceStatus.ContractOnly)));

        Assert.Equal("numberChoice", exception.ParamName);
        Assert.Contains("require one typed", exception.Message, StringComparison.Ordinal);

        PinnedCapabilityDecision<TValue> Decision<TValue>(
            string decisionId,
            TValue value)
            where TValue : struct, Enum
        {
            return new PinnedCapabilityDecision<TValue>(
                decisionId,
                identity.RouteId,
                fingerprint,
                value,
                "typed-choice-test");
        }
    }
}

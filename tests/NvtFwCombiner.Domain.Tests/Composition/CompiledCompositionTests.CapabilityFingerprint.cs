using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompiledCompositionTests
{
    /// <summary>A capability-bound fingerprint references definition identity instead of serializing it again.</summary>
    [Fact]
    public void CapabilityBoundV2FingerprintAddsOnlyExactCompilationState()
    {
        const string capabilityFingerprint =
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
        CompiledComposition baseline = CreateV2()
            .BindCapabilityFingerprint(capabilityFingerprint);
        CompiledComposition changedDefinitionProvenance = CreateV2(
                bundleId: "other-bundle",
                bundleVersion: "9.0.0",
                bundleContentHash: new string('e', 64),
                trustAnchorBindingId: "other-binding",
                profileEntryId: "other-entry",
                profileEntryHash: new string('f', 64),
                profileEvidenceRefs: ["other-evidence"])
            .BindCapabilityFingerprint(capabilityFingerprint);

        Assert.Equal(
            "29d191d3bef7e7502ab3776f84af49ef2707eaf5b2ea243cbf8a6ac96c17c163",
            baseline.CompilationFingerprint);
        Assert.Equal(
            baseline.CompilationFingerprint,
            changedDefinitionProvenance.CompilationFingerprint);
    }
}

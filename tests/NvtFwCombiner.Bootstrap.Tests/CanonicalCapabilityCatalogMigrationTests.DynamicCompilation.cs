using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class CanonicalCapabilityCatalogMigrationTests
{
    /// <summary>Dynamic compilation contracts reject malformed definition bounds at construction.</summary>
    [Fact]
    public void DynamicCompilationContractRejectsMalformedDefinitionBounds()
    {
        const string profileId = "profile";
        const string profileVersion = "1.0.0";
        const string semanticId = "compiler-semantic";
        string hash = new('a', 64);

        _ = Assert.Throws<ArgumentException>(() =>
            new CanonicalCapabilityCompilationContract(
                profileId,
                profileVersion,
                "not-a-sha256",
                ["map"],
                semanticId));
        _ = Assert.Throws<ArgumentException>(() =>
            new CanonicalCapabilityCompilationContract(
                profileId,
                profileVersion,
                hash,
                [""],
                semanticId));
        _ = Assert.Throws<ArgumentException>(() =>
            new CanonicalCapabilityCompilationContract(
                profileId,
                profileVersion,
                hash,
                [],
                semanticId));
        _ = Assert.Throws<ArgumentException>(() =>
            new CanonicalCapabilityCompilationContract(
                profileId,
                profileVersion,
                hash,
                ["map"],
                semanticId,
                [" "]));
    }

    /// <summary>Map, compiler, and selection-group drift are independent admission failures.</summary>
    [Fact]
    public void DynamicCompilationContractRejectsExactSemanticDrift()
    {
        var catalog = new CanonicalCapabilityCatalog(
            new CanonicalCapabilityCatalogMigrationSource());
        CapabilityCatalogReloadResult reload = catalog.Reload(
            TestContext.Current.CancellationToken);
        ResolvedCapabilityRoute route = reload.Snapshot!.DynamicRoutes.Single(
            candidate =>
                candidate.Identity.IcId == "NT51928" &&
                candidate.Identity.WorkflowId == "dp-replace");
        BuiltInV2Registration registration =
            BuiltInV2RegistrationRegistry.DpReplaceByIc.Value["NT51928"];
        registration.TryCompile(
            0x40000,
            requestedTopology: null,
            [.. registration.InputSelectionGroupMemberSlotIds.Take(1)],
            out CompiledComposition? compiled,
            out IReadOnlyList<CompositionIssue> issues);
        CanonicalCapabilityCompilationContract expected =
            route.CompilationContract;

        var wrongMap = new CanonicalCapabilityCompilationContract(
            expected.ProfileId,
            expected.ProfileVersion,
            expected.TrustedDefinitionSha256,
            ["different-map"],
            expected.CompilerSemanticId,
            expected.SemanticBindingIds);
        var wrongCompiler = new CanonicalCapabilityCompilationContract(
            expected.ProfileId,
            expected.ProfileVersion,
            expected.TrustedDefinitionSha256,
            expected.AllowedMapVariantIds,
            "different-compiler-semantic",
            expected.SemanticBindingIds);
        var wrongSelectionGroup = new CanonicalCapabilityCompilationContract(
            expected.ProfileId,
            expected.ProfileVersion,
            expected.TrustedDefinitionSha256,
            expected.AllowedMapVariantIds,
            expected.CompilerSemanticId,
            ["different-selection-member"]);

        Assert.True(reload.Succeeded);
        Assert.Empty(issues);
        _ = Assert.Throws<ArgumentException>(() =>
            PublishWithContract(route, wrongMap).BindCompilation(compiled!));
        _ = Assert.Throws<ArgumentException>(() =>
            PublishWithContract(route, wrongCompiler).BindCompilation(compiled!));
        _ = Assert.Throws<ArgumentException>(() =>
            PublishWithContract(route, wrongSelectionGroup).BindCompilation(compiled!));
    }

    private static ResolvedCapabilityRoute PublishWithContract(
        ResolvedCapabilityRoute source,
        CanonicalCapabilityCompilationContract contract)
    {
        var definition = new CanonicalDynamicCapabilityDefinition(
            source.Identity,
            source.CapabilityFingerprint,
            contract,
            source.Authoring,
            source.Publication,
            source.Evidence);
        var catalog = new CanonicalCapabilityCatalog(
            new SingleCandidateSource(new CanonicalCapabilityCatalogCandidate(
                "semantic-drift-test",
                "1.0.0",
                new string('a', 64),
                [],
                [definition])));
        CapabilityCatalogReloadResult reload = catalog.Reload(
            TestContext.Current.CancellationToken);

        Assert.True(reload.Succeeded);
        return Assert.Single(reload.Snapshot!.DynamicRoutes);
    }
}

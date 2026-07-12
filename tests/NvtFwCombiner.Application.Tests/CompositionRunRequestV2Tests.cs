using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Tests that the current Application boundary rejects non-executable v2 plan artifacts.</summary>
public sealed class CompositionRunRequestV2Tests
{
    /// <summary>Verifies V2PlanCompiled cannot reach the current Preview or Build request boundary.</summary>
    [Fact]
    public void RequestRejectsV2PlanCompiledArtifact()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "v2-run",
            CreateV2PlanCompiled(),
            [],
            "output.bin"));

        Assert.Contains("not executable", exception.Message, StringComparison.Ordinal);
    }

    private static CompiledComposition CreateV2PlanCompiled()
    {
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap = CreateResolvedMap();
        var provenance = new V2CompilationProvenance(
            new ProfileBundleIdentity(
                "bundle-v2",
                "1.0.0",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "release-binding"),
            new ProfileBundleEntryIdentity(
                "profile-entry",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
            resolvedMap,
            new CompiledProfilePromotion(CompiledProfilePromotionStage.Compilable, []),
            ["profile-evidence"],
            [],
            []);
        var output = new CompiledOutputNamingRequirement(
            "{original-name}.bin",
            allowOverride: false,
            CompiledOutputInvalidCharacterPolicy.Reject,
            ["original-name"]);
        var identity = new V2CompiledCompositionIdentity(
            "profile-v2",
            "2.0.0",
            "standard-merge",
            CompositionKind.Merge,
            new V2CompiledCompositionDetails(provenance, CreateInputContract(), output));
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            [
                new AddressSpace(
                    "input",
                    4,
                    AddressSpaceMutability.Immutable,
                    allowedInputLengths: [4]),
                new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable),
            ],
            []);
        return CompiledComposition.CreateV2(
            plan,
            identity,
            CompiledIcNumberPolicy.NotApplicable);
    }

    private static CompiledInputContract CreateInputContract()
    {
        return new CompiledInputContract(
            [new CompiledInputSlotRequirement(
                "input-slot",
                "input",
                CompiledInputArtifactClass.ReferenceImage,
                required: true,
                CompiledInputSlotCardinality.ExactlyOne,
                [".bin"],
                new CompiledExactResolvedMapCapacityInputLengthRequirement(4),
                new CompiledNoInputNormalization())],
            [new CompiledInputSpaceBinding(
                "input",
                "input-slot",
                CompiledInputInstancePolicy.Singleton)]);
    }

    private static FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap CreateResolvedMap()
    {
        var map = FirmwareImageMap.CreateDirect(
            "map",
            "flash",
            new FirmwareMapApplicability(
                ["NT-SYNTHETIC"],
                ["standard"],
                TopologyRequirement.NoTopologyConstraint(),
                4),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [new FirmwareRegionSet(
                "physical",
                "flash",
                [new FirmwareRegion(
                    "root",
                    parentRegionId: null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, 4),
                    FirmwareWriteConstraint.Forbidden)],
                ["map-evidence"])],
            [],
            ["map-evidence"]);
        var definition = new FirmwareFamilyResolutionDefinition(
            "synthetic-family",
            "1.0.0",
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
            [map],
            []);
        FirmwareMapResolutionResult result = definition.ResolveMap(new FirmwareMapResolutionInputs(
            "NT-SYNTHETIC",
            "standard",
            4,
            requestedTopology: null,
            []));

        return Assert.IsType<FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap>(result.ResolvedMap);
    }
}

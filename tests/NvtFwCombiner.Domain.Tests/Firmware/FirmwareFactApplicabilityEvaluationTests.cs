using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using ResolvedFirmwareImageMap = NvtFwCombiner.Domain.Firmware.FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests fact-applicability evaluation against one immutable resolved map.</summary>
public sealed class FirmwareFactApplicabilityEvaluationTests
{
    private const string FamilyHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    /// <summary>Verifies mode, capacity, topology, and unavailable Common FW category remain closed resolved-map facts.</summary>
    [Fact]
    public void EvaluateChecksResolvedStaticSelectionsAndUnavailableRequirements()
    {
        ResolvedFirmwareImageMap resolvedMap = ResolveMap(2);

        Assert.Equal(
            FirmwareApplicabilityResult.Match,
            Applicability().Evaluate(resolvedMap));
        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            Applicability(modeIds: ["ab"]).Evaluate(resolvedMap));
        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            Applicability(capacityBytes: 32).Evaluate(resolvedMap));
        Assert.Equal(
            FirmwareApplicabilityResult.Pending,
            Applicability(topology: TopologyRequirement.RequireSingleChip()).Evaluate(resolvedMap));
        Assert.Equal(
            FirmwareApplicabilityResult.Pending,
            Applicability(categories: ["common"]).Evaluate(resolvedMap));
    }

    /// <summary>Verifies fact predicates evaluate only against successful resolver-owned decoded metadata values.</summary>
    [Fact]
    public void EvaluateChecksResolvedTypedMetadataPredicates()
    {
        ResolvedFirmwareImageMap resolvedMap = ResolveMap(2);

        Assert.Equal(
            FirmwareApplicabilityResult.Match,
            Applicability(predicate: Predicate(2)).Evaluate(resolvedMap));
        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            Applicability(predicate: Predicate(3)).Evaluate(resolvedMap));
        Assert.Equal(
            FirmwareApplicabilityResult.Pending,
            Applicability(predicate: new FirmwareMetadataPredicate(
                "missing-structure",
                "pid",
                FirmwareMetadataPredicateOperator.Equal,
                [FirmwareMetadataValue.FromUnsignedInteger(2)])).Evaluate(resolvedMap));
    }

    /// <summary>Verifies a known contradiction dominates unresolved requirements regardless of predicate declaration order.</summary>
    [Fact]
    public void EvaluatePrioritizesKnownNoMatchOverPendingRequirements()
    {
        ResolvedFirmwareImageMap resolvedMap = ResolveMap(2);
        FirmwareMetadataPredicate missing = new(
            "missing-structure",
            "pid",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromUnsignedInteger(2)]);
        FirmwareMetadataPredicate mismatch = Predicate(3);

        FirmwareFactApplicability[] cases =
        [
            Applicability(categories: ["common"], predicates: [missing, mismatch]),
            Applicability(topology: TopologyRequirement.RequireSingleChip(), predicates: [mismatch, missing]),
        ];

        Assert.All(cases, applicability => Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            applicability.Evaluate(resolvedMap)));
    }

    private static FirmwareFactApplicability Applicability(
        IReadOnlyList<string>? modeIds = null,
        TopologyRequirement? topology = null,
        long capacityBytes = 16,
        IReadOnlyList<string>? categories = null,
        FirmwareMetadataPredicate? predicate = null,
        IReadOnlyList<FirmwareMetadataPredicate>? predicates = null)
    {
        return new FirmwareFactApplicability(
            modeIds ?? ["standard"],
            topology ?? TopologyRequirement.NoTopologyConstraint(),
            capacityBytes,
            categories,
            predicates ?? (predicate is null ? [] : [predicate]));
    }

    private static FirmwareMetadataPredicate Predicate(ulong expectedValue)
    {
        return new FirmwareMetadataPredicate(
            "config",
            "pid",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromUnsignedInteger(expectedValue)]);
    }

    private static ResolvedFirmwareImageMap ResolveMap(byte pid)
    {
        var structure = new FirmwareMetadataStructure(
            "config",
            "tp-firmware",
            1,
            new FirmwareAbsoluteRangeLocator(
                new FirmwareAddressedRange("flash", new ByteRange(0, 1)),
                "root"),
            [new FirmwareMetadataField(
                "pid",
                0,
                1,
                FirmwareMetadataEncoding.UnsignedInteger,
                FirmwareMetadataByteOrder.LittleEndian)],
            []);
        var metadata = new FirmwareMetadataSet("metadata", [structure], ["metadata-evidence"]);
        FirmwareImageMap map = FirmwareImageMapTestFactory.CreateDirect(
            "map",
            "flash",
            new FirmwareMapApplicability(
                ["NT-SYNTHETIC"],
                ["standard"],
                TopologyRequirement.NoTopologyConstraint(),
                16),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [new FirmwareRegionSet(
                "physical",
                "flash",
                [new FirmwareRegion(
                    "root",
                    parentRegionId: null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, 16),
                    FirmwareWriteConstraint.Forbidden)],
                ["region-evidence"])],
            [metadata],
            ["map-evidence"]);
        var definition = new FirmwareFamilyResolutionDefinition(
            "synthetic-family",
            "1.0.0",
            FamilyHash,
            [map],
            [metadata]);
        FirmwareMapResolutionResult result = definition.ResolveMap(new FirmwareMapResolutionInputs(
            "NT-SYNTHETIC",
            "standard",
            16,
            requestedTopology: null,
            [new FirmwareArtifactPayload("tp-firmware", [pid])]));

        return Assert.IsType<ResolvedFirmwareImageMap>(result.ResolvedMap);
    }
}

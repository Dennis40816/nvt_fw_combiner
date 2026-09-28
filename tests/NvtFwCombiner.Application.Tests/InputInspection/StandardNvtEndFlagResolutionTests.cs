using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Tests.InputInspection;

/// <summary>
/// NVT-END-FLAG-1113-01 (F-1): a Standard CtrlRAM Base reads its event-buffer Backup at the NVT end flag declared by
/// the exact Standard layout or, without one, the single declaration every consensus Standard layout shares. No
/// candidate, disagreeing candidates or an undeclared layout are unresolved, so the read is skipped instead of
/// searching the whole image.
/// </summary>
public sealed class StandardNvtEndFlagResolutionTests
{
    private const string Fingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly ByteRange DeclaredEndFlag = new(0x1FFC, 4);

    /// <summary>The exact Standard layout supplies its own declaration, whatever the consensus candidates declare.</summary>
    [Fact]
    public void ExactStandardLayoutSuppliesItsDeclaration()
    {
        FirmwareNvtEndFlagResolution resolution = FirmwareArtifactClassificationResolver.ResolveStandardNvtEndFlag(
            Capability("exact", 0x1FFC), [Capability("consensus", 0x0FFC)]);

        Assert.Equal(DeclaredEndFlag, Assert.IsType<FirmwareNvtEndFlag>(resolution.EndFlag).Position.Range);
    }

    /// <summary>Without an exact layout, consensus layouts must share one declaration.</summary>
    [Fact]
    public void ConsensusLayoutsMustShareOneDeclaration()
    {
        FirmwareNvtEndFlagResolution agreed = FirmwareArtifactClassificationResolver.ResolveStandardNvtEndFlag(
            null, [Capability("first", 0x1FFC), Capability("second", 0x1FFC)]);
        FirmwareNvtEndFlagResolution inconsistent = FirmwareArtifactClassificationResolver.ResolveStandardNvtEndFlag(
            null, [Capability("first", 0x1FFC), Capability("second", 0x0FFC)]);

        Assert.Equal(DeclaredEndFlag, Assert.IsType<FirmwareNvtEndFlag>(agreed.EndFlag).Position.Range);
        Assert.Equal(FirmwareNvtEndFlagResolution.Unresolved, inconsistent);
    }

    /// <summary>No candidate or an undeclared layout outside the migration inventory resolves to nothing.</summary>
    [Fact]
    public void MissingOrUndeclaredLayoutsAreUnresolved()
    {
        Assert.Equal(FirmwareNvtEndFlagResolution.Unresolved,
            FirmwareArtifactClassificationResolver.ResolveStandardNvtEndFlag(null, null));
        Assert.Equal(FirmwareNvtEndFlagResolution.Unresolved,
            FirmwareArtifactClassificationResolver.ResolveStandardNvtEndFlag(null, []));
        Assert.Equal(FirmwareNvtEndFlagResolution.Unresolved,
            FirmwareArtifactClassificationResolver.ResolveStandardNvtEndFlag(Capability("exact", null), null));
        Assert.Equal(FirmwareNvtEndFlagResolution.Unresolved,
            FirmwareArtifactClassificationResolver.ResolveStandardNvtEndFlag(
                null, [Capability("first", 0x1FFC), Capability("second", null)]));
    }

    private static ResolvedCapability Capability(string name, long? endFlagStart)
    {
        const long capacity = 0x2000;
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", capacity),
            [
                new AddressSpace("reference-base", capacity, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", capacity, AddressSpaceMutability.Mutable),
            ],
            [CompositionOperation.CopyRange(
                "copy-reference",
                10,
                "reference-base",
                new ByteRange(0, 4),
                "output-image",
                new ByteRange(0, 4),
                OverlapPolicy.ReplaceExisting,
                "Synthetic Standard layout.")]);
        CompiledComposition composition = CompiledCompositionTestFactory.Create(
            plan,
            new TestCompiledCompositionIdentity(
                $"synthetic-{name}",
                "1.0.0",
                "NT-SYNTHETIC",
                ExperienceIds.StandardMerge,
                ExperienceIds.StandardMerge,
                CompositionKind.Merge),
            $"synthetic-{name}.bin",
            mapId: $"{name}-map",
            nvtEndFlagStart: endFlagStart);
        var identity = new CapabilityRouteIdentity("NT-SYNTHETIC", ExperienceIds.StandardMerge, "none", $"{name}-map");
        var token = new ResolutionToken($"standard-end-flag-{name}");
        return new ResolvedCapability(
            identity,
            Fingerprint,
            composition,
            Decision(identity, CapabilityAuthoringAvailability.Available),
            Decision(identity, CapabilityPublicationStatus.TestOnly),
            Decision(identity, CapabilityEvidenceStatus.SyntheticOracle),
            MetadataPlanDefinition.Empty.Resolve(token),
            token);
    }

    private static PinnedCapabilityDecision<T> Decision<T>(CapabilityRouteIdentity identity, T value)
        where T : struct, Enum
    {
        return new PinnedCapabilityDecision<T>(
            $"standard-end-flag-{typeof(T).Name}",
            identity.RouteId,
            Fingerprint,
            value,
            "synthetic-standard-end-flag");
    }
}

namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Read-only full-image consumers remain inside the existing catalog, builder and metadata resolver.</summary>
    [Fact]
    public void FullImageMetadataReusesCanonicalOwnersWithoutDpExecution()
    {
        string inventory = ReadText("src/NvtFwCombiner.Infrastructure/Composition/CanonicalFullImageMetadataInventory.cs");
        string authority = ReadText("src/NvtFwCombiner.Application/Composition/FirmwareMetadataPlanAuthority.cs");
        string source = ReadText("src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCatalogSource.cs");
        string resolver = ReadText("src/NvtFwCombiner.Domain/Firmware/FirmwareFamilyResolutionDefinition.MetadataResolution.cs");
        string inspector = ReadText("src/NvtFwCombiner.Application/Metadata/FirmwareMetadataInspection.cs");
        Assert.DoesNotContain("DpReplace", inventory, StringComparison.Ordinal);
        Assert.DoesNotContain("TryCompile", inventory, StringComparison.Ordinal);
        Assert.DoesNotContain("ExperienceIds.DpReplace", authority, StringComparison.Ordinal);
        Assert.Contains("ResolveFullImageMetadataPlan(", authority, StringComparison.Ordinal);
        Assert.Contains("loadFullImageMetadataPlans()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ResolvedFirmwareImageMap", resolver, StringComparison.Ordinal);
        Assert.Contains("FirmwareArtifactPayload fullImage", resolver, StringComparison.Ordinal);
        Assert.Contains("FirmwareArtifactPayload fullImage", inspector, StringComparison.Ordinal);
        Assert.DoesNotContain("switch (icId)", inventory + authority + inspector, StringComparison.Ordinal);
    }
}

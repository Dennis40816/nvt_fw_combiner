using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>
/// NVT-END-FLAG-1113-01 (ADR 0076): a map declares its NVT end flag only through canonical Backup locators whose search
/// range is exactly the marker length; resolution succeeds without one only for the named migration inventory.
/// </summary>
public sealed class FirmwareNvtEndFlagTests
{
    private const string FamilyHash = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private const long Capacity = 0x2000;
    private static readonly byte[] Nvt = [0x00, 0x4E, 0x56, 0x54];
    private static readonly byte[] OtherMarker = [0x41, 0x42, 0x43, 0x44];

    /// <summary>Every NVT locator at the same 4-byte range declares the end flag.</summary>
    [Fact]
    public void ExactNvtLocatorsDeclareTheEndFlag()
    {
        FirmwareFamilyResolutionDefinition family = Family("synthetic-family",
            Structure("tp", Locator(0x1FFC, 4)), Structure("reference", Locator(0x1FFC, 4)));

        FirmwareNvtEndFlagResolution resolution = family.ResolveNvtEndFlag("map");

        Assert.True(resolution.Succeeded);
        Assert.False(resolution.UsesLegacyCompatibilityRead);
        Assert.Equal(new FirmwareAddressedRange("flash", new ByteRange(0x1FFC, 4)),
            Assert.IsType<FirmwareNvtEndFlag>(resolution.EndFlag).Position);
    }

    /// <summary>A bounded search range or no NVT locator fails outside the migration inventory.</summary>
    [Fact]
    public void UndeclaredLayoutOutsideTheInventoryIsUnresolved()
    {
        Assert.Equal(FirmwareNvtEndFlagResolution.Unresolved,
            Family("synthetic-family", Structure("tp", Locator(0x1000, 0x1000))).ResolveNvtEndFlag("map"));
        Assert.Equal(FirmwareNvtEndFlagResolution.Unresolved,
            Family("synthetic-family", Structure("tp", Locator(0x1FFC, 4, marker: OtherMarker))).ResolveNvtEndFlag("map"));
    }

    /// <summary>An inventory family keeps the existing compatibility read, with or without an NVT locator.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InventoryFamilyWithoutADeclarationUsesTheCompatibilityRead(bool withBoundedLocator)
    {
        string familyId = FirmwareNvtEndFlagMigration.LegacyCompatibilityFamilyIds.Order(StringComparer.Ordinal).First();
        FirmwareFamilyResolutionDefinition family = withBoundedLocator
            ? Family(familyId, Structure("tp", Locator(0x1000, 0x1000)))
            : Family(familyId);

        FirmwareNvtEndFlagResolution resolution = family.ResolveNvtEndFlag("map");

        Assert.True(resolution.Succeeded);
        Assert.True(resolution.UsesLegacyCompatibilityRead);
        Assert.Null(resolution.EndFlag);
    }

    /// <summary>A 4-byte range is a declaration only with unique selection and the -0xFFC Backup offset.</summary>
    [Fact]
    public void ExactRangeWithAnotherOffsetOrSelectionIsNoDeclaration()
    {
        Assert.Equal(FirmwareNvtEndFlagResolution.Unresolved,
            Family("synthetic-family", Structure("tp", Locator(0x1FFC, 4, resultOffset: -0x1000)))
                .ResolveNvtEndFlag("map"));
        Assert.Equal(FirmwareNvtEndFlagResolution.Unresolved,
            Family("synthetic-family", Structure("tp", Locator(0x1FFC, 4,
                selection: new FirmwareTerminalMarkerSelection(FirmwareMarkerTerminal.LowestAddress, 1))))
                .ResolveNvtEndFlag("map"));
    }

    /// <summary>A map declaring the end flag for only some NVT locators, or at two positions, is rejected at build.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartialOrSplitDeclarationIsRejectedWhenTheFamilyIsBuilt(bool split)
    {
        _ = Assert.ThrowsAny<ArgumentException>(() => Family("synthetic-family",
            Structure("tp", Locator(0x1FFC, 4)),
            Structure("reference", split ? Locator(0x0FFC, 4) : Locator(0x1000, 0x1000))));
    }

    /// <summary>Several candidates share one resolution only when all resolve alike; otherwise none.</summary>
    [Fact]
    public void CommonResolutionRequiresAgreement()
    {
        FirmwareNvtEndFlagResolution declared = Family("synthetic-family", Structure("tp", Locator(0x1FFC, 4)))
            .ResolveNvtEndFlag("map");
        FirmwareNvtEndFlagResolution other = Family("synthetic-family", Structure("tp", Locator(0x0FFC, 4)))
            .ResolveNvtEndFlag("map");

        Assert.Equal(declared, FirmwareNvtEndFlagResolution.Common([declared, declared]));
        Assert.Equal(FirmwareNvtEndFlagResolution.Unresolved, FirmwareNvtEndFlagResolution.Common([declared, other]));
        Assert.Equal(FirmwareNvtEndFlagResolution.Unresolved,
            FirmwareNvtEndFlagResolution.Common([declared, FirmwareNvtEndFlagResolution.LegacyCompatibility]));
        Assert.Equal(FirmwareNvtEndFlagResolution.Unresolved,
            FirmwareNvtEndFlagResolution.Common([FirmwareNvtEndFlagResolution.Unresolved]));
        Assert.Equal(FirmwareNvtEndFlagResolution.Unresolved, FirmwareNvtEndFlagResolution.Common([]));
        Assert.Equal(FirmwareNvtEndFlagResolution.LegacyCompatibility, FirmwareNvtEndFlagResolution.Common(
            [FirmwareNvtEndFlagResolution.LegacyCompatibility, FirmwareNvtEndFlagResolution.LegacyCompatibility]));
    }

    private static FirmwareFamilyResolutionDefinition Family(string familyId, params FirmwareMetadataStructure[] structures)
    {
        FirmwareMetadataSet[] sets = structures.Length == 0 ? [] : [new FirmwareMetadataSet("fwconfig", structures, ["evidence"])];
        FirmwareImageMap map = FirmwareImageMapTestFactory.CreateDirect(
            "map",
            "flash",
            new FirmwareMapApplicability(["NT00001"], ["standard"], TopologyRequirement.NoTopologyConstraint(), Capacity),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [new FirmwareRegionSet(
                "physical",
                "flash",
                [new FirmwareRegion("root", null, FirmwareRegionOwner.System, FirmwareRegionKind.Image,
                    new ByteRange(0, Capacity), FirmwareWriteConstraint.Forbidden)],
                ["evidence"])],
            sets,
            ["evidence"]);
        return new FirmwareFamilyResolutionDefinition(familyId, "1.0.0", FamilyHash, [map], sets);
    }

    private static FirmwareMetadataStructure Structure(string bindingId, FirmwareMarkerRelativeLocator locator)
    {
        // A terminal (non-unique) marker selection requires one structure assertion.
        FirmwareMetadataByteAssertion[] assertions = locator.Selection is FirmwareUniqueMarkerSelection
            ? []
            : [FirmwareMetadataByteAssertion.Exact(0, [0x00])];
        return new FirmwareMetadataStructure($"{bindingId}-backup", bindingId, 0x80, locator, [], assertions);
    }

    private static FirmwareMarkerRelativeLocator Locator(
        long searchStart,
        long searchLength,
        long resultOffset = FirmwareNvtEndFlag.BackupStartOffsetFromMarker,
        FirmwareMarkerSelection? selection = null,
        byte[]? marker = null)
    {
        return new FirmwareMarkerRelativeLocator(
            new FirmwareAddressedRange("flash", new ByteRange(searchStart, searchLength)),
            marker ?? Nvt,
            selection ?? new FirmwareUniqueMarkerSelection(),
            resultOffset,
            "root");
    }
}

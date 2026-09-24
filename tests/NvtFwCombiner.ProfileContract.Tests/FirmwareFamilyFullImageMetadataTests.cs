using System.Text.Json;
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.FirmwareFamilies;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class FirmwareFamilyResolutionNormalizerTests
{
    /// <summary>Wire declarations normalize through the existing family owner into exact canonical references.</summary>
    [Fact]
    public void FullImageMetadataWireRoundTripPreservesCanonicalReferences()
    {
        FirmwareFamilyDocument source = Document(includeRelations: true) with { FullImageMetadataViews = [FullImageView()] };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        string json = JsonSerializer.Serialize(source, options);
        FirmwareFamilyDocument restored = Assert.IsType<FirmwareFamilyDocument>(JsonSerializer.Deserialize<FirmwareFamilyDocument>(json, options));
        FirmwareFamilyResolutionDefinition family = FirmwareFamilyResolutionNormalizer.Normalize(restored, FamilyHash);
        FirmwareFullImageMetadataView view = Assert.Single(family.FullImageMetadataViews!);
        Assert.Same(Assert.Single(family.ImageMaps), view.ImageMap);
        FirmwareMetadataStructure structure = Assert.Single(family.GetStructuresForMap("map"));
        FirmwareFullImageMetadataBinding binding = Assert.Single(view.MetadataBindings);
        Assert.Same(structure, binding.Structure);
        Assert.Same(structure.Definition, binding.Structure.Definition);
        Assert.Equal("tp-firmware", binding.Structure.ArtifactBindingId);
        _ = Assert.Single(structure.Relations);
        Assert.Equal("chip-number", Assert.Single(binding.TargetReferences).TargetId);
    }

    /// <summary>Absence, an empty collection, and an explicitly empty view remain distinct.</summary>
    [Fact]
    public void FullImageMetadataPreservesAbsentAndExplicitEmpty()
    {
        FirmwareFamilyDocument source = Document();
        Assert.Null(FirmwareFamilyResolutionNormalizer.Normalize(source, FamilyHash).FullImageMetadataViews);
        Assert.Empty(FirmwareFamilyResolutionNormalizer.Normalize(source with { FullImageMetadataViews = [] }, FamilyHash).FullImageMetadataViews!);
        FirmwareFamilyResolutionDefinition empty = FirmwareFamilyResolutionNormalizer.Normalize(source with
        {
            FullImageMetadataViews = [FullImageView() with { MetadataBindings = [] }],
        }, FamilyHash);
        Assert.Empty(Assert.Single(empty.FullImageMetadataViews!).MetadataBindings);
    }

    /// <summary>Exact references preserve provider definition identity through the additional view.</summary>
    [Fact]
    public void FullImageMetadataRetainsExternalDefinitionIdentity()
    {
        (FirmwareFamilyDocument source, FirmwareMetadataStructureDefinition provider,
            FirmwareMetadataStructureDefinitionReferenceDocument reference) = ReferencedDocument();
        FirmwareFamilyResolutionDefinition family = FirmwareFamilyResolutionNormalizer.Normalize(source with
        {
            FullImageMetadataViews = [FullImageView()],
        }, FamilyHash, new ExactDefinitionResolver(reference, provider));
        Assert.Same(provider, Assert.Single(Assert.Single(family.FullImageMetadataViews!).MetadataBindings).Structure.Definition);
    }

    /// <summary>Targets use the existing typed definition's span/field/series/group owner.</summary>
    [Theory]
    [InlineData("span", "header")]
    [InlineData("field", "raw")]
    [InlineData("series", "dlm-records")]
    [InlineData("group", "header-values")]
    public void FullImageMetadataAcceptsTypedTargets(string kind, string targetId)
    {
        FirmwareFamilyDocument source = WithTpFlashHeader(Document(includePredicate: false), "tp-flash-header", TpFlashHeaderPayload());
        FirmwareFullImageMetadataViewDocument view = FullImageView();
        source = source with
        {
            FullImageMetadataViews = [view with
            {
                MetadataBindings = [view.MetadataBindings[0] with { TargetReferences = [new(kind, targetId)] }],
            }],
        };
        FirmwareFamilyResolutionDefinition family = FirmwareFamilyResolutionNormalizer.Normalize(source, FamilyHash);
        Assert.Equal(targetId, Assert.Single(Assert.Single(Assert.Single(family.FullImageMetadataViews!).MetadataBindings).TargetReferences).TargetId);
    }

    /// <summary>Malformed and ambiguous declarations fail through the real normalizer boundary.</summary>
    [Theory]
    [InlineData("unknown-map")]
    [InlineData("unknown-member")]
    [InlineData("unknown-structure")]
    [InlineData("unknown-target")]
    [InlineData("unknown-kind")]
    [InlineData("duplicate-member")]
    [InlineData("duplicate-binding")]
    [InlineData("duplicate-structure")]
    [InlineData("duplicate-target")]
    [InlineData("duplicate-view")]
    [InlineData("ambiguous-view")]
    [InlineData("empty-target")]
    [InlineData("empty-evidence")]
    public void FullImageMetadataRejectsInvalidDeclarations(string mutation)
    {
        FirmwareFullImageMetadataViewDocument view = FullImageView();
        FirmwareFullImageMetadataBindingDocument binding = view.MetadataBindings[0];
        view = mutation switch
        {
            "unknown-map" => view with { MapId = "unknown" },
            "unknown-member" => view with { MemberIds = ["NT99999"] },
            "unknown-structure" => view with { MetadataBindings = [binding with { StructureId = "unknown" }] },
            "unknown-target" => view with { MetadataBindings = [binding with { TargetReferences = [new("field", "unknown")] }] },
            "unknown-kind" => view with { MetadataBindings = [binding with { TargetReferences = [new("policy", "chip-number")] }] },
            "duplicate-member" => view with { MemberIds = ["NT00001", "NT00001"] },
            "duplicate-binding" => view with { MetadataBindings = [binding, binding] },
            "duplicate-structure" => view with { MetadataBindings = [binding, binding with { BindingId = "other" }] },
            "duplicate-target" => view with { MetadataBindings = [binding with { TargetReferences = [binding.TargetReferences[0], binding.TargetReferences[0]] }] },
            "empty-target" => view with { MetadataBindings = [binding with { TargetReferences = [] }] },
            "empty-evidence" => view with { EvidenceRefs = [] },
            _ => view,
        };
        FirmwareFullImageMetadataViewDocument[] views = mutation switch
        {
            "duplicate-view" => [view, view],
            "ambiguous-view" => [view, view with { ViewId = "other" }],
            _ => [view],
        };
        _ = Assert.Throws<FirmwareFamilyNormalizationException>(() => FirmwareFamilyResolutionNormalizer.Normalize(
            Document() with { FullImageMetadataViews = views }, FamilyHash));
    }

    /// <summary>Canonical dependency validation and view closure run together without resolving runtime values.</summary>
    [Theory]
    [InlineData("complete", true)]
    [InlineData("missing-binding", false)]
    [InlineData("unknown-prerequisite", false)]
    [InlineData("cycle", false)]
    public void FullImageMetadataRequiresCanonicalPrerequisiteClosure(string mutation, bool valid)
    {
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareMetadataSetDocument set = source.MetadataSets[0];
        FirmwareMetadataStructureDocument root = set.Structures[0];
        FirmwareMetadataLocatorDocument locator = new("metadata-field-selected", "config-region",
            ResultOffset: Number("0"), PrerequisiteStructureId: mutation == "unknown-prerequisite" ? "unknown" : "config",
            PrerequisiteFieldId: "chip-number", Branches: [new(Number("0"), Number("15"), AddressedRange(0, 4))]);
        FirmwareMetadataStructureDocument dependent = root with { StructureId = "dependent", Locator = locator };
        if (mutation == "cycle")
        {
            root = root with { Locator = locator with { PrerequisiteStructureId = "dependent" } };
        }

        FirmwareFullImageMetadataBindingDocument binding = FullImageView().MetadataBindings[0];
        FirmwareFullImageMetadataBindingDocument child = binding with { BindingId = "dependent", StructureId = "dependent" };
        source = source with
        {
            MetadataSets = [set with { Structures = [root, dependent] }],
            FullImageMetadataViews = [FullImageView() with
            {
                MetadataBindings = mutation == "missing-binding" ? [child] : [binding, child],
            }],
        };
        if (valid)
        {
            FirmwareFamilyResolutionDefinition family = FirmwareFamilyResolutionNormalizer.Normalize(source, FamilyHash);
            Assert.Equal(2, Assert.Single(family.FullImageMetadataViews!).MetadataBindings.Count);
        }
        else
        {
            _ = Assert.Throws<FirmwareFamilyNormalizationException>(() => FirmwareFamilyResolutionNormalizer.Normalize(source, FamilyHash));
        }
    }

    private static FirmwareFullImageMetadataViewDocument FullImageView()
    {
        return new("full-image", "map", ["NT00001"],
            [new("config-view", "config", [new("field", "chip-number")], ["binding-evidence"])], ["view-evidence"]);
    }
}

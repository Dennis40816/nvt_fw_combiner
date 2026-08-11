using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.FirmwareFamilies;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class FirmwareFamilyResolutionNormalizerTests
{
    private const string ProviderFamilyId = "canonical-provider";
    private const string ProviderFamilyVersion = "2.0.0";
    private const string ProviderFamilyHash =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    /// <summary>
    /// An exact reference reuses the provider's immutable definition instance
    /// while retaining the consumer binding and locator.
    /// </summary>
    [Fact]
    public void NormalizeExactDefinitionReferenceReusesProviderIdentity()
    {
        (
            FirmwareFamilyDocument consumer,
            FirmwareMetadataStructureDefinition provider,
            FirmwareMetadataStructureDefinitionReferenceDocument reference) =
            ReferencedDocument();
        var resolver = new ExactDefinitionResolver(reference, provider);

        FirmwareFamilyResolutionDefinition definition =
            FirmwareFamilyResolutionNormalizer.Normalize(
                consumer,
                FamilyHash,
                resolver);

        FirmwareMetadataStructure structure =
            Assert.Single(definition.GetStructuresForMap("map"));
        Assert.Same(provider, structure.Definition);
        Assert.Equal("config", structure.StructureId);
        Assert.Equal("tp-firmware", structure.ArtifactBindingId);
    }

    /// <summary>
    /// Family, version, content hash, and logical structure identity are all
    /// exact trust inputs; none may be inferred or aliased.
    /// </summary>
    [Theory]
    [InlineData("family")]
    [InlineData("version")]
    [InlineData("hash")]
    [InlineData("structure")]
    public void NormalizeRejectsMismatchedDefinitionReference(string mismatch)
    {
        (
            FirmwareFamilyDocument consumer,
            FirmwareMetadataStructureDefinition provider,
            FirmwareMetadataStructureDefinitionReferenceDocument reference) =
            ReferencedDocument();
        var resolver = new ExactDefinitionResolver(reference, provider);
        FirmwareMetadataStructureDocument structure =
            Assert.Single(Assert.Single(consumer.MetadataSets).Structures);
        FirmwareMetadataStructureDefinitionReferenceDocument source =
            Assert.IsType<FirmwareMetadataStructureDefinitionReferenceDocument>(
                structure.DefinitionReference);
        FirmwareMetadataStructureDefinitionReferenceDocument changed =
            mismatch switch
            {
                "family" => source with { FamilyId = "unknown-provider" },
                "version" => source with { FamilyVersion = "2.0.1" },
                "hash" => source with
                {
                    FamilyContentHash =
                        "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                },
                "structure" => source with { StructureId = "other-config" },
                _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
            };
        FirmwareFamilyDocument mismatched = consumer with
        {
            MetadataSets =
            [
                Assert.Single(consumer.MetadataSets) with
                {
                    Structures =
                    [
                        structure with { DefinitionReference = changed },
                    ],
                },
            ],
        };

        FirmwareFamilyNormalizationException exception =
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.Normalize(
                    mismatched,
                    FamilyHash,
                    resolver));

        Assert.Equal(
            "metadataSets[metadata].structures[0].definitionReference",
            exception.Path);
    }

    private static (
        FirmwareFamilyDocument Consumer,
        FirmwareMetadataStructureDefinition Provider,
        FirmwareMetadataStructureDefinitionReferenceDocument Reference)
        ReferencedDocument()
    {
        FirmwareFamilyDocument source = Document();
        FirmwareMetadataSetDocument set = Assert.Single(source.MetadataSets);
        FirmwareMetadataStructureDocument structure =
            Assert.Single(set.Structures);
        FirmwareMetadataStructureDefinition provider =
            Assert.Single(
                FirmwareFamilyResolutionNormalizer.Normalize(source, FamilyHash)
                    .GetStructuresForMap("map"))
                .Definition;
        var reference = new FirmwareMetadataStructureDefinitionReferenceDocument(
            ProviderFamilyId,
            ProviderFamilyVersion,
            ProviderFamilyHash,
            provider.DefinitionId);
        FirmwareFamilyDocument consumer = source with
        {
            MetadataSets =
            [
                set with
                {
                    Structures =
                    [
                        structure with
                        {
                            Length = default,
                            Fields = null!,
                            Assertions = null!,
                            Relations = null,
                            DefinitionReference =
                                new FirmwareMetadataStructureDefinitionReferenceDocument(
                                    reference.FamilyId,
                                    reference.FamilyVersion,
                                    reference.FamilyContentHash,
                                    reference.StructureId),
                        },
                    ],
                },
            ],
        };
        return (consumer, provider, reference);
    }

    private sealed class ExactDefinitionResolver(
        FirmwareMetadataStructureDefinitionReferenceDocument expected,
        FirmwareMetadataStructureDefinition definition)
        : IFirmwareMetadataStructureDefinitionResolver
    {
        public bool TryResolve(
            FirmwareMetadataStructureDefinitionReferenceDocument reference,
            out FirmwareMetadataStructureDefinition? resolved)
        {
            if (reference == expected)
            {
                resolved = definition;
                return true;
            }

            resolved = null;
            return false;
        }
    }
}

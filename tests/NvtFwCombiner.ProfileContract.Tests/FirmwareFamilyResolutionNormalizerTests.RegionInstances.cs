using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Profiles.FirmwareFamilies;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class FirmwareFamilyResolutionNormalizerTests
{
    /// <summary>Verifies direct normalization cannot accept an orphan template or instance declaration.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NormalizeRequiresRegionTemplatesAndInstancesTogether(bool templateOnly)
    {
        FirmwareFamilyDocument source = Document();
        FirmwareRegionSetDocument regionSet = Assert.Single(source.RegionSets);
        var template = new FirmwareRegionTemplateDocument(
            "bank",
            Number("16"),
            [
                new FirmwareRegionDocument(
                    "bank",
                    "system",
                    "image",
                    Range(0, 16),
                    "explicit-range",
                    Number("1")),
            ]);
        var instance = new FirmwareRegionInstanceDocument(
            "a-bank",
            "bank",
            Number("0"),
            [new FirmwareRegionIdBindingDocument("bank", "a-bank")],
            "root");
        FirmwareFamilyDocument document = source with
        {
            SchemaVersion = "1.2",
            RegionSets =
            [
                regionSet with
                {
                    RegionTemplates = templateOnly ? [template] : null,
                    RegionInstances = templateOnly ? null : [instance],
                },
            ],
        };

        FirmwareFamilyNormalizationException exception =
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal("regionSets[physical]", exception.Path);
    }
}

using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Checks canonical instance-relative physical region definitions.</summary>
public sealed class FirmwareRegionTemplateTests
{
    /// <summary>Verifies one bank definition produces exact A/B absolute projections.</summary>
    [Fact]
    public void OneRelativeBankDefinitionExpandsAtTwoDeclaredBases()
    {
        var bank = new FirmwareRegionTemplate(
            "ab-bank",
            0x40000,
            [
                new FirmwareRelativeRegion(
                    "bank",
                    null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, 0x40000),
                    FirmwareWriteConstraint.ExplicitRange),
                new FirmwareRelativeRegion(
                    "dpcmi",
                    "bank",
                    FirmwareRegionOwner.Dp,
                    FirmwareRegionKind.Command,
                    new ByteRange(0x401A, 3),
                    FirmwareWriteConstraint.ExplicitRange),
                new FirmwareRelativeRegion(
                    "tp-code",
                    "bank",
                    FirmwareRegionOwner.Tp,
                    FirmwareRegionKind.Code,
                    new ByteRange(0x7000, 0x39000),
                    FirmwareWriteConstraint.WholeRegion),
            ]);
        var a = new FirmwareRegionInstance(
            "a-bank",
            bank,
            0,
            "ab-image",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["bank"] = "a-bank",
                ["dpcmi"] = "a-cmi-dp-version",
                ["tp-code"] = "tpa-code",
            });
        var b = new FirmwareRegionInstance(
            "b-bank",
            bank,
            0x40000,
            "ab-image",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["bank"] = "b-bank",
                ["dpcmi"] = "b-cmi-dp-version",
                ["tp-code"] = "tpb-code",
            });
        var regions = new FirmwareRegionSet(
            "ab-regions",
            "flash",
            [
                new FirmwareRegion(
                    "ab-image",
                    null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, 0x80000),
                    FirmwareWriteConstraint.ExplicitRange),
            ],
            ["owner-ab-map"],
            [bank],
            [a, b]);

        Assert.Same(bank, a.Template);
        Assert.Same(bank, b.Template);
        Assert.Equal(0x40000, b.BaseOffset - a.BaseOffset);
        Assert.Equal(new ByteRange(0x401A, 3), Region("a-cmi-dp-version").Range);
        Assert.Equal(new ByteRange(0x4401A, 3), Region("b-cmi-dp-version").Range);
        Assert.Equal(new ByteRange(0x7000, 0x39000), Region("tpa-code").Range);
        Assert.Equal(new ByteRange(0x47000, 0x39000), Region("tpb-code").Range);

        FirmwareRegion Region(string id)
        {
            return regions.Regions.Single(region => StringComparer.Ordinal.Equals(region.RegionId, id));
        }
    }

    /// <summary>Verifies an instance cannot silently omit template geometry identity.</summary>
    [Fact]
    public void InstanceRequiresOneResolvedIdForEveryTemplateRegion()
    {
        var bank = new FirmwareRegionTemplate(
            "ab-bank",
            0x40000,
            [
                new FirmwareRelativeRegion(
                    "bank",
                    null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, 0x40000),
                    FirmwareWriteConstraint.ExplicitRange),
                new FirmwareRelativeRegion(
                    "tp-code",
                    "bank",
                    FirmwareRegionOwner.Tp,
                    FirmwareRegionKind.Code,
                    new ByteRange(0x7000, 0x39000),
                    FirmwareWriteConstraint.WholeRegion),
            ]);

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new FirmwareRegionInstance(
                "a-bank",
                bank,
                0,
                "ab-image",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["bank"] = "a-bank",
                }));

        Assert.Contains("exactly one resolved id", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies resolved-id bindings are copied and exposed through an immutable wrapper.</summary>
    [Fact]
    public void InstanceSnapshotsResolvedIdsWithoutExposingMutableDictionary()
    {
        var bank = new FirmwareRegionTemplate(
            "ab-bank",
            16,
            [
                new FirmwareRelativeRegion(
                    "bank",
                    null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, 16),
                    FirmwareWriteConstraint.ExplicitRange),
            ]);
        var source = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bank"] = "a-bank",
        };
        var instance = new FirmwareRegionInstance("a-bank", bank, 0, null, source);

        source["bank"] = "changed";

        Assert.Equal("a-bank", instance.ResolvedRegionIds["bank"]);
        IDictionary<string, string> exposed =
            Assert.IsType<IDictionary<string, string>>(
                instance.ResolvedRegionIds,
                exactMatch: false);
        _ = Assert.Throws<NotSupportedException>(() => exposed["bank"] = "changed-again");
    }
}

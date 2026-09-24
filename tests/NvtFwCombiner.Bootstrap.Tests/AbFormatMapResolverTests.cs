using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Characterizes the shared selection contract without claiming input or execution admission.</summary>
public sealed class AbFormatMapResolverTests
{
    /// <summary>Map labels use exact canonical associations, never unrelated maps or arbitrary special names.</summary>
    [Theory]
    [InlineData("shared", "Common")]
    [InlineData("special", "Vendor layout")]
    [InlineData("ambiguous", null)]
    [InlineData("unassociated", "Common")]
    [InlineData("no-policy", "Common")]
    public void SelectedMapLabelRetainsCanonicalDeclaration(string scenario, string? expected)
    {
        FirmwareFamilyResolutionDefinition original = Family();
        FirmwareAbFormatPolicy policy = original.AbFormatPolicy!;
        const string mapId = "nt51950-ab-merge-512k";
        var formats = new List<FirmwareAbFormatDefinition>
        {
            new("desay", "Vendor layout", [0x97, 0xA6]),
        };
        var variants = policy.Variants.Where(variant => variant.MapId != mapId ||
            scenario == "shared" || (scenario is "special" or "ambiguous" && variant.FormatId != policy.CommonFormatId)).ToList();
        if (scenario == "ambiguous")
        {
            formats.Add(new("second-special", "Another layout", [0x20]));
            variants.Add(new("NT51950", "second-special", mapId));
        }
        var definition = new FirmwareFamilyResolutionDefinition(original.FamilyId, original.FamilyVersion, original.FamilyContentHash,
            original.ImageMaps, original.MetadataSets, original.CapabilityBindings, original.FamilyRelationships,
            scenario == "no-policy" ? null : new FirmwareAbFormatPolicy(policy.ScopeId, policy.CommonFormatId,
                policy.CommonDisplayName, formats, policy.PrimaryBindings, variants));
        FirmwareMapResolutionResult result = definition.ResolveMapWithinForProfile(
            new FirmwareMapResolutionInputs("NT51950", "ab-merge", 0x80000, Topology(1), []),
            new HashSet<string>(StringComparer.Ordinal) { mapId }, new HashSet<string>(StringComparer.Ordinal));
        Assert.Equal(FirmwareMapResolutionStatus.Unique, result.Status);
        Assert.Equal(mapId, result.ResolvedMap!.ImageMap.MapId);
        Assert.Equal(expected, result.ResolvedMap.DisplayName);
    }

    /// <summary>Disabled vendor specialization preserves labels while selecting the common physical map.</summary>
    [Theory]
    [InlineData("NT51950", 1, "nt51950-ab-merge-512k")]
    [InlineData("NT51950", 2, "nt51950-ab-merge-1024k")]
    [InlineData("NT51950", 3, "nt51950-ab-merge-1024k")]
    [InlineData("NT51951", 0, "nt51951-ab-merge-1024k")]
    public void DisabledSpecializationUsesCommonMap(string member, int count, string expectedMap)
    {
        FirmwareFamilyResolutionDefinition family = Family();
        AbFormatMapResolutionResult common = AbFormatMapResolver.Resolve(family, member, Configuration(family),
            0x84, 0x85, Topology(count), count, count);
        AbFormatMapResolutionResult desay = AbFormatMapResolver.Resolve(family, member, Configuration(family),
            0x97, 0xA6, Topology(count), count, count);

        Assert.Empty(common.Issues);
        Assert.Empty(desay.Issues);
        Assert.Equal(expectedMap, common.Selection!.MapId);
        Assert.Equal(expectedMap, desay.Selection!.MapId);
        Assert.Equal("common", common.Selection.FormatId);
        Assert.Equal("desay", desay.Selection.FormatId);
    }

    /// <summary>Every configured family map retains its explicit result, independent of a page or artifact DTO.</summary>
    [Theory]
    [InlineData("NT51950", 1, 1, 1, 0x84, 0x85, "common", "Common", "nt51950-ab-merge-512k")]
    [InlineData("NT51950", 2, 2, 2, 0x84, 0x85, "common", "Common", "nt51950-ab-merge-1024k")]
    [InlineData("NT51950", 2, 3, 3, 0x84, 0x85, "common", "Common", "nt51950-ab-merge-1024k")]
    [InlineData("NT51950", 2, 2, 3, 0x84, 0x85, "common", "Common", "nt51950-ab-merge-1024k")]
    [InlineData("NT51950", 2, 3, 2, 0x84, 0x85, "common", "Common", "nt51950-ab-merge-1024k")]
    [InlineData("NT51950", 2, null, 2, 0x84, 0x85, "common", "Common", "nt51950-ab-merge-1024k")]
    [InlineData("NT51950", 2, 2, null, 0x84, 0x85, "common", "Common", "nt51950-ab-merge-1024k")]
    [InlineData("NT51950", 1, 1, 1, 0x97, 0xA6, "desay", "Desay", "nt51950-ab-merge-512k")]
    [InlineData("NT51950", 2, 2, 3, 0x97, 0xA6, "desay", "Desay", "nt51950-ab-merge-1024k")]
    [InlineData("NT51951", 0, null, null, 0x84, 0x85, "common", "Common", "nt51951-ab-merge-1024k")]
    [InlineData("NT51951", 0, null, null, 0x97, 0xA6, "desay", "Desay", "nt51951-ab-merge-1024k")]
    public void DeclaredMapsUseObservedCountsAndCurrentFormat(
        string member, int selectedCount, int? countA, int? countB, byte rawA, byte rawB,
        string format, string displayName, string map)
    {
        FirmwareFamilyResolutionDefinition family = Family();
        AbFormatMapResolutionResult result = AbFormatMapResolver.Resolve(family, member, Configuration(family),
            rawA, rawB, Topology(selectedCount), countA, countB);

        Assert.Empty(result.Issues);
        Assert.Equal(new AbFormatMapSelection(map, format, displayName), result.Selection);
    }

    /// <summary>Format disagreement wins over an unavailable baseline; neither failure produces a map.</summary>
    [Theory]
    [InlineData(0x97, 0x84, "AB_FORMAT_MISMATCH", "TPA and TPB resolve to different Event Buffer Formats.")]
    [InlineData(0x84, 0x85, "AB_FORMAT_MAP_UNAVAILABLE", "No unique AB baseline matches the selected format and topology.")]
    public void FailureOrderingAndMessagesRemainStable(byte rawA, byte rawB, string code, string message)
    {
        FirmwareFamilyResolutionDefinition family = Family();
        AbFormatMapResolutionResult result = AbFormatMapResolver.Resolve(family, "NT51950", Configuration(family),
            rawA, rawB, null, 2, 2);

        Assert.Null(result.Selection);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal((code, message, CompositionIssueSeverity.Error, null),
            (issue.Code, issue.Message, issue.Severity, issue.OperationId));
    }

    /// <summary>Current recognition and alias values do not mutate a previously returned choice.</summary>
    [Fact]
    public void IndependentConfigurationsDoNotShareSelectionState()
    {
        FirmwareFamilyResolutionDefinition family = Family();
        AbFormatMapResolutionResult original = AbFormatMapResolver.Resolve(family, "NT51951", Configuration(family),
            0x97, 0xA6, null, null, null);
        EventBufferFormatConfiguration changed = Configuration(family, [0x10, 0x11], "Panel format");
        AbFormatMapResolutionResult common = AbFormatMapResolver.Resolve(family, "NT51951", changed,
            0x97, 0xA6, null, null, null);
        AbFormatMapResolutionResult renamed = AbFormatMapResolver.Resolve(family, "NT51951", changed,
            0x10, 0x11, null, null, null);

        Assert.Equal(new AbFormatMapSelection("nt51951-ab-merge-1024k", "desay", "Desay"), original.Selection);
        Assert.Equal(new AbFormatMapSelection("nt51951-ab-merge-1024k", "common", "Common"), common.Selection);
        Assert.Equal(new AbFormatMapSelection("nt51951-ab-merge-1024k", "desay", "Panel format"), renamed.Selection);
        Assert.All<AbFormatMapResolutionResult>([original, common, renamed], result => Assert.Empty(result.Issues));
    }

    /// <summary>Invalid canonical declarations cannot be constructed to bypass the selection preconditions.</summary>
    [Theory]
    [InlineData("missing-map")]
    [InlineData("duplicate-baseline")]
    [InlineData("duplicate-variant")]
    public void CanonicalFamilyRejectsAmbiguousOrMissingMapsBeforeSelection(string mutation)
    {
        FirmwareFamilyResolutionDefinition family = Family();
        FirmwareAbFormatPolicy policy = family.AbFormatPolicy!;
        IEnumerable<FirmwareAbFormatVariant> variants = mutation switch
        {
            "missing-map" => policy.Variants.Append(new("NT51950", "common", "missing-map")),
            "duplicate-baseline" => policy.Variants.Append(new("NT51950", "common", "nt51950-ab-desay-cascade-1024k")),
            _ => policy.Variants.Append(policy.Variants[0]),
        };

        _ = Assert.Throws<ArgumentException>(() =>
        {
            var invalidPolicy = new FirmwareAbFormatPolicy(policy.ScopeId, policy.CommonFormatId, policy.CommonDisplayName,
                policy.Formats, policy.PrimaryBindings, variants);
            _ = new FirmwareFamilyResolutionDefinition(family.FamilyId, family.FamilyVersion, family.FamilyContentHash,
                family.ImageMaps, family.MetadataSets, family.CapabilityBindings, family.FamilyRelationships, invalidPolicy);
        });
    }

    private static FirmwareFamilyResolutionDefinition Family()
    {
        return BuiltInV2RegistrationRegistry.FindAbMergeRegistration("NT51950", "nt51950-ab-merge-maps")!.GetFirmwareFamily();
    }

    private static EventBufferFormatConfiguration Configuration(
        FirmwareFamilyResolutionDefinition family, IReadOnlyList<int>? values = null, string? alias = null)
    {
        FirmwareAbFormatPolicy policy = family.AbFormatPolicy!;
        return EventBufferFormatConfigurationAdmission.Admit(policy.ScopeId,
            [.. policy.Formats.Select(format => new EventBufferFormatIdentity(format.UniqueId, format.DisplayName))],
            [.. policy.Formats.Select(format => new EventBufferFormatDraftEntry(format.UniqueId, alias,
                values ?? [.. format.DefaultRecognitionValues.Select(value => (int)value)]))]).Configuration!;
    }

    private static TopologySelection? Topology(int count)
    {
        return count == 0 ? null : new(count, "test", TopologySelectionSource.Requested, "test");
    }
}

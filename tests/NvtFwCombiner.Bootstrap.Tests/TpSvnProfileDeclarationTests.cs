using System.Text.Json;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// TP-SVN-MODEL-1113-01: the TP SVN stamp and the SVN copy inside the Header copy are profile-declared facts.
/// Every expected position below comes from the owner tables (P0-3, decisions 44, 45 and 83), never from the
/// profiles or from the bytes; NT51926 follows its declared firmware-version map, never its version bytes.
/// </summary>
public sealed class TpSvnProfileDeclarationTests
{
    private const string DefinitionId = "tp-svn";
    private const string HeaderCopyStructureId = "tp-svn-header-copy";
    private const string GoldenRoot = "testdata/golden/canonical";

    /// <summary>Decision 45: Header copy start + 0x24 per IC (bank-local in AB images).</summary>
    private static readonly Dictionary<string, long> HeaderCopyStampByIc = new(StringComparer.Ordinal)
    {
        ["NT51923"] = 0x30334,
        ["NT51917"] = 0x32DE4,
        ["NT51927"] = 0x32DE4,
        ["NT51928"] = 0x32DE4,
        ["NT51919"] = 0x27F14,
        ["NT51929"] = 0x27F14,
        ["NT51932"] = 0x27F14,
        ["NT51950"] = 0x2D330,
        ["NT51951"] = 0x2D330,
    };

    /// <summary>Decision 45: NT51926 variants; NT51925 borrows these positions pending Proposed ADR 0083.</summary>
    private static readonly Dictionary<string, long> VersionedHeaderCopyStampByMap = new(StringComparer.Ordinal)
    {
        ["nt51925-ctrlram-fw141-tp-work-240k"] = 0x32F74,
        ["nt51925-ctrlram-fw141-full-flash-256k"] = 0x32F74,
        ["nt51925-ctrlram-fw200-tp-work-240k"] = 0x32A94,
        ["nt51925-ctrlram-fw200-full-flash-256k"] = 0x32A94,
        ["nt51926-ctrlram-fw141-tp-work-240k"] = 0x32F74,
        ["nt51926-ctrlram-fw141-full-flash-256k"] = 0x32F74,
        ["nt51926-ctrlram-fw200-tp-work-240k"] = 0x32A94,
        ["nt51926-ctrlram-fw200-full-flash-256k"] = 0x32A94,
    };

    /// <summary>P0-3: TP start + 0x24 per IC.</summary>
    private static readonly Dictionary<string, long> MainStampByIc = new(StringComparer.Ordinal)
    {
        ["NT51917"] = 0x24,
        ["NT51923"] = 0x24,
        ["NT51925"] = 0x24, // Prepared option A, pending official owner evidence.
        ["NT51926"] = 0x24,
        ["NT51927"] = 0x24,
        ["NT51928"] = 0x24,
        ["NT51919"] = 0x7024,
        ["NT51929"] = 0x7024,
        ["NT51932"] = 0x7024,
        ["NT51950"] = 0xA024,
        ["NT51951"] = 0xA024,
    };

    /// <summary>
    /// Every CtrlRAM Replace layout of every IC declares the Header-copy stamp at the owner position, through the
    /// one canonical definition; NT51926 fw1.4.1 and fw2.0.0 layouts each keep their own declared position.
    /// </summary>
    [Fact]
    public void EveryCtrlRamReplaceLayoutDeclaresTheHeaderCopyStampAtTheOwnerPosition()
    {
        var covered = new SortedSet<string>(StringComparer.Ordinal);
        var nt51926Maps = new SortedSet<string>(StringComparer.Ordinal);
        FirmwareMetadataStructureDefinition? canonical = null;
        foreach ((FirmwareFamilyResolutionDefinition family, FirmwareImageMap map) in CtrlRamReplaceMaps())
        {
            FirmwareResolvedMetadataStructure resolved = ResolveHeaderCopy(family, map, new byte[map.CapacityBytes]);
            Assert.Equal(DefinitionId, resolved.StructureDefinition.Definition.DefinitionId);
            Assert.Equal(CompositionAddressSpaceIds.ReferenceBase, resolved.StructureDefinition.ArtifactBindingId);
            canonical ??= resolved.StructureDefinition.Definition;
            Assert.Same(canonical, resolved.StructureDefinition.Definition);
            foreach (string member in map.Applicability.MemberIds)
            {
                long expected = member is "NT51925" or "NT51926"
                    ? VersionedHeaderCopyStampByMap[map.MapId]
                    : HeaderCopyStampByIc[member];
                Assert.Equal(new FirmwareAddressedRange("flash", new ByteRange(expected, 4)),
                    resolved.LocatorOutcome.ResolvedRange);
                _ = covered.Add(member);
            }

            if (map.Applicability.MemberIds.Any(static member => member is "NT51925" or "NT51926"))
            {
                _ = nt51926Maps.Add(map.MapId);
            }
        }

        Assert.Equal(MainStampByIc.Keys.Order(StringComparer.Ordinal), covered);
        Assert.Equal(VersionedHeaderCopyStampByMap.Keys.Order(StringComparer.Ordinal), nt51926Maps);
    }

    /// <summary>
    /// Golden CtrlRAM inputs carry their Header-copy stamp exactly at the position of their declared layout; the
    /// expected bytes are read directly at the owner offset. For NT51926 the other variant's position does not hold
    /// the stamp (fw1.4.1 files read FE at 0x32A94, fw2.0.0 files read 00 at 0x32F74), so only the declared variant
    /// can locate it.
    /// </summary>
    [Theory]
    [InlineData("NT51923/ctrlram-replace/fw1.4.1/single/nt51923-fw141-single-auto-prj-662-20260717/inputs/NT51923_TPFW_T81_20260430.bin", "nt51923-ctrlram-fw141-single-tp-work-240k", 0x30334, null)]
    [InlineData("NT51923/ctrlram-replace/fw1.4.1/single/nt51923-fw141-single-auto-prj-662-20260717/expected/NT51923TT_Flashcode_TM_TL129VVKS05-00_BYD_V06_D80T81_20260601.bin", "nt51923-ctrlram-fw141-single-full-flash", 0x30334, null)]
    [InlineData("NT51926/ctrlram-replace/fw1.4.1/single/nt51926-fw141-single-auto-prj-747-20260717/inputs/nt51926_fw_80.bin", "nt51926-ctrlram-fw141-tp-work-240k", 0x32F74, "FEFEFEFE")]
    [InlineData("NT51926/ctrlram-replace/fw1.4.1/single/nt51926-fw141-single-auto-prj-747-20260717/expected/NT51926_Flashcode_TM_TL080JFKS07-00_SKYWORTH_D80T80_20260522.bin", "nt51926-ctrlram-fw141-full-flash-256k", 0x32F74, "FEFEFEFE")]
    [InlineData("NT51926/ctrlram-replace/fw2.0.0/single/nt51926-fw200-single-auto-prj-597-20260718/inputs/NT51926_CommonFW_2.0.0_Single_TPFW.bin", "nt51926-ctrlram-fw200-tp-work-240k", 0x32A94, "00000000")]
    [InlineData("NT51926/ctrlram-replace/fw2.0.0/single/nt51926-fw200-single-auto-prj-597-20260718/expected/NT51926_FlashCode_D02TFF_20260718.bin", "nt51926-ctrlram-fw200-full-flash-256k", 0x32A94, "00000000")]
    [InlineData("NT51927/ctrlram-replace/fw1.4.1/single/nt51927-fw141-single-auto-prj-529-20260717/inputs/51927_TPFW_T01_20251205.bin", "nt51927-ctrlram-fw141-single-tp-work-212k", 0x32DE4, null)]
    [InlineData("NT51927/ctrlram-replace/fw1.4.0/cascade-3/nt51927-3chip-self-20260705/inputs/nt51927-3ic-tm-tl177xfks03-gm-d08t9b-20260703.bin", "nt51927-ctrlram-fw140-threechip-full-flash", 0x32DE4, null)]
    [InlineData("NT51928/standard-merge/gen-flash/topology-unscoped/nt51928-gen-flash/expected/nt51928-expected-output.bin", "nt51928-ctrlram-fw141-single-full-flash", 0x32DE4, null)]
    [InlineData("NT51929/ctrlram-replace/fw2.0.0/single/nt51929-fw200-single-auto-prj-594-20260717/inputs/nt51929_TPFW_T05_20260611.bin", "nt51929-ctrlram-fw200-single-full-flash", 0x27F14, null)]
    [InlineData("NT51932/ctrlram-replace/fw2.0.0/cascade-3/nt51932-fw200-cascade3-auto-prj-525-20260718/inputs/NT51932_TPFW_CASCADE_3.bin", "nt51932-ctrlram-fw200-cascade-full-flash", 0x27F14, null)]
    [InlineData("NT51950/ctrlram-replace/fw2.0.0/single/nt51950-fw200-single-auto-prj-676-20260717/inputs/nt51950_fw.bin", "nt51950-ctrlram-fw200-single-tp-work", 0x2D330, null)]
    [InlineData("NT51951/ctrlram-replace/fw2.0.0/single/nt51951-fw200-single-auto-prj-695-20260718/expected/nt51951-expected-output.bin", "nt51951-ctrlram-fw200-single-full-flash", 0x2D330, null)]
    public void GoldenInputsCarryTheHeaderCopyStampAtTheirDeclaredLayoutPosition(
        string goldenPath, string mapId, long ownerOffset, string? otherVariantBytes)
    {
        byte[] image = ReadGolden(goldenPath);
        (FirmwareFamilyResolutionDefinition family, FirmwareImageMap map) = CtrlRamReplaceMap(mapId);
        Assert.Equal(map.CapacityBytes, image.Length);

        FirmwareResolvedMetadataStructure resolved = ResolveHeaderCopy(family, map, image);

        Assert.Equal(new FirmwareAddressedRange("flash", new ByteRange(ownerOffset, 4)), resolved.LocatorOutcome.ResolvedRange);
        Assert.Equal(Convert.ToHexString(image, (int)ownerOffset, 4), StampHex(resolved));
        if (otherVariantBytes is not null)
        {
            long otherOffset = ownerOffset == 0x32F74 ? 0x32A94 : 0x32F74;
            Assert.Equal(otherVariantBytes, Convert.ToHexString(image, (int)otherOffset, 4));
            Assert.NotEqual(otherVariantBytes, StampHex(resolved));
        }
    }

    /// <summary>
    /// AB images keep one Header copy per bank at the bank-local position of the existing same-IC local CtrlRAM
    /// parent; the stamp is read from each bank slice without comparing A and B. The 1 MiB NT51951 Common and NT51950
    /// Common cascade images with 512 KiB banks are synthetic (no Golden), built from Golden banks with a different B
    /// stamp; the NT51950 cascade local parent covers the first 256 KiB of each 512 KiB bank.
    /// </summary>
    [Theory]
    [InlineData("NT51929/ab-merge/t05-d06/topology-unscoped/nt51929-ab-t05-d06/expected/NT51929ZT_Flashcode_TM_TL150UQAS01-00_Stellantis_V28_D06T05_20260611_AB.bin", "nt51929-ctrlram-fw200-single-full-flash", 0x40000, 0x27F14)]
    [InlineData("NT51950/ab-merge/boe-d82t80/topology-unscoped/nt51950-ab-boe-d82t80/expected/NT51950_FlashCode_BOE1720_BOEVX_2560x1660_A_D82T80_B_D82T80_20260616_BOE.bin", "nt51950-ctrlram-fw200-single-full-flash", 0x40000, 0x2D330)]
    [InlineData("NT51950/ab-merge/osd-d03t02/single/nt51950-ab-osd-d03t02-20260924/expected/NT51950TT_Flashcode_BOE_NIO_Hiway_A_D03T02_B_D03T02_NioHiway_20260924.bin", "nt51950-ctrlram-fw200-single-full-flash", 0x40000, 0x2D330)]
    [InlineData(null, "nt51951-ctrlram-fw200-single-full-flash", 0x80000, 0x2D330)]
    [InlineData(null, "nt51950-ctrlram-fw1x-cascade-full-flash", 0x80000, 0x2D330)]
    public void AbImagesCarryEachBankHeaderCopyStampBankLocally(
        string? goldenPath, string localMapId, int bankLength, long ownerOffset)
    {
        ArgumentNullException.ThrowIfNull(localMapId);
        byte[] image = goldenPath is not null ? ReadGolden(goldenPath)
            : localMapId.StartsWith("nt51951", StringComparison.Ordinal) ? CreateSyntheticNt51951CommonImage()
            : CreateSyntheticNt51950CommonCascadeImage();
        (FirmwareFamilyResolutionDefinition family, FirmwareImageMap map) = CtrlRamReplaceMap(localMapId);
        Assert.True(map.CapacityBytes <= bankLength);
        string[] stamps = new string[2];
        for (int bank = 0; bank < 2; bank++)
        {
            byte[] bankBytes = image.AsSpan(bank * bankLength, (int)map.CapacityBytes).ToArray();
            FirmwareResolvedMetadataStructure resolved = ResolveHeaderCopy(family, map, bankBytes);
            Assert.Equal(new ByteRange(ownerOffset, 4), resolved.LocatorOutcome.ResolvedRange.Range);
            stamps[bank] = StampHex(resolved);
            Assert.Equal(Convert.ToHexString(image, (int)((bank * bankLength) + ownerOffset), 4), stamps[bank]);
        }

        if (goldenPath is null)
        {
            Assert.NotEqual(stamps[0], stamps[1]);
        }
    }

    /// <summary>
    /// Decision 43: the Header-copy stamp is modeled only. No profile binds it for any purpose or requires it,
    /// only CtrlRAM Replace families declare it, and every TP SVN binding of the main stamp is display-only.
    /// </summary>
    [Fact]
    public void HeaderCopyStampIsNeverBoundRequiredOrDisplayed()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "profiles", "built-in");
        int displayBindings = 0;
        foreach (string path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
                     .Where(static path => path.Contains($"{Path.DirectorySeparatorChar}profiles{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                         !path.Contains($"{Path.DirectorySeparatorChar}schemas{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("metadataBindings", out JsonElement bindings))
            {
                continue;
            }

            foreach (JsonElement binding in bindings.EnumerateArray())
            {
                string structureId = binding.GetProperty("structureId").GetString()!;
                Assert.NotEqual(HeaderCopyStructureId, structureId);
                if (structureId is "tp-svn" or "tp-a-svn" or "tp-b-svn")
                {
                    Assert.Equal(["display"], binding.GetProperty("purposes").EnumerateArray().Select(static item => item.GetString()));
                    displayBindings++;
                }
            }

            if (document.RootElement.TryGetProperty("mapBinding", out JsonElement mapBinding) &&
                mapBinding.TryGetProperty("requiredMetadataStructureIds", out JsonElement required))
            {
                Assert.DoesNotContain(HeaderCopyStructureId, required.EnumerateArray().Select(static item => item.GetString()));
            }
        }

        Assert.Equal(27, displayBindings);
        foreach (string path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
                     .Where(static path => path.Contains($"{Path.DirectorySeparatorChar}families{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            if (File.ReadAllText(path).Contains($"\"{HeaderCopyStructureId}\"", StringComparison.Ordinal))
            {
                Assert.Contains("ctrlram-replace", Path.GetFileName(path), StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// Every Standard and AB TP input binds exactly one display-only entry of the one canonical definition at
    /// TP start + 0x24; AB TP A and TP B inputs are standalone TP files with the same position.
    /// </summary>
    [Fact]
    public void EveryStandardAndAbTpInputBindsOneDisplayOnlyStampAtTheOwnerPosition()
    {
        FirmwareMetadataStructureDefinition? canonical = null;
        foreach ((string icId, BuiltInV2Registration registration) in BuiltInV2RegistrationRegistry.StandardMergeByIc)
        {
            long capacity = icId == "NT51928" ? 0x80000 : 0x40000;
            MetadataPlanEntry entry = SingleStamp(Plan(registration, capacity), CompositionAddressSpaceIds.TpInput);
            canonical ??= entry.StructureDefinition.Definition;
            AssertStamp(entry, canonical, MainStampByIc[icId]);
        }

        foreach ((string icId, string mapVariant) in (ReadOnlySpan<(string, string)>)[
                     ("NT51919", "nt51919-ab-merge-512k"), ("NT51929", "nt51929-ab-merge-512k"), ("NT51932", "nt51932-ab-merge-512k"),
                     ("NT51950", "nt51950-ab-merge-maps"), ("NT51950", "nt51950-ab-cascade-maps"), ("NT51951", "nt51951-ab-merge-1024k")])
        {
            BuiltInV2Registration registration = Assert.IsType<BuiltInV2Registration>(
                BuiltInV2RegistrationRegistry.FindAbMergeRegistration(icId, mapVariant));
            MetadataPlanDefinition plan = Plan(registration, icId is "NT51951" || mapVariant.Contains("cascade", StringComparison.Ordinal)
                ? 0x100000 : 0x80000, icId == "NT51950" ? mapVariant.Contains("cascade", StringComparison.Ordinal) ? 2 : 1 : null);
            AssertStamp(SingleStamp(plan, CompositionAddressSpaceIds.TpAInput), canonical!, MainStampByIc[icId]);
            AssertStamp(SingleStamp(plan, CompositionAddressSpaceIds.TpBInput), canonical!, MainStampByIc[icId]);
        }
    }

    /// <summary>
    /// Decisions 44 and 83: the B-code stamp of an AB image is at the b-bank start of the map the published route
    /// compiles + TP start + 0x24. The published NT51950 cascade and NT51951 routes keep the Common map for the Desay
    /// format too, so a 1 MiB image has B at 0x8A024; the published NT51950 512 KiB route has B at 0x4A024. The map
    /// comes from the runtime registration and the production format selection; the positions are the owner values.
    /// </summary>
    [Theory]
    [InlineData("NT51950", 1, 0x84, 0x85, "common", "nt51950-ab-merge-maps", "nt51950-ab-merge-512k", 0x4A024)]
    [InlineData("NT51950", 1, 0x97, 0xA6, "desay", "nt51950-ab-merge-maps", "nt51950-ab-merge-512k", 0x4A024)]
    [InlineData("NT51950", 2, 0x84, 0x85, "common", "nt51950-ab-cascade-maps", "nt51950-ab-merge-1024k", 0x8A024)]
    [InlineData("NT51950", 2, 0x97, 0xA6, "desay", "nt51950-ab-cascade-maps", "nt51950-ab-merge-1024k", 0x8A024)]
    [InlineData("NT51950", 3, 0x97, 0xA6, "desay", "nt51950-ab-cascade-maps", "nt51950-ab-merge-1024k", 0x8A024)]
    [InlineData("NT51951", 0, 0x84, 0x85, "common", "nt51951-ab-merge-1024k", "nt51951-ab-merge-1024k", 0x8A024)]
    [InlineData("NT51951", 0, 0x97, 0xA6, "desay", "nt51951-ab-merge-1024k", "nt51951-ab-merge-1024k", 0x8A024)]
    public void PublishedAbFormatsPlaceTheBCodeStampOnTheCompiledMap(
        string icId, int chipCount, byte rawA, byte rawB, string format, string mapVariant, string mapId, long bStamp)
    {
        Assert.Contains(PublishedRegistrations(), registration =>
            registration.IcId == icId && registration.MapVariantSetId == mapVariant);
        BuiltInV2Registration registration = Assert.IsType<BuiltInV2Registration>(
            BuiltInV2RegistrationRegistry.FindAbMergeRegistration(icId, mapVariant));
        int? count = chipCount == 0 ? null : chipCount;
        CompiledComposition compiled = Compile(registration, 0x100000, count);
        FirmwareImageMap map = compiled.V2Details.Provenance.ResolvedMap.ImageMap;
        Assert.Equal(mapId, map.MapId);

        FirmwareFamilyResolutionDefinition family = registration.GetFirmwareFamily();
        FirmwareAbFormatPolicy policy = Assert.IsType<FirmwareAbFormatPolicy>(family.AbFormatPolicy);
        EventBufferFormatConfiguration configuration = EventBufferFormatConfigurationAdmission.Admit(policy.ScopeId,
            [.. policy.Formats.Select(static item => new EventBufferFormatIdentity(item.UniqueId, item.DisplayName))],
            [.. policy.Formats.Select(static item => new EventBufferFormatDraftEntry(item.UniqueId, null,
                [.. item.DefaultRecognitionValues.Select(static value => (int)value)]))]).Configuration!;
        AbFormatMapResolutionResult selection = AbFormatMapResolver.Resolve(family, icId, configuration, rawA, rawB,
            count is { } selected ? new TopologySelection(selected, "test", TopologySelectionSource.Requested, "test") : null,
            count, count);
        Assert.Empty(selection.Issues);
        Assert.Equal((mapId, format), (selection.Selection!.MapId, selection.Selection.FormatId));

        Assert.Equal(bStamp, BBank(map).Range.Start + MainStampByIc[icId]);
    }

    /// <summary>
    /// Decision 83: the special Desay layout of decision 44 (B at 0x4A024 in a 1 MiB image) belongs only to the
    /// closed Desay maps. They stay unpublished and the special case is retired for now, but their declarations,
    /// including the TP SVN structures, are kept: no runtime registration and no declared format variant reaches them.
    /// </summary>
    [Fact]
    public void ClosedDesayMapsKeepTheirRetiredStampDeclarationsUnpublished()
    {
        BuiltInV2Registration published = Assert.IsType<BuiltInV2Registration>(
            BuiltInV2RegistrationRegistry.FindAbMergeRegistration("NT51950", "nt51950-ab-cascade-maps"));
        FirmwareFamilyResolutionDefinition family = published.GetFirmwareFamily();
        foreach (string mapId in (ReadOnlySpan<string>)["nt51950-ab-desay-single-1024k", "nt51950-ab-desay-cascade-1024k", "nt51951-ab-desay-1024k"])
        {
            FirmwareImageMap map = Assert.Single(family.ImageMaps, candidate => candidate.MapId == mapId);
            Assert.Equal(0x100000, map.CapacityBytes);
            Assert.Equal(0x4A024, BBank(map).Range.Start + MainStampByIc["NT51950"]);
            IReadOnlyList<FirmwareMetadataStructure> structures = family.GetStructuresForMap(mapId);
            Assert.Contains(structures, static structure => structure.StructureId == "tp-a-svn");
            Assert.Contains(structures, static structure => structure.StructureId == "tp-b-svn");
            Assert.DoesNotContain(family.AbFormatPolicy!.Variants, variant => variant.MapId == mapId);
        }

        Assert.DoesNotContain(PublishedRegistrations(), static registration =>
            registration.ProfileId is "nt51950-ab-merge-desay" or "nt51951-ab-merge-desay");
    }

    private static FirmwareRegion BBank(FirmwareImageMap map)
    {
        return Assert.Single(map.Regions, static region => region.RegionId == "b-bank");
    }

    private static IEnumerable<ProfileBundleRuntimeRegistration> PublishedRegistrations()
    {
        return BuiltInV2BundleRegistry.TrustIndex.Bundles.SelectMany(static bundle => bundle.RuntimeRegistrations);
    }

    private static void AssertStamp(MetadataPlanEntry entry, FirmwareMetadataStructureDefinition canonical, long offset)
    {
        Assert.Same(canonical, entry.StructureDefinition.Definition);
        Assert.Equal([MetadataReferencePurpose.Display], entry.Purposes);
        byte[] image = new byte[offset + 0x40];
        FirmwareMetadataStructureResolution resolution = entry.FamilyDefinition.ResolveMetadataStructure(
            entry.ImageMap.MapId, entry.StructureDefinition.StructureId,
            new FirmwareMapResolutionInputs(entry.MemberId, entry.ResolvedMap.ModeId, entry.ResolvedMap.CapacityBytes,
                requestedTopology: null, [new FirmwareArtifactPayload(entry.SpaceId, image)]));
        Assert.Equal(new ByteRange(offset, 4),
            Assert.IsType<FirmwareResolvedMetadataStructure>(resolution.Resolved).LocatorOutcome.ResolvedRange.Range);
    }

    private static MetadataPlanEntry SingleStamp(MetadataPlanDefinition plan, string spaceId)
    {
        return Assert.Single(plan.Entries, entry =>
            entry.SpaceId == spaceId && entry.StructureDefinition.Definition.DefinitionId == DefinitionId);
    }

    private static MetadataPlanDefinition Plan(BuiltInV2Registration registration, long capacity, int? chipCount = null)
    {
        return registration.CreateMetadataPlan(Compile(registration, capacity, chipCount));
    }

    private static CompiledComposition Compile(BuiltInV2Registration registration, long capacity, int? chipCount)
    {
        if (chipCount is { } count)
        {
            registration.TryCompile(null, new TopologySelection(count, "test", TopologySelectionSource.Requested, "test"),
                out CompiledComposition? topologyCompiled, out IReadOnlyList<CompositionIssue> topologyIssues);
            Assert.Empty(topologyIssues);
            return Assert.IsType<CompiledComposition>(topologyCompiled);
        }

        registration.TryCompile(capacity, out CompiledComposition? compiled, out IReadOnlyList<CompositionIssue> issues);
        Assert.Empty(issues);
        return Assert.IsType<CompiledComposition>(compiled);
    }

    private static IEnumerable<(FirmwareFamilyResolutionDefinition Family, FirmwareImageMap Map)> CtrlRamReplaceMaps()
    {
        var seen = new HashSet<(string, string)>();
        foreach (ProfileBundlePackageTrustEntry bundle in BuiltInV2BundleRegistry.TrustIndex.Bundles)
        {
            foreach (ProfileBundleRuntimeRegistration registration in bundle.RuntimeRegistrations.Where(static registration =>
                         registration.WorkflowId == "ctrlram-replace"))
            {
                FirmwareFamilyResolutionDefinition family = BuiltInV2BundleRegistry.All[bundle.BundleDirectory]
                    .GetFirmwareFamily(registration.ProfileId, registration.ProfileVersion);
                foreach (FirmwareImageMap map in family.ImageMaps.Where(static map =>
                             map.Applicability.ModeIds.SequenceEqual(["ctrlram-replace"])))
                {
                    if (seen.Add((family.FamilyContentHash, map.MapId)))
                    {
                        yield return (family, map);
                    }
                }
            }
        }
    }

    private static (FirmwareFamilyResolutionDefinition Family, FirmwareImageMap Map) CtrlRamReplaceMap(string mapId)
    {
        return CtrlRamReplaceMaps().First(candidate => candidate.Map.MapId == mapId);
    }

    private static FirmwareResolvedMetadataStructure ResolveHeaderCopy(
        FirmwareFamilyResolutionDefinition family, FirmwareImageMap map, byte[] image)
    {
        FirmwareMetadataStructureResolution resolution = family.ResolveMetadataStructure(
            map.MapId, HeaderCopyStructureId,
            new FirmwareMapResolutionInputs(map.Applicability.MemberIds[0], "ctrlram-replace", map.CapacityBytes,
                requestedTopology: null, [new FirmwareArtifactPayload(CompositionAddressSpaceIds.ReferenceBase, image)]));
        return Assert.IsType<FirmwareResolvedMetadataStructure>(resolution.Resolved);
    }

    private static string StampHex(FirmwareResolvedMetadataStructure resolved)
    {
        string Field(string fieldId)
        {
            return Assert.Single(resolved.DecodedStructure.Facts, fact => fact.FieldId == fieldId).Value.BytesValue!.Hex;
        }

        return (Field("svn-flags") + Field("svn-revision")).ToUpperInvariant();
    }

    private static byte[] ReadGolden(string relativePath)
    {
        return File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot($"{GoldenRoot}/{relativePath}"));
    }

    private static byte[] CreateSyntheticNt51950CommonCascadeImage()
    {
        byte[] bank = ReadGolden("NT51950/ctrlram-replace/fw2.0.0/single/nt51950-fw200-single-auto-prj-676-20260717/expected/NT51950_Flashcode_BOE1540_Faurecia_Chery_D86T80_20260709.bin");
        byte[] image = new byte[0x100000];
        image.AsSpan().Fill(0xFF);
        bank.CopyTo(image, 0);
        bank.CopyTo(image, 0x80000);
        image.AsSpan(0x80000 + 0x2D330, 4).Clear();
        image[0x80000 + 0x2D330] = 0x80;
        image[0x80000 + 0x2D333] = 0x19;
        return image;
    }

    private static byte[] CreateSyntheticNt51951CommonImage()
    {
        byte[] bank = ReadGolden(
            "NT51951/ctrlram-replace/fw2.0.0/single/nt51951-fw200-single-auto-prj-695-20260718/expected/nt51951-expected-output.bin");
        byte[] image = new byte[0x100000];
        bank.CopyTo(image, 0);
        bank.CopyTo(image, 0x80000);
        image.AsSpan(0x80000 + 0x2D330, 4).Clear();
        image[0x80000 + 0x2D330] = 0x60;
        image[0x80000 + 0x2D331] = 0x21;
        return image;
    }
}

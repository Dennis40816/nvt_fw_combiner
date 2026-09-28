using System.Buffers.Binary;
using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// AB-TWO-NVT-1112-01 as refined by NVT-END-FLAG-1113-01: a captured CtrlRAM Base is AB only when a published AB
/// layout compiles at its length and either that layout's trusted AB structure holds or each canonical bank holds
/// the NVT marker at the bank-local end flag that AB layout declares (NT51950/NT51951), or, for a layout without a
/// declared end flag, exactly one marker in the bank. Markers away from a declared end flag are neither counted nor
/// rejected.
/// </summary>
public sealed class CtrlRamTwoNvtClassificationTests
{
    private const string Nt51950Osd = "nt51950-ab-osd-d03t02-20260924";
    private const string Nt51950Single = "nt51950-fw200-single-auto-prj-676-20260717";
    private const string Nt51951Single = "nt51951-fw200-single-auto-prj-695-20260718";
    private const string Nt51951Cascade = "nt51951-fw200-cascade2-auto-prj-599-20260731";
    private static readonly byte[] NvtMarker = [0x00, 0x4E, 0x56, 0x54];

    /// <summary>
    /// A Standard FlashCode Base followed by an equal-length non-NVT tail has one NVT marker, so a compiling AB
    /// layout at that length is not AB evidence and every Number keeps the Standard result.
    /// </summary>
    [Theory]
    [InlineData("NT51950", "standard-merge", 0xFF, IcNumberSelectionTokens.SingleChip)]
    [InlineData("NT51950", "standard-merge", 0xFF, IcNumberSelectionTokens.Cascade)]
    [InlineData("NT51950", "standard-merge", null, IcNumberSelectionTokens.SingleChip)]
    [InlineData("NT51950", Nt51950Single, 0xFF, IcNumberSelectionTokens.SingleChip)]
    [InlineData("NT51951", "standard-merge", 0xFF, IcNumberSelectionTokens.SingleChip)]
    [InlineData("NT51951", Nt51951Cascade, 0xFF, IcNumberSelectionTokens.SingleChip)]
    [InlineData("NT51951", Nt51951Cascade, null, IcNumberSelectionTokens.Cascade)]
    [InlineData("NT51951", Nt51951Single, 0xFF, IcNumberSelectionTokens.SingleChip)]
    public void StandardBaseWithOneNvtMarkerIsNotAb(string ic, string source, int? tailFill, string number)
    {
        byte[] standard = source == "standard-merge"
            ? File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath("standard-merge", ic, "expected-output"))
            : CtrlRamExpectedOutput(source);
        byte[] reference = [.. standard, .. NonNvtTail(standard.Length, tailFill)];
        Assert.Equal(1, TotalMarkers(reference));
        AssertStandardClassification(ic, reference, CompiledFirmwareArtifactKind.FlashCode);
        List<AbLayoutCandidate> layouts = CompileAbLayouts(ic, reference);
        Assert.NotEmpty(layouts);
        Assert.All(layouts, static layout =>
        {
            Assert.False(layout.HasTrustedStructure);
            Assert.Equal<int>([1, 0], layout.BankMarkerCounts);
        });

        CtrlRamBaseInspection inspected = Inspect(ic, reference, number);

        Assert.Equal(CtrlRamBaseKind.StandardFlash, inspected.Kind);
        Assert.Empty(inspected.Banks);
        Assert.IsNotType<AbCtrlRamDraftState>(inspected.EffectiveDraft);
        Assert.DoesNotContain(inspected.Issues, static issue =>
            issue.Code.StartsWith("input.bank-reference.", StringComparison.Ordinal));
    }

    /// <summary>Real two-bank AB Bases keep AB identity and the bank facts read from each bank's own marker.</summary>
    [Theory]
    [InlineData("NT51929", "nt51929-t05-d06", IcNumberSelectionTokens.SingleChip)]
    [InlineData("NT51919", "nt51929-t05-d06", IcNumberSelectionTokens.SingleChip)]
    [InlineData("NT51932", "nt51929-t05-d06", IcNumberSelectionTokens.SingleChip)]
    [InlineData("NT51932", "perfect-cascade-3", IcNumberSelectionTokens.CascadeTwoToEight)]
    [InlineData("NT51950", "nt51950-boe-d82t80", IcNumberSelectionTokens.SingleChip)]
    [InlineData("NT51950", "nt51950-hiway-d82t80", IcNumberSelectionTokens.SingleChip)]
    [InlineData("NT51950", Nt51950Osd, IcNumberSelectionTokens.SingleChip)]
    [InlineData("NT51951", Nt51951Single, IcNumberSelectionTokens.SingleChip)]
    [InlineData("NT51951", Nt51951Cascade, IcNumberSelectionTokens.Cascade)]
    public void RealTwoBankAbBaseKeepsAbFacts(string ic, string source, string number)
    {
        byte[] reference = RealAbReference(source);

        CtrlRamBaseInspection inspected = Inspect(ic, reference, number);

        Assert.Equal(CtrlRamBaseKind.AbFlash, inspected.Kind);
        Assert.Empty(inspected.Issues);
        Assert.Collection(inspected.Banks,
            static bank => Assert.Equal("a-bank", bank.BankId),
            static bank => Assert.Equal("b-bank", bank.BankId));
        Assert.Equal(0, inspected.Banks[0].Range.Start);
        Assert.Equal(inspected.Banks[0].Range.EndExclusive, inspected.Banks[1].Range.Start);
        Assert.Equal(inspected.Banks[0].Range.Length, inspected.Banks[1].Range.Length);
        foreach (CtrlRamBaseBankInspection bank in inspected.Banks)
        {
            ReadOnlySpan<byte> bytes = reference.AsSpan(checked((int)bank.Range.Start), checked((int)bank.Range.Length));
            int marker = Assert.Single(MarkerOffsets(bytes));
            Assert.Empty(bank.Issues);
            FirmwareConfigMetadataSnapshot config = Assert.IsType<FirmwareConfigMetadataSnapshot>(bank.FirmwareConfig);
            Assert.True(config.IsFirmwareVersionBarValid);
            Assert.True(config.ChipNumber > 0);
            Assert.Equal((long)marker + 3 - 0xFFF, config.FirmwareConfigBackupStart);
            Assert.Equal(bytes[checked((int)config.FirmwareConfigBackupStart)], config.FirmwareVersion);
            Assert.True(bank.TpVersion!.IsKnown);
        }
    }

    /// <summary>
    /// A damaged OSD Backup whose bank no longer has exactly one NVT marker stays AB only through the unique
    /// trusted AB structure retained from OSD-AB-REFERENCE-CLASSIFICATION-1111-01.
    /// </summary>
    [Theory]
    [InlineData(0, "moved-marker", "input.bank-reference.metadata-unreadable")]
    [InlineData(0, "missing-marker", "input.bank-reference.metadata-unreadable")]
    [InlineData(0x40000, "moved-marker", "input.bank-reference.metadata-unreadable")]
    [InlineData(0x40000, "missing-marker", "input.bank-reference.metadata-unreadable")]
    public void DamagedOsdMarkerRemainsAbThroughTrustedStructure(int bankStart, string damage, string issueCode)
    {
        byte[] reference = RealAbReference(Nt51950Osd);
        switch (damage)
        {
            case "moved-marker": reference[bankStart + 0x36FFC] ^= 1; NvtMarker.CopyTo(reference, bankStart + 0x1000); break;
            case "missing-marker": reference[bankStart + 0x36FFC] ^= 1; break;
            default: throw new ArgumentOutOfRangeException(nameof(damage));
        }
        List<AbLayoutCandidate> layouts = CompileAbLayouts("NT51950", reference);
        Assert.DoesNotContain(layouts, static layout => layout.HasOneMarkerPerBank);
        _ = Assert.Single(layouts, static layout => layout.HasTrustedStructure);

        CtrlRamBaseInspection inspected = Inspect("NT51950", reference, IcNumberSelectionTokens.SingleChip);

        Assert.Equal(CtrlRamBaseKind.AbFlash, inspected.Kind);
        Assert.Equal(2, inspected.Banks.Count);
        Assert.Contains(inspected.Issues, issue => issue.Code == issueCode);
    }

    /// <summary>
    /// NVT-END-FLAG-1113-01: an extra complete marker away from a bank's end flag is neither counted nor rejected;
    /// the real OSD AB Base keeps AB identity and both valid bank facts without an ambiguity issue.
    /// </summary>
    [Theory]
    [InlineData(0x1000)]
    [InlineData(0x40000 + 0x1000)]
    [InlineData(0x80000 + 0x1000)]
    public void MarkerAwayFromTheEndFlagIsIgnoredInARealOsdAbBase(int offset)
    {
        byte[] reference = WithMarker(RealAbReference(Nt51950Osd), offset);
        _ = Assert.Single(CompileAbLayouts("NT51950", reference), static layout => layout.HasOneMarkerPerBank);

        CtrlRamBaseInspection inspected = Inspect("NT51950", reference, IcNumberSelectionTokens.SingleChip);

        Assert.Equal(CtrlRamBaseKind.AbFlash, inspected.Kind);
        Assert.All(inspected.Banks, static bank => Assert.NotNull(bank.FirmwareConfig));
        Assert.DoesNotContain(inspected.Issues, static issue => issue.Code == "input.bank-reference.metadata-ambiguous");
    }

    /// <summary>
    /// NVT-END-FLAG-1113-01 boundary, accepted by the owner (position-only rule): a Standard Base whose Display OSD
    /// half has a marker away from the B end flag has no B-bank evidence and stays Standard; a marker exactly at the
    /// B end flag (0x76FFC) is B-bank evidence, so the Base is classified AB and its invalid B bank is reported.
    /// </summary>
    [Theory]
    [InlineData(0x50000, CtrlRamBaseKind.StandardFlash)]
    [InlineData(0x76000, CtrlRamBaseKind.StandardFlash)]
    [InlineData(0x76FFC, CtrlRamBaseKind.AbFlash)]
    public void OsdHalfMarkerCountsOnlyAtTheBEndFlag(int offset, CtrlRamBaseKind expected)
    {
        byte[] reference = WithMarker([.. StandardOutput("NT51950"), .. NonNvtTail(0x40000, 0xFF)], offset);

        CtrlRamBaseInspection inspected = Inspect("NT51950", reference, IcNumberSelectionTokens.SingleChip);

        Assert.Equal(expected, inspected.Kind);
        Assert.Equal(expected == CtrlRamBaseKind.AbFlash, inspected.Issues.Any(static issue =>
            issue.Code.StartsWith("input.bank-reference.", StringComparison.Ordinal)));
    }

    /// <summary>Markers away from a bank's declared scope are not AB evidence; the Standard/Unknown fallback decides.</summary>
    [Theory]
    [InlineData("NT51951", "osd-under-nt51951-geometry", CtrlRamBaseKind.StandardFlash)]
    [InlineData("NT51950", "nt51950-standard-extra-a-marker", CtrlRamBaseKind.StandardFlash)]
    [InlineData("NT51950", "nt51950-standard-in-b-extra-b-marker", CtrlRamBaseKind.Unknown)]
    [InlineData("NT51929", "nt51929-standard-extra-a-marker", CtrlRamBaseKind.Unknown)]
    public void MarkersOutsideTheDeclaredBankScopeAreNotAbEvidence(string ic, string shape, CtrlRamBaseKind fallback)
    {
        byte[] reference = shape switch
        {
            "osd-under-nt51951-geometry" => RealAbReference(Nt51950Osd),
            "nt51950-standard-extra-a-marker" => WithMarker([.. StandardOutput("NT51950"), .. NonNvtTail(0x40000, 0xFF)], 0x1000),
            "nt51950-standard-in-b-extra-b-marker" => WithMarker([.. NonNvtTail(0x40000, 0xFF), .. StandardOutput("NT51950")], 0x41000),
            "nt51929-standard-extra-a-marker" => WithMarker([.. StandardOutput("NT51929"), .. NonNvtTail(0x40000, 0xFF)], 0x1000),
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
        Assert.Equal(2, TotalMarkers(reference));
        List<AbLayoutCandidate> layouts = CompileAbLayouts(ic, reference);
        Assert.NotEmpty(layouts);
        Assert.All(layouts, static layout =>
        {
            Assert.False(layout.HasTrustedStructure);
            Assert.False(layout.HasOneMarkerPerBank);
            Assert.Contains(layout.BankMarkerCounts, static count => count == 0);
        });

        CtrlRamBaseInspection inspected = Inspect(ic, reference, IcNumberSelectionTokens.SingleChip);

        Assert.Equal(fallback, inspected.Kind);
        Assert.Empty(inspected.Banks);
    }

    /// <summary>
    /// With no trusted structure, one NVT marker in each canonical bank decides AB; version/bar and relocation
    /// damage remain reported issues instead of deciding the kind.
    /// </summary>
    [Theory]
    [InlineData("NT51950", 0x4A100, 0x76001)]
    [InlineData("NT51929", 0x47164, 0x6E001)]
    public void OneNvtMarkerPerBankIsAbDespiteBankIssues(string ic, int relocationByte, int bVersionBarByte)
    {
        byte[] reference = RealAbReference(ic == "NT51950" ? Nt51950Osd : "nt51929-t05-d06");
        reference[relocationByte] ^= 1;
        reference[bVersionBarByte] ^= 1;
        List<AbLayoutCandidate> layouts = CompileAbLayouts(ic, reference);
        Assert.DoesNotContain(layouts, static layout => layout.HasTrustedStructure);
        _ = Assert.Single(layouts, static layout => layout.HasOneMarkerPerBank);

        CtrlRamBaseInspection inspected = Inspect(ic, reference, IcNumberSelectionTokens.SingleChip);

        Assert.Equal(CtrlRamBaseKind.AbFlash, inspected.Kind);
        Assert.Equal(2, inspected.Banks.Count);
        Assert.NotNull(inspected.Banks[0].FirmwareConfig);
        Assert.Null(inspected.Banks[1].FirmwareConfig);
        Assert.Contains(inspected.Issues, static issue => issue.Code == "input.bank-reference.version-bar");
        Assert.Contains(inspected.Issues, static issue => issue.Code == "input.bank-reference.invalid");
    }

    private static CtrlRamBaseInspection Inspect(string ic, byte[] reference, string number)
    {
        FirmwareInspectionStatusBatch batch =
            ((CtrlRamAuthoringExperience)BootstrapTestHost.Canonical.CtrlRamAuthoring).InspectInputSlots(
                ic, [new FirmwareInspectionSnapshotInput("base", CompositionSlotIds.ReplaceBase + ".bin",
                    CtrlRamRequest: new(number, new AbCtrlRamDraftState()),
                    CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)],
                _ => reference);
        return Assert.IsType<CtrlRamBaseInspection>(batch.CtrlRamBaseInspection);
    }

    private static void AssertStandardClassification(string ic, byte[] reference, CompiledFirmwareArtifactKind expected)
    {
        var resolver = new FirmwareArtifactClassificationResolver(
            BootstrapTestHost.Canonical.Catalog, BootstrapTestHost.Services.Compiler);
        Assert.Equal(expected, resolver.Resolve(ic, null, reference)?.Kind);
    }

    /// <summary>Mirrors the published AB layout candidates that the Reference classifier compiles at this length.</summary>
    private static List<AbLayoutCandidate> CompileAbLayouts(string ic, byte[] reference)
    {
        var adapter = new BuiltInCtrlRamAuthoringAdapter(
            BootstrapTestHost.Canonical.Catalog, BootstrapTestHost.Canonical.Projection);
        CanonicalCapabilityCompilerAdapter compiler = BootstrapTestHost.Canonical.Compiler;
        TopologySelection?[] selections =
            [null, .. compiler.GetAbMergeTopologyChoices(ic).Select(static choice => choice.Selection)];
        var layouts = new List<AbLayoutCandidate>();
        foreach (TopologySelection? selection in selections)
        {
            if (!compiler.TryCompileAbMergeCapability(ic, selection, ["dp-ab-input"], out _,
                    out ResolvedCapability? published, out _) || published is null ||
                !compiler.TryCompilePublishedDynamicCapability(published.Identity, reference.Length,
                    [new FirmwareArtifactPayload("dp-ab-input", reference)], ["dp-ab-input"],
                    out CompiledComposition? layout, out _, out _, selection) ||
                layout is null || layout.Plan.OutputInitialization.Capacity != reference.Length)
            {
                continue;
            }
            int[] counts = [.. layout.V2Details.Provenance.ResolvedMap.ImageMap.Regions
                .Where(static region => region.RegionId is "a-bank" or "b-bank")
                .OrderBy(static region => region.Range.Start)
                .Select(region => BankMarkerCount(layout, reference, region.Range))];
            layouts.Add(new(counts, adapter.ValidateAbReference(layout, reference).HasTrustedAbStructure));
        }
        return layouts;
    }

    /// <summary>Counts bank evidence the way the classifier does: at the AB layout's declared end flag, if any.</summary>
    private static int BankMarkerCount(CompiledComposition layout, byte[] reference, ByteRange range)
    {
        _ = FirmwareConfigMetadataReader.TryReadBackup(
            reference.AsSpan(checked((int)range.Start), checked((int)range.Length)),
            layout.V2Details.Provenance.ResolvedMap.NvtEndFlagResolution, out _, out int markers);
        return markers;
    }

    private static int TotalMarkers(byte[] reference)
    {
        return MarkerOffsets(reference).Count;
    }

    private static List<int> MarkerOffsets(ReadOnlySpan<byte> bytes)
    {
        var offsets = new List<int>();
        int start = 0;
        int found;
        while ((found = bytes[start..].IndexOf(NvtMarker)) >= 0)
        {
            offsets.Add(start + found);
            start += found + 1;
        }
        return offsets;
    }

    private static byte[] RealAbReference(string source)
    {
        return source switch
        {
            "nt51929-t05-d06" => AbMergeOutput("NT51929", "t05-d06"),
            "nt51950-boe-d82t80" => AbMergeOutput("NT51950", "boe-d82t80"),
            "nt51950-hiway-d82t80" => AbMergeOutput("NT51950", "hiway-d82t80"),
            "perfect-cascade-3" => AbCtrlRamReferencePlanTests.CascadeReference(),
            Nt51950Osd => File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(CanonicalGoldenTestData.Artifact(
                CanonicalGoldenTestData.LoadDirectCase("ab-merge", Nt51950Osd), "expected-output"))),
            _ => PartialTwoBankReference(CtrlRamExpectedOutput(source)),
        };
    }

    private static byte[] AbMergeOutput(string ic, string variant)
    {
        return File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath("ab-merge", ic, "expected-output", variant));
    }

    /// <summary>The existing Partial-family AB evidence: two equal local outputs with B TP addresses relocated.</summary>
    private static byte[] PartialTwoBankReference(byte[] bank)
    {
        byte[] reference = [.. bank, .. bank];
        foreach (int field in new[] { 0xA100, 0xA110, 0xA120 })
        {
            Span<byte> address = reference.AsSpan(bank.Length + field, sizeof(uint));
            BinaryPrimitives.WriteUInt32LittleEndian(address,
                checked(BinaryPrimitives.ReadUInt32LittleEndian(address) + (uint)bank.Length));
        }
        return reference;
    }

    private static byte[] CtrlRamExpectedOutput(string caseId)
    {
        JsonElement golden = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", caseId);
        return File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(
            CanonicalGoldenTestData.Artifact(golden, "expected-output")));
    }

    private static byte[] StandardOutput(string ic)
    {
        return File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath("standard-merge", ic, "expected-output"));
    }

    private static byte[] NonNvtTail(int length, int? fill)
    {
        // A null fill uses a mod-251 counter: 0x00 is always followed by 0x01, so it never forms a marker.
        return [.. Enumerable.Range(0, length).Select(index => fill is { } value ? (byte)value : (byte)(index % 251))];
    }

    private static byte[] WithMarker(byte[] reference, int offset)
    {
        NvtMarker.CopyTo(reference, offset);
        return reference;
    }

    private sealed record AbLayoutCandidate(IReadOnlyList<int> BankMarkerCounts, bool HasTrustedStructure)
    {
        internal bool HasOneMarkerPerBank => BankMarkerCounts.Count == 2 && BankMarkerCounts.All(static count => count == 1);
    }
}

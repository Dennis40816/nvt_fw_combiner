using System.Buffers.Binary;
using System.Text.Json;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// Test-only executable witnesses for the reviewed formal-route denominator. This catalog keeps
/// policy evidence identity separate from fixture construction: derived and synthetic witnesses
/// prove host/runtime closure only and never become product Golden evidence.
/// </summary>
internal static class CanonicalFormalRouteRuntimeFixtureCatalog
{
    internal static IReadOnlyList<CanonicalFormalRouteRuntimeFixture> Create()
    {
        return
        [
            .. BuiltInCanonicalCapabilityPolicy.Load().Routes
                .Where(static route => route.Identity.WorkflowId is
                    ExperienceIds.StandardMerge or
                    ExperienceIds.AbMerge or
                    ExperienceIds.CtrlRamReplace)
                .OrderBy(static route => route.Identity.WorkflowId, StringComparer.Ordinal)
                .ThenBy(static route => route.Identity.IcId, StringComparer.Ordinal)
                .ThenBy(static route => route.Identity.IcCountVariant, StringComparer.Ordinal)
                .ThenBy(static route => route.Identity.MapVariant, StringComparer.Ordinal)
                .Select(static route => new CanonicalFormalRouteRuntimeFixture(
                    route,
                    Classify(route.Evidence.Value))),
        ];
    }

    internal static IReadOnlyList<CanonicalFormalRouteRuntimeCase> Materialize(
        CanonicalFormalRouteRuntimeFixture fixture,
        TempWorkspace workspace,
        CompositionHostServices host)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(host);
        return fixture.Policy.Identity.WorkflowId switch
        {
            ExperienceIds.StandardMerge => MaterializeStandard(fixture, workspace),
            ExperienceIds.AbMerge => MaterializeAb(fixture, workspace),
            ExperienceIds.CtrlRamReplace => MaterializeCtrlRam(fixture, workspace, host),
            _ => throw new InvalidOperationException(
                $"Unsupported formal workflow '{fixture.Policy.Identity.WorkflowId}'."),
        };
    }

    private static IReadOnlyList<CanonicalFormalRouteRuntimeCase> MaterializeStandard(
        CanonicalFormalRouteRuntimeFixture fixture,
        TempWorkspace workspace)
    {
        CapabilityRouteIdentity identity = fixture.Policy.Identity;
        if (identity.IcId == "NT51928")
        {
            return
            [
                CreateStandardCase(
                    fixture,
                    workspace,
                    sourceIc: "51927",
                    capacity: 0x40000,
                    expectedMapId: "nt51928-standard-merge-256k",
                    variant: null,
                    caseSuffix: "256k"),
                CreateStandardCase(
                    fixture,
                    workspace,
                    sourceIc: "51928",
                    capacity: 0x80000,
                    expectedMapId: "nt51928-standard-merge-512k",
                    variant: null,
                    caseSuffix: "512k"),
            ];
        }

        (string sourceIc, string? variant) = identity.IcId switch
        {
            "NT51917" => ("51927", null),
            "NT51919" => ("51929", null),
            "NT51923" => ("51923", null),
            "NT51926" => ("51926", null),
            "NT51927" => ("51927", null),
            "NT51929" => ("51929", null),
            "NT51932" => ("51932", null),
            "NT51950" => ("51950", "dp-256k"),
            "NT51951" => ("51951", "dp-512k"),
            _ => throw UnknownRoute(identity),
        };
        int capacity = ParseCapacity(identity.MapVariant);
        return
        [
            CreateStandardCase(
                fixture,
                workspace,
                sourceIc,
                capacity,
                identity.MapVariant,
                variant,
                FormattableString.Invariant($"{capacity / 1024}k")),
        ];
    }

    private static CanonicalFormalRouteRuntimeCase CreateStandardCase(
        CanonicalFormalRouteRuntimeFixture fixture,
        TempWorkspace workspace,
        string sourceIc,
        int capacity,
        string expectedMapId,
        string? variant,
        string caseSuffix)
    {
        string dpSource = CanonicalGoldenTestData.ArtifactPath(
            ExperienceIds.StandardMerge,
            sourceIc,
            CompositionAddressSpaceIds.DpInput,
            variant);
        string tpSource = CanonicalGoldenTestData.ArtifactPath(
            ExperienceIds.StandardMerge,
            sourceIc,
            CompositionAddressSpaceIds.TpInput,
            variant);
        byte[] dp = ResizeCanonicalInput(File.ReadAllBytes(dpSource), capacity, 0x31);
        string dpPath = dp.Length == new FileInfo(dpSource).Length
            ? dpSource
            : workspace.Write($"inputs/{fixture.Policy.Identity.IcId}-{caseSuffix}-dp.bin", dp);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpInput] = dpPath,
            [CompositionAddressSpaceIds.TpInput] = tpSource,
        };
        if (expectedMapId == "nt51928-standard-merge-512k")
        {
            paths.Add(
                CompositionAddressSpaceIds.LdcInput,
                CanonicalGoldenTestData.ArtifactPath(
                    ExperienceIds.StandardMerge,
                    sourceIc,
                    CompositionAddressSpaceIds.LdcInput,
                variant));
        }
        string sourceIcId = $"NT{sourceIc}";
        string sourceCaseId = StandardSourceCaseId(sourceIc);
        CanonicalFormalRuntimeWitnessKind canonicalKind =
            StringComparer.Ordinal.Equals(sourceIcId, fixture.Policy.Identity.IcId)
                ? CanonicalFormalRuntimeWitnessKind.DirectCanonicalInput
                : fixture.Policy.Evidence.Value == CapabilityEvidenceStatus.ApprovedAlias
                    ? CanonicalFormalRuntimeWitnessKind.ApprovedAlias
                    : CanonicalFormalRuntimeWitnessKind.CanonicalDerived;
        CanonicalFormalRuntimeWitnessProvenance[] witnesses =
        [
            .. paths.Keys.Select(slotId => new CanonicalFormalRuntimeWitnessProvenance(
                slotId,
                slotId == CompositionAddressSpaceIds.DpInput && dpPath != dpSource
                    ? CanonicalFormalRuntimeWitnessKind.CanonicalDerived
                    : canonicalKind,
                ExperienceIds.StandardMerge,
                sourceIcId,
                sourceCaseId,
                CanonicalFormalRuntimeParityClaim.RuntimeContractOnly)),
        ];
        return new CanonicalFormalRouteRuntimeCase(
            $"{fixture.RouteId}:{caseSuffix}",
            fixture,
            expectedMapId,
            SelectionToken: null,
            paths,
            witnesses);
    }

    private static IReadOnlyList<CanonicalFormalRouteRuntimeCase> MaterializeAb(
        CanonicalFormalRouteRuntimeFixture fixture,
        TempWorkspace workspace)
    {
        CapabilityRouteIdentity identity = fixture.Policy.Identity;
        return identity.IcId == "NT51950" && identity.IcCountVariant == "2-plus-ic"
            ?
            [
                MaterializeAbCase(fixture, workspace, requestedCount: 2),
                MaterializeAbCase(fixture, workspace, requestedCount: 9),
            ]
            : [MaterializeAbCase(fixture, workspace, requestedCount: null)];
    }

    private static CanonicalFormalRouteRuntimeCase MaterializeAbCase(
        CanonicalFormalRouteRuntimeFixture fixture,
        TempWorkspace workspace,
        int? requestedCount)
    {
        CapabilityRouteIdentity identity = fixture.Policy.Identity;
        Dictionary<string, byte[]> bytes;
        int? selectedCount = null;
        string? sourceCaseId;
        string? sourceIcId;
        CanonicalFormalRuntimeWitnessKind witnessKind;
        if (identity.IcId is "NT51919" or "NT51929" or "NT51932")
        {
            sourceCaseId = "nt51929-ab-t05-d06";
            sourceIcId = "NT51929";
            bytes = ReadAbGoldenInputs(sourceCaseId);
            witnessKind = identity.IcId == sourceIcId
                ? CanonicalFormalRuntimeWitnessKind.DirectCanonicalInput
                : fixture.Policy.Evidence.Value == CapabilityEvidenceStatus.ApprovedAlias
                    ? CanonicalFormalRuntimeWitnessKind.ApprovedAlias
                    : CanonicalFormalRuntimeWitnessKind.CanonicalDerived;
        }
        else if (identity.IcId == "NT51950" && identity.IcCountVariant == "1-ic")
        {
            sourceCaseId = "nt51950-ab-boe-d82t80";
            sourceIcId = "NT51950";
            bytes = ReadAbGoldenInputs(sourceCaseId);
            witnessKind = CanonicalFormalRuntimeWitnessKind.DirectCanonicalInput;
            selectedCount = 1;
        }
        else if (identity.IcId == "NT51950")
        {
            sourceCaseId = "nt51950-ab-boe-d82t80";
            sourceIcId = "NT51950";
            bytes = ReadAbGoldenInputs(sourceCaseId);
            witnessKind = CanonicalFormalRuntimeWitnessKind.CanonicalDerived;
            bytes[CompositionAddressSpaceIds.DpAbInput] = ResizeCanonicalInput(
                bytes[CompositionAddressSpaceIds.DpAbInput],
                0x100000,
                0x5D);
            selectedCount = requestedCount ?? 2;
            PatchFirmwareConfigChipCount(
                bytes[CompositionAddressSpaceIds.TpAInput],
                checked((byte)selectedCount.Value));
            PatchFirmwareConfigChipCount(
                bytes[CompositionAddressSpaceIds.TpBInput],
                checked((byte)selectedCount.Value));
        }
        else
        {
            sourceCaseId = null;
            sourceIcId = null;
            witnessKind = CanonicalFormalRuntimeWitnessKind.Synthetic;
            bytes = identity.IcId == "NT51951"
                ? new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    [CompositionAddressSpaceIds.DpAbInput] = CreatePattern(0x100000, 0x31),
                    [CompositionAddressSpaceIds.TpAInput] = CreateAbTpImage(0x81, 0x00, 1),
                    [CompositionAddressSpaceIds.TpBInput] = CreateAbTpImage(0x82, 0x01, 1),
                }
                : throw UnknownRoute(identity);
        }

        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string slotId, byte[] input) in bytes)
        {
            string countToken = selectedCount?.ToString(
                System.Globalization.CultureInfo.InvariantCulture) ?? "unscoped";
            paths.Add(
                slotId,
                workspace.Write(
                    $"inputs/{identity.IcId}-{identity.IcCountVariant}-{countToken}-{slotId}.bin",
                    input));
        }
        CanonicalFormalRuntimeWitnessProvenance[] witnesses =
        [
            .. paths.Keys.Select(slotId => new CanonicalFormalRuntimeWitnessProvenance(
                slotId,
                witnessKind,
                sourceCaseId is null ? null : ExperienceIds.AbMerge,
                sourceIcId,
                sourceCaseId,
                CanonicalFormalRuntimeParityClaim.RuntimeContractOnly)),
        ];
        return new CanonicalFormalRouteRuntimeCase(
            selectedCount is null ? fixture.RouteId : $"{fixture.RouteId}:count-{selectedCount}",
            fixture,
            identity.MapVariant,
            selectedCount?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            paths,
            witnesses,
            selectedCount,
            ExpectedResolvedIcCount: null);
    }

    private static IReadOnlyList<CanonicalFormalRouteRuntimeCase> MaterializeCtrlRam(
        CanonicalFormalRouteRuntimeFixture fixture,
        TempWorkspace workspace,
        CompositionHostServices host)
    {
        CapabilityRouteIdentity identity = fixture.Policy.Identity;
        int[] chipCounts = identity.IcCountVariant switch
        {
            "1-ic" => [1],
            "2-ic" => [2],
            "3-ic" => [3],
            "2-8-ic" => [2, 8],
            "2-plus-ic" => [2, 8, 9],
            _ => throw UnknownRoute(identity),
        };
        return
        [
            .. chipCounts.Select(chipCount => CreateCtrlRamCase(
                fixture,
                workspace,
                host,
                chipCount)),
        ];
    }

    private static CanonicalFormalRouteRuntimeCase CreateCtrlRamCase(
        CanonicalFormalRouteRuntimeFixture fixture,
        TempWorkspace workspace,
        CompositionHostServices host,
        int chipCount)
    {
        CapabilityRouteIdentity identity = fixture.Policy.Identity;
        CanonicalFormalRuntimeSource source = ReadCtrlRamCanonicalBase(identity);
        byte[] baseBytes = source.Bytes;
        int capacity = CtrlRamCapacity(identity);
        baseBytes = ResizeCanonicalInput(baseBytes, capacity, 0x6B);
        PlaceFirmwareConfigBackupForTopology(
            baseBytes,
            identity,
            checked((byte)chipCount));
        string number = identity.IcCountVariant switch
        {
            "1-ic" => IcNumberSelectionTokens.SingleChip,
            "2-ic" when identity.IcId is "NT51950" or "NT51951" =>
                IcNumberSelectionTokens.Cascade,
            "2-ic" => "2",
            "3-ic" => "3",
            "2-8-ic" => IcNumberSelectionTokens.CascadeTwoToEight,
            "2-plus-ic" => IcNumberSelectionTokens.Cascade,
            _ => throw UnknownRoute(identity),
        };
        string suffix = $"{identity.IcId}-{identity.MapVariant}-{chipCount}";
        string basePath = workspace.Write($"inputs/{suffix}-base.bin", baseBytes);
        IReadOnlyList<ReplaceInputSlot> slots = host.CtrlRamAuthoring
            .GetDiscoveryDisplayFromAcceptedBase(identity.IcId, number, baseBytes)
            .InputSlots;
        ReplaceInputSlot replacement = slots.FirstOrDefault(static slot => !slot.IsOptional) ??
            (slots.Count == 0 ? null : slots[0]) ??
            throw new InvalidOperationException($"CtrlRAM route '{fixture.RouteId}' exposes no input slot.");
        string replacementPath = workspace.Write(
            $"inputs/{suffix}-{replacement.SlotId}.bin",
            CreatePattern(0x100000, unchecked((byte)(0x41 + chipCount))));
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = basePath,
            [replacement.SlotId] = replacementPath,
        };
        CanonicalFormalRuntimeWitnessProvenance[] witnesses =
        [
            new(
                CompositionSlotIds.ReplaceBase,
                CanonicalFormalRuntimeWitnessKind.CanonicalDerived,
                source.SourceWorkflowId,
                source.SourceIcId,
                source.SourceCaseId,
                CanonicalFormalRuntimeParityClaim.RuntimeContractOnly),
            new(
                replacement.SlotId,
                CanonicalFormalRuntimeWitnessKind.Synthetic,
                SourceWorkflowId: null,
                SourceIcId: null,
                SourceCaseId: null,
                ParityClaim: CanonicalFormalRuntimeParityClaim.RuntimeContractOnly),
        ];
        return new CanonicalFormalRouteRuntimeCase(
            $"{fixture.RouteId}:count-{chipCount}",
            fixture,
            identity.MapVariant,
            number,
            paths,
            witnesses,
            chipCount,
            chipCount);
    }

    private static CanonicalFormalRuntimeSource ReadCtrlRamCanonicalBase(
        CapabilityRouteIdentity identity)
    {
        string map = identity.MapVariant;
        string ic = identity.IcId;
        if (ic is "NT51917" or "NT51927")
        {
            bool twoChip = map.Contains("fw132-twochip", StringComparison.Ordinal);
            bool threeChip = map.Contains("fw140-threechip", StringComparison.Ordinal);
            string caseId = twoChip
                ? "nt51927-2chip-self-20260705"
                : threeChip
                    ? "nt51927-3chip-self-20260705"
                    : "nt51927-fw141-single-auto-prj-529-20260717";
            string artifactId = twoChip || threeChip
                ? "reference-base"
                : map.Contains("tp-work", StringComparison.Ordinal)
                    ? "tp-input"
                    : "expected-output";
            return new CanonicalFormalRuntimeSource(
                ReadCtrlRamArtifact(caseId, artifactId, directEvidence: twoChip || threeChip),
                ExperienceIds.CtrlRamReplace,
                "NT51927",
                caseId);
        }
        if (ic == "NT51928")
        {
            return new CanonicalFormalRuntimeSource(
                File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(
                    ExperienceIds.StandardMerge,
                    "51928",
                    "expected-output")),
                ExperienceIds.StandardMerge,
                "NT51928",
                "nt51928-gen-flash");
        }
        if (ic is "NT51919" or "NT51929")
        {
            // NT51919 is an approved NT51929-family alias; never borrow NT51932 bytes here.
            return new CanonicalFormalRuntimeSource(
                File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(
                    ExperienceIds.StandardMerge,
                    "51929",
                    "expected-output")),
                ExperienceIds.StandardMerge,
                "NT51929",
                "nt51929-gen-flash");
        }
        if (ic == "NT51923")
        {
            string caseId = identity.IcCountVariant == "1-ic"
                ? "nt51923-fw141-single-auto-prj-662-20260717"
                : "nt51923-fw141-cascade3-auto-prj-734-20260717";
            return new CanonicalFormalRuntimeSource(
                ReadCtrlRamArtifact(
                    caseId,
                    map.Contains("tp-work", StringComparison.Ordinal)
                        ? "tp-input"
                        : "expected-output"),
                ExperienceIds.CtrlRamReplace,
                "NT51923",
                caseId);
        }
        if (ic == "NT51926")
        {
            string version = map.Contains("fw141", StringComparison.Ordinal) ? "fw141" : "fw200";
            string topology = identity.IcCountVariant == "1-ic" ? "single" : "cascade";
            string caseId = (version, topology) switch
            {
                ("fw141", "single") => "nt51926-fw141-single-auto-prj-747-20260717",
                ("fw141", _) => "nt51926-fw141-cascade2-auto-prj-597-20260717",
                ("fw200", "single") => "nt51926-fw200-single-auto-prj-597-20260718",
                _ => "nt51926-fw200-cascade3-auto-prj-597-20260718",
            };
            return new CanonicalFormalRuntimeSource(
                ReadCtrlRamArtifact(
                    caseId,
                    map.Contains("tp-work", StringComparison.Ordinal)
                        ? "tp-input"
                        : "expected-output"),
                ExperienceIds.CtrlRamReplace,
                "NT51926",
                caseId);
        }
        if (ic == "NT51932")
        {
            // Keep this contract witness on the declared IC even when another family happens to admit it.
            return new CanonicalFormalRuntimeSource(
                File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(
                    ExperienceIds.StandardMerge,
                    "51932",
                    "expected-output")),
                ExperienceIds.StandardMerge,
                "NT51932",
                "nt51932-gen-flash");
        }
        if (ic is "NT51950" or "NT51951")
        {
            string caseId = identity.IcCountVariant == "1-ic"
                ? ic == "NT51950"
                    ? "nt51950-fw200-single-auto-prj-676-20260717"
                    : "nt51951-fw200-single-auto-prj-695-20260718"
                : "nt51951-fw200-cascade2-auto-prj-599-20260731";
            string sourceIcId = identity.IcCountVariant == "1-ic" ? ic : "NT51951";
            return new CanonicalFormalRuntimeSource(
                ReadCtrlRamArtifact(
                    caseId,
                    map.Contains("tp-work", StringComparison.Ordinal)
                        ? identity.IcCountVariant == "1-ic" ? "tp-input" : "tp-firmware-input"
                        : "expected-output"),
                ExperienceIds.CtrlRamReplace,
                sourceIcId,
                caseId);
        }
        throw UnknownRoute(identity);
    }

    private static byte[] ReadCtrlRamArtifact(
        string caseId,
        string artifactId,
        bool directEvidence = false)
    {
        JsonElement goldenCase = directEvidence
            ? CanonicalGoldenTestData.LoadDirectEvidenceCase(ExperienceIds.CtrlRamReplace, caseId)
            : CanonicalGoldenTestData.LoadDirectCase(ExperienceIds.CtrlRamReplace, caseId);
        return File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(
            CanonicalGoldenTestData.Artifact(goldenCase, artifactId)));
    }

    private static Dictionary<string, byte[]> ReadAbGoldenInputs(string caseId)
    {
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase(
            ExperienceIds.AbMerge,
            caseId);
        return goldenCase.GetProperty("artifacts")
            .EnumerateArray()
            .Where(static artifact => artifact.GetProperty("role").GetString() == "input")
            .ToDictionary(
                static artifact => artifact.GetProperty("artifactId").GetString()!,
                static artifact => File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(artifact)),
                StringComparer.Ordinal);
    }

    private static int CtrlRamCapacity(CapabilityRouteIdentity identity)
    {
        string map = identity.MapVariant;
        return map.Contains("tp-work-212k", StringComparison.Ordinal)
            ? 0x35000
            : map.Contains("tp-work-240k", StringComparison.Ordinal)
                ? 0x3C000
                : map.EndsWith("-tp-work", StringComparison.Ordinal)
                    ? 0x37000
                    : identity.IcId is "NT51928" or "NT51951"
                        ? 0x80000
                        : 0x40000;
    }

    private static int ParseCapacity(string mapId)
    {
        return mapId.EndsWith("-256k", StringComparison.Ordinal)
            ? 0x40000
            : mapId.EndsWith("-512k", StringComparison.Ordinal)
                ? 0x80000
                : mapId.EndsWith("-1024k", StringComparison.Ordinal)
                    ? 0x100000
                    : throw new InvalidOperationException(
                        $"Standard runtime fixture cannot parse map capacity '{mapId}'.");
    }

    private static string StandardSourceCaseId(string sourceIc)
    {
        return sourceIc switch
        {
            "51950" => "51950-dp-256k",
            "51951" => "51951-dp-512k",
            _ => $"nt{sourceIc}-gen-flash",
        };
    }

    private static byte[] ResizeCanonicalInput(byte[] source, int length, byte salt)
    {
        if (source.Length == length)
        {
            return source;
        }
        byte[] resized = CreatePattern(length, salt);
        source.AsSpan(0, Math.Min(source.Length, resized.Length)).CopyTo(resized);
        return resized;
    }

    private static byte[] CreatePattern(int length, byte salt)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = unchecked((byte)(salt + (index * 37)));
        }
        return bytes;
    }

    private static byte[] CreateAbTpImage(byte version, byte subVersion, byte chipCount)
    {
        const int backupStart = 0x1000;
        byte[] bytes = CreatePattern(0x37000, version);
        bytes[backupStart + FirmwareConfigLayout.FirmwareVersionOffset] = version;
        bytes[backupStart + FirmwareConfigLayout.FirmwareVersionBarOffset] = unchecked((byte)~version);
        bytes[backupStart + FirmwareConfigLayout.FirmwareSubVersionOffset] = subVersion;
        bytes[backupStart + FirmwareConfigLayout.ChipNumberOffset] = chipCount;
        bytes[backupStart + 0xFFC] = 0;
        bytes[backupStart + 0xFFD] = (byte)'N';
        bytes[backupStart + 0xFFE] = (byte)'V';
        bytes[backupStart + 0xFFF] = (byte)'T';
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x7164, sizeof(uint)), 0x00123456);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x7168, sizeof(uint)), 0x00ABCDEF);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x716C, sizeof(uint)), 0x0000C0DE);
        return bytes;
    }

    private static void PatchFirmwareConfigChipCount(byte[] bytes, byte chipCount)
    {
        if (!FirmwareConfigMetadataReader.TryReadBackup(bytes, out FirmwareConfigMetadata metadata))
        {
            throw new InvalidDataException("Canonical-derived runtime base has no readable FWConfig Backup.");
        }
        bytes[checked((int)metadata.StructureStart + FirmwareConfigLayout.ChipNumberOffset)] = chipCount;
    }

    private static void PlaceFirmwareConfigBackupForTopology(
        byte[] bytes,
        CapabilityRouteIdentity identity,
        byte chipCount)
    {
        if (!FirmwareConfigMetadataReader.TryReadBackup(bytes, out FirmwareConfigMetadata metadata))
        {
            throw new InvalidDataException("Canonical-derived runtime base has no readable FWConfig Backup.");
        }

        if (identity.IcId is not ("NT51919" or "NT51929" or "NT51932") || chipCount < 2)
        {
            PatchFirmwareConfigChipCount(bytes, chipCount);
            return;
        }

        if (!LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
                identity.IcId,
                metadata.CommonFwVersion,
                out LegacyCombinerPostbuildProfile? profile,
                out string? issue) ||
            profile is null)
        {
            throw new InvalidDataException(
                $"Canonical-derived runtime base cannot select the declared Postbuild profile for " +
                $"'{identity.RouteId}' and Common FW {metadata.CommonFwVersion}: {issue}");
        }
        LegacyCombinerDiffDlmPolicy? policy = profile.DiffDlmPolicy;
        if (policy is null || !policy.AppliesTo(chipCount))
        {
            PatchFirmwareConfigChipCount(bytes, chipCount);
            return;
        }

        int sourceStart = checked((int)metadata.StructureStart);
        int targetStart = checked((int)policy.GetExpectedFirmwareConfigBackupStart(chipCount));
        int envelopeLength = checked((int)policy.FirmwareConfigBackupLength);
        if (sourceStart != targetStart)
        {
            if (sourceStart < 0 ||
                targetStart < 0 ||
                sourceStart + envelopeLength > bytes.Length ||
                targetStart + envelopeLength > bytes.Length)
            {
                throw new InvalidDataException(
                    $"Topology-derived FWConfig Backup placement for '{identity.RouteId}' is outside the base image.");
            }

            bytes.AsSpan(sourceStart, envelopeLength).CopyTo(bytes.AsSpan(targetStart, envelopeLength));
            bytes.AsSpan(sourceStart + envelopeLength - 4, 4).Clear();
        }
        bytes[targetStart + FirmwareConfigLayout.ChipNumberOffset] = chipCount;
    }

    private static CanonicalFormalRuntimePolicyEvidenceClass Classify(
        CapabilityEvidenceStatus evidence)
    {
        return evidence switch
        {
            CapabilityEvidenceStatus.DirectGolden => CanonicalFormalRuntimePolicyEvidenceClass.DirectGolden,
            CapabilityEvidenceStatus.ApprovedAlias => CanonicalFormalRuntimePolicyEvidenceClass.ApprovedAlias,
            CapabilityEvidenceStatus.SyntheticOracle => CanonicalFormalRuntimePolicyEvidenceClass.SyntheticOracle,
            CapabilityEvidenceStatus.ContractOnly => CanonicalFormalRuntimePolicyEvidenceClass.ContractOnly,
            CapabilityEvidenceStatus.Missing => throw new InvalidDataException(
                $"Formal runtime fixture has unsupported evidence status '{evidence}'."),
            _ => throw new InvalidDataException(
                $"Formal runtime fixture has unknown evidence status '{evidence}'."),
        };
    }

    private static InvalidOperationException UnknownRoute(CapabilityRouteIdentity identity)
    {
        return new InvalidOperationException($"No runtime fixture recipe exists for '{identity.RouteId}'.");
    }

    private sealed record CanonicalFormalRuntimeSource(
        byte[] Bytes,
        string SourceWorkflowId,
        string SourceIcId,
        string SourceCaseId);
}

using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Tests CtrlRAM report metadata authority with materialized built-in profiles.</summary>
public sealed class CtrlRamReportMetadataPlanTests
{
    /// <summary>Every route remains empty when its exact Standard profile declares no report classification.</summary>
    [Theory]
    [InlineData("NT51919", 2)]
    [InlineData("NT51950", 2)]
    [InlineData("NT51951", 2)]
    public void UndeclaredReportClassificationUsesEmptyPlan(string icId, int routeCount)
    {
        CtrlRamV2Route[] routes =
        [
            .. CtrlRamV2RouteRegistry.All.Where(route =>
                StringComparer.Ordinal.Equals(route.Key.IcId, icId)),
        ];

        Assert.Equal(routeCount, routes.Length);
        Assert.All(routes, route =>
        {
            MetadataPlanDefinition plan =
                BuiltInCtrlRamAuthoringAdapter.CreateCtrlRamReportMetadataPlan(route);
            Assert.Null(route.ReportMetadataMapId);
            Assert.Same(MetadataPlanDefinition.Empty, plan);
            Assert.Empty(plan.Entries);
            Assert.Empty(plan.ReportProjections);
            Assert.Null(plan.SourceIdentity);
        });
    }

    /// <summary>All reportful routes retain the one trust-index-declared exact Standard map.</summary>
    [Theory]
    [InlineData("NT51917", 3, "nt51927-standard-merge-256k")]
    [InlineData("NT51923", 2, "nt51923-standard-merge-256k")]
    [InlineData("NT51926", 4, "nt51926-standard-merge-256k")]
    [InlineData("NT51927", 3, "nt51927-standard-merge-256k")]
    [InlineData("NT51928", 3, "nt51928-standard-merge-512k")]
    [InlineData("NT51929", 2, "nt51929-standard-merge-256k")]
    [InlineData("NT51932", 2, "nt51932-standard-merge-256k")]
    public void ReportClassificationUsesDeclaredExactStandardMap(
        string icId,
        int routeCount,
        string expectedMapId)
    {
        CtrlRamV2Route[] routes =
        [
            .. CtrlRamV2RouteRegistry.All.Where(route =>
                StringComparer.Ordinal.Equals(route.Key.IcId, icId)),
        ];

        Assert.Equal(routeCount, routes.Length);
        Assert.All(routes, route =>
        {
            MetadataPlanDefinition plan =
                BuiltInCtrlRamAuthoringAdapter.CreateCtrlRamReportMetadataPlan(route);
            Assert.Equal(expectedMapId, route.ReportMetadataMapId);
            Assert.NotEmpty(plan.Entries);
            Assert.NotNull(plan.SourceIdentity);
            Assert.All(plan.Entries, entry =>
            {
                Assert.Contains(
                    MetadataReferencePurpose.ReportClassification,
                    entry.Purposes);
                Assert.Equal(expectedMapId, entry.ResolvedMap.ImageMap.MapId);
                Assert.Equal(CompositionAddressSpaceIds.ReferenceBase, entry.SlotId);
            });
        });
    }

    /// <summary>Admission rejects missing, extraneous, cross-IC, and unknown counterpart declarations.</summary>
    [Fact]
    public void CounterpartAdmissionFailsClosedBeforeRoutePublication()
    {
        ProfileBundleRuntimeRegistration reportful = BuiltInV2BundleRegistry.TrustIndex.Bundles
            .SelectMany(static bundle => bundle.RuntimeRegistrations)
            .Single(static registration =>
                registration.WorkflowId == ExperienceIds.CtrlRamReplace &&
                registration.IcId == "NT51926" &&
                registration.PostbuildBranch == "single-chip" &&
                registration.PostbuildProcessorId == "nfc.nt51926.ctrlram-postbuild-v1");
        BuiltInV2Registration reportfulStandard =
            BuiltInV2RegistrationRegistry.StandardMergeByIc[reportful.IcId];
        MetadataPlanDefinition accepted =
            CtrlRamV2RouteRegistry.ValidateReportMetadataCounterpart(
                reportful,
                reportfulStandard);
        Assert.NotEmpty(accepted.Entries);

        _ = Assert.Throws<InvalidDataException>(() =>
            CtrlRamV2RouteRegistry.ValidateReportMetadataCounterpart(
                reportful with { ReportMetadataMapId = null },
                reportfulStandard));
        _ = Assert.Throws<InvalidDataException>(() =>
            CtrlRamV2RouteRegistry.ValidateReportMetadataCounterpart(
                reportful with { ReportMetadataMapId = "unknown-standard-map" },
                reportfulStandard));
        _ = Assert.Throws<InvalidDataException>(() =>
            CtrlRamV2RouteRegistry.ValidateReportMetadataCounterpart(
                reportful with
                {
                    ReportMetadataMapId = "nt51923-standard-merge-256k",
                },
                reportfulStandard));
        _ = Assert.Throws<InvalidDataException>(() =>
            CtrlRamV2RouteRegistry.ValidateReportMetadataCounterpart(
                reportful,
                BuiltInV2RegistrationRegistry.StandardMergeByIc["NT51923"]));
        _ = Assert.Throws<InvalidDataException>(() =>
            CtrlRamV2RouteRegistry.ValidateReportMetadataCounterpart(
                reportful,
                standardRegistration: null));

        ProfileBundleRuntimeRegistration multiMapReportful =
            BuiltInV2BundleRegistry.TrustIndex.Bundles
                .SelectMany(static bundle => bundle.RuntimeRegistrations)
                .First(static registration =>
                    registration.WorkflowId == ExperienceIds.CtrlRamReplace &&
                    registration.IcId == "NT51928");
        MetadataPlanDefinition compact =
            CtrlRamV2RouteRegistry.ValidateReportMetadataCounterpart(
                multiMapReportful with
                {
                    ReportMetadataMapId = "nt51928-standard-merge-256k",
                },
                BuiltInV2RegistrationRegistry.StandardMergeByIc["NT51928"]);
        Assert.NotEmpty(compact.Entries);
        Assert.All(compact.Entries, entry => Assert.Equal(
            "nt51928-standard-merge-256k",
            entry.ResolvedMap.ImageMap.MapId));

        ProfileBundleRuntimeRegistration reportless = BuiltInV2BundleRegistry.TrustIndex.Bundles
            .SelectMany(static bundle => bundle.RuntimeRegistrations)
            .First(static registration =>
                registration.WorkflowId == ExperienceIds.CtrlRamReplace &&
                registration.IcId == "NT51950");
        Assert.Same(
            MetadataPlanDefinition.Empty,
            CtrlRamV2RouteRegistry.ValidateReportMetadataCounterpart(
                reportless,
                BuiltInV2RegistrationRegistry.StandardMergeByIc[reportless.IcId]));
        _ = Assert.Throws<InvalidDataException>(() =>
            CtrlRamV2RouteRegistry.ValidateReportMetadataCounterpart(
                reportless with
                {
                    ReportMetadataMapId = "nt51950-standard-merge-256k",
                },
                BuiltInV2RegistrationRegistry.StandardMergeByIc[reportless.IcId]));
    }

    /// <summary>A compiled CtrlRAM route rejects report metadata materialized from another declared map.</summary>
    [Fact]
    public void RuntimeCompilationRejectsDifferentReportMetadataMap()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51926-fw200-single-auto-prj-597-20260718");
        JsonElement[] artifacts = [.. fixtureCase.GetProperty("artifacts").EnumerateArray()];
        string ArtifactPath(string artifactId)
        {
            return CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact =>
                artifact.GetProperty("artifactId").GetString() == artifactId));
        }

        var slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = ArtifactPath("expected-output"),
            ["replace-ctrlram-normal"] = ArtifactPath("normal-ctrlram-input"),
        };
        var inputBytes = slotPaths.ToDictionary(
            static pair => pair.Key,
            static pair => File.ReadAllBytes(pair.Value),
            StringComparer.Ordinal);
        CtrlRamAuthoringSessionPreparation preparation =
            BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
                new AuthoringSessionState(ExperienceIds.CtrlRamReplace),
                "NT51926",
                "single",
                slotPaths,
                inputBytes);
        ActiveSessionSnapshot session = Assert.IsType<ActiveSessionSnapshot>(
            preparation.AcceptedSession);
        ResolvedCapability capability = Assert.IsType<ResolvedCapability>(
            session.GetAcceptedCapability(AuthoringDerivedResultKind.Inspection));
        CapabilityRouteResolutionResult resolution =
            BootstrapTestHost.Canonical.Catalog.ResolveDynamicRoute(
                capability.Identity.RouteId);
        CtrlRamV2Route otherMapRoute = CtrlRamV2RouteRegistry.All.First(static route =>
            route.Key.IcId == "NT51923");

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            resolution.Route!.BindCompilation(
                capability.CompiledComposition,
                otherMapRoute.ReportMetadataPlan,
                capability.RuntimeReferenceProof));

        Assert.True(resolution.Succeeded);
        Assert.Contains(
            "report-metadata-map:nt51926-standard-merge-256k",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "report-metadata-map:nt51923-standard-merge-256k",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>A reviewed NT51928 512-KiB route rejects the same profile's 256-KiB report plan.</summary>
    [Fact]
    public void RuntimeCompilationRejectsSameProfileDifferentReportMetadataMap()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace",
            "nt51927-2chip-self-20260705");
        var slotPaths = fixtureCase.GetProperty("artifacts").EnumerateArray()
            .Where(static artifact =>
                artifact.GetProperty("slotId").GetString() != CompositionSlotIds.ReplaceBase)
            .ToDictionary(
                static artifact => artifact.GetProperty("slotId").GetString()!,
                CanonicalGoldenTestData.ArtifactPath,
                StringComparer.Ordinal);
        slotPaths[CompositionSlotIds.ReplaceBase] = CanonicalGoldenTestData.ArtifactPath(
            "standard-merge",
            "51928",
            "expected-output");
        var inputBytes = slotPaths.ToDictionary(
            static pair => pair.Key,
            static pair => File.ReadAllBytes(pair.Value),
            StringComparer.Ordinal);
        CtrlRamAuthoringSessionPreparation preparation =
            BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
                new AuthoringSessionState(ExperienceIds.CtrlRamReplace),
                "NT51928",
                "2",
                slotPaths,
                inputBytes);
        ActiveSessionSnapshot session = Assert.IsType<ActiveSessionSnapshot>(
            preparation.AcceptedSession);
        ResolvedCapability capability = Assert.IsType<ResolvedCapability>(
            session.GetAcceptedCapability(AuthoringDerivedResultKind.Inspection));
        CapabilityRouteResolutionResult resolution =
            BootstrapTestHost.Canonical.Catalog.ResolveDynamicRoute(
                capability.Identity.RouteId);
        ProfileBundleRuntimeRegistration registration =
            BuiltInV2BundleRegistry.TrustIndex.Bundles
                .SelectMany(static bundle => bundle.RuntimeRegistrations)
                .First(static candidate =>
                    candidate.WorkflowId == ExperienceIds.CtrlRamReplace &&
                    candidate.IcId == "NT51928");
        MetadataPlanDefinition compactPlan =
            CtrlRamV2RouteRegistry.ValidateReportMetadataCounterpart(
                registration with
                {
                    ReportMetadataMapId = "nt51928-standard-merge-256k",
                },
                BuiltInV2RegistrationRegistry.StandardMergeByIc["NT51928"]);

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            resolution.Route!.BindCompilation(
                capability.CompiledComposition,
                compactPlan,
                capability.RuntimeReferenceProof));

        Assert.True(resolution.Succeeded);
        Assert.Contains(
            "report-metadata-map:nt51928-standard-merge-512k",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "report-metadata-map:nt51928-standard-merge-256k",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>A non-map Base fails closed at classification or at exact-route length admission.</summary>
    [Theory]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717", "NT51950", "tp-input", -1, "input.reference.unrecognized")]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717", "NT51950", "tp-input", 1, CompositionIssueCodes.InputAddressSpaceLengthMismatch)]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717", "NT51950", "expected-output", -1, CompositionIssueCodes.InputAddressSpaceLengthMismatch)]
    [InlineData("nt51951-fw200-single-auto-prj-695-20260718", "NT51951", "tp-input", -1, "input.reference.unrecognized")]
    [InlineData("nt51951-fw200-single-auto-prj-695-20260718", "NT51951", "tp-input", 1, CompositionIssueCodes.InputAddressSpaceLengthMismatch)]
    [InlineData("nt51951-fw200-single-auto-prj-695-20260718", "NT51951", "expected-output", -1, CompositionIssueCodes.InputAddressSpaceLengthMismatch)]
    public void NonMapReferenceCapacityFailsClosedAtItsAdmissionStage(
        string caseId,
        string icId,
        string baseArtifactId,
        int lengthDelta,
        string expectedIssueCode)
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            caseId);
        JsonElement baseArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("artifactId").GetString() == baseArtifactId);
        JsonElement replacementArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("originalFileName").GetString() == "NF_Ctrlram.bin");
        byte[] source = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(baseArtifact));
        byte[] invalid = lengthDelta > 0
            ? [.. source, 0x00]
            : source[..^1];
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ctrlram-invalid-capacity");
        string basePath = workspace.Write("reference.bin", invalid);
        var slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = basePath,
            ["replace-ctrlram-nf"] = CanonicalGoldenTestData.ArtifactPath(replacementArtifact),
        };
        Dictionary<string, byte[]> inputBytes = slotPaths.ToDictionary(
            static pair => pair.Key,
            static pair => File.ReadAllBytes(pair.Value),
            StringComparer.Ordinal);
        CtrlRamAuthoringSessionPreparation? preparation = null;

        Exception? exception = Record.Exception(() =>
            preparation = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
                new AuthoringSessionState(ExperienceIds.CtrlRamReplace),
                icId,
                "single",
                slotPaths,
                inputBytes));

        Assert.Null(exception);
        Assert.NotNull(preparation);
        Assert.Null(preparation.AcceptedSession);
        CompositionIssue issue = Assert.Single(
            preparation.Issues,
            issue => issue.Code == expectedIssueCode &&
                issue.OperationId == CompositionSlotIds.ReplaceBase);
        Assert.Contains(
            expectedIssueCode == CompositionIssueCodes.InputAddressSpaceLengthMismatch
                ? "length"
                : "unambiguous",
            issue.Message,
            StringComparison.OrdinalIgnoreCase);

        var adapter = new BuiltInCtrlRamAuthoringAdapter(
            BootstrapTestHost.Canonical.Catalog,
            BootstrapTestHost.Canonical.Projection);
        CtrlRamAuthoringCompilation routeAdmission = adapter.Resolve(
            icId,
            "single",
            slotPaths,
            firmwareVersionEdit: null,
            selectedInputBytes: inputBytes);
        Assert.Null(routeAdmission.Capability);
        CompositionIssue lengthIssue = Assert.Single(routeAdmission.Issues);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, lengthIssue.Code);
        Assert.Equal(CompositionSlotIds.ReplaceBase, lengthIssue.OperationId);
        Assert.Contains("accepted exact reference lengths", lengthIssue.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// CTRLRAM-OSD-ENVELOPE-1112-01: a Base one byte longer than the full-flash map is recognized by its Standard
    /// prefix and accepted as a Display-OSD envelope with the nonstandard-length warning.
    /// </summary>
    [Theory]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717", "NT51950")]
    [InlineData("nt51951-fw200-single-auto-prj-695-20260718", "NT51951")]
    public void LongerThanFullFlashBaseIsAcceptedAsAnEnvelope(string caseId, string icId)
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", caseId);
        JsonElement baseArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            static artifact => artifact.GetProperty("artifactId").GetString() == "expected-output");
        JsonElement replacementArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            static artifact => artifact.GetProperty("originalFileName").GetString() == "NF_Ctrlram.bin");
        byte[] source = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(baseArtifact));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ctrlram-envelope-capacity");
        var slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = workspace.Write("reference.bin", [.. source, 0x00]),
            ["replace-ctrlram-nf"] = CanonicalGoldenTestData.ArtifactPath(replacementArtifact),
        };
        Dictionary<string, byte[]> inputBytes = slotPaths.ToDictionary(
            static pair => pair.Key,
            static pair => File.ReadAllBytes(pair.Value),
            StringComparer.Ordinal);

        CtrlRamAuthoringSessionPreparation preparation = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.CtrlRamReplace), icId, "single", slotPaths, inputBytes);

        ResolvedCapability capability = Assert.IsType<ResolvedCapability>(preparation.AcceptedSession?.ExactCapability);
        RuntimeReferenceReplaceV2CompilationContext context = Assert.IsType<RuntimeReferenceReplaceV2CompilationContext>(
            capability.CompiledComposition.V2Details.Provenance.Context);
        Assert.Equal(source.LongLength, context.SourceEnvelope!.LayoutTemplateCapacity);
        Assert.Equal(source.LongLength + 1, context.SourceEnvelope.ActualOutputLength);
        Assert.Contains(
            capability.CtrlRamExecutionPlan!.AdvisoryIssues,
            static advisory => advisory.Code == "DP_NONSTANDARD_SIZE_WARNING" &&
                advisory.Severity == CompositionIssueSeverity.Warning);
    }

    /// <summary>
    /// CTRLRAM-OSD-ENVELOPE-CLASSIFY-1112-01: shared firmware inspection shows a nonstandard-length flash as Flash
    /// Code by its prefix at the largest shorter published Standard length, and reports the complete candidate as
    /// not matching that capacity.
    /// </summary>
    [Theory]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717", "NT51950", 0x40000, 0x60000, 0x40000)]
    [InlineData("nt51951-fw200-cascade2-auto-prj-599-20260731", "NT51950", 0x80000, 0xC0000, 0x80000)]
    [InlineData("nt51951-fw200-single-auto-prj-695-20260718", "NT51951", 0x80000, 0xC0000, 0x80000)]
    public void NonstandardEnvelopeFlashIsFlashCodeByItsLargestShorterPrefix(
        string caseId, string icId, int flashLength, int candidateLength, int expectedPrefixLength)
    {
        byte[] candidate = CreateEnvelopeCandidate(caseId, flashLength, candidateLength);
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-envelope-inspection");

        FirmwareInspectionSnapshot inspection = FirmwareInspectionTestSupport.InspectFirmware(
            icId, workspace.Write("flash.bin", candidate), tpPath: null, ctrlRamRequest: null);

        CompiledFirmwareArtifactClassification classification = Assert.IsType<
            CompiledFirmwareArtifactClassification>(inspection.ArtifactClassification);
        Assert.Equal(BaseFirmwareArtifactKind.FlashCode, inspection.BaseFirmwareArtifactKind);
        Assert.Equal(CompiledFirmwareArtifactKind.FlashCode, classification.Kind);
        CompiledFirmwareArtifactSignal capacity = classification.Signals.Single(
            static signal => signal.Kind == CompiledFirmwareArtifactSignalKind.DeclaredContainerCapacity);
        Assert.Equal(CompiledFirmwareArtifactSignalStatus.NotSatisfied, capacity.Status);
        Assert.Equal(expectedPrefixLength, capacity.RequiredEndExclusive);
        Assert.Equal(ByteRange.FromStartEndExclusive(expectedPrefixLength, candidateLength), capacity.FailedRange);
    }

    /// <summary>
    /// Only the largest shorter published length is tried: when both the 256 KiB and the 512 KiB prefixes of a
    /// 768 KiB NT51950 candidate would classify, the 512 KiB composition decides and no shorter prefix is consulted.
    /// </summary>
    [Fact]
    public void NonstandardEnvelopeFlashUsesOnlyTheLargestShorterPrefix()
    {
        byte[] candidate = CreateEnvelopeCandidate("nt51950-fw200-single-auto-prj-676-20260717", 0x40000, 0xC0000);
        candidate.AsSpan(0x40000, 0x40000).Fill(0xFF);
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-envelope-largest-prefix");

        FirmwareInspectionSnapshot inspection = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51950", workspace.Write("flash.bin", candidate), tpPath: null, ctrlRamRequest: null);

        CompiledFirmwareArtifactClassification classification = Assert.IsType<
            CompiledFirmwareArtifactClassification>(inspection.ArtifactClassification);
        Assert.Equal(CompiledFirmwareArtifactKind.FlashCode, classification.Kind);
        Assert.Equal(0x80000, classification.Signals.Single(
            static signal => signal.Kind == CompiledFirmwareArtifactSignalKind.DeclaredContainerCapacity)
            .RequiredEndExclusive);
    }

    private static byte[] CreateEnvelopeCandidate(string caseId, int flashLength, int candidateLength)
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", caseId);
        JsonElement flashArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            static artifact => artifact.GetProperty("artifactId").GetString() == "expected-output");
        byte[] flash = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(flashArtifact));
        Assert.Equal(flashLength, flash.Length);
        byte[] candidate = new byte[candidateLength];
        flash.CopyTo(candidate, 0);
        for (int offset = flashLength; offset < candidateLength; offset++)
        {
            candidate[offset] = unchecked((byte)offset);
        }

        return candidate;
    }

    /// <summary>
    /// CLASSIFY-EXACT-FALLTHROUGH-1112-01: with the active Standard Merge envelope capability, a nonstandard-length
    /// flash still classifies as Flash Code by its largest shorter published Standard prefix.
    /// </summary>
    [Fact]
    public void NonstandardEnvelopeFlashIsFlashCodeWithTheExactStandardCapability()
    {
        byte[] candidate = CreateEnvelopeCandidate("nt51950-fw200-single-auto-prj-676-20260717", 0x40000, 0x60000);
        Assert.True(
            BootstrapTestHost.Canonical.Compiler.TryCompileStandardMerge(
                "NT51950", candidate, ["dp-input", "tp-input"],
                out _, out ResolvedCapability? capability, out IReadOnlyList<CompositionIssue> issues),
            string.Join(" | ", issues.Select(static issue => issue.Message)));
        var resolver = new FirmwareArtifactClassificationResolver(
            BootstrapTestHost.Canonical.Catalog, BootstrapTestHost.Services.Compiler);

        CompiledFirmwareArtifactClassification classification = Assert.IsType<CompiledFirmwareArtifactClassification>(
            resolver.Resolve("NT51950", Assert.IsType<ResolvedCapability>(capability), candidate));

        Assert.Equal(CompiledFirmwareArtifactKind.FlashCode, classification.Kind);
        Assert.Equal(0x40000, classification.Signals.Single(
            static signal => signal.Kind == CompiledFirmwareArtifactSignalKind.DeclaredContainerCapacity)
            .RequiredEndExclusive);
    }

    /// <summary>
    /// Known 1.1.12 limitation, fail-closed: a 512 KiB NT51950 Standard Base whose Display OSD half contains one
    /// complete NVT marker has one marker in each AB bank and is classified as AB; its B bank is not a valid bank,
    /// so the session is rejected and no Replace can run.
    /// </summary>
    [Fact]
    public void Nt51950StandardOsdBaseWithTailNvtMarkerFailsClosed()
    {
        const string caseId = "nt51950-fw200-single-auto-prj-676-20260717";
        byte[] candidate = CreateEnvelopeCandidate(caseId, 0x40000, 0x80000);
        byte[] marker = [0x00, 0x4E, 0x56, 0x54];
        marker.CopyTo(candidate.AsSpan(0x50000));
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", caseId);
        JsonElement replacementArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            static artifact => artifact.GetProperty("originalFileName").GetString() == "NF_Ctrlram.bin");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-osd-tail-marker");
        var slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = workspace.Write("reference.bin", candidate),
            ["replace-ctrlram-nf"] = CanonicalGoldenTestData.ArtifactPath(replacementArtifact),
        };
        Dictionary<string, byte[]> inputBytes = slotPaths.ToDictionary(
            static pair => pair.Key,
            static pair => File.ReadAllBytes(pair.Value),
            StringComparer.Ordinal);

        CtrlRamAuthoringSessionPreparation preparation = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.CtrlRamReplace), "NT51950", "single", slotPaths, inputBytes);

        Assert.Null(preparation.AcceptedSession);
        Assert.Contains(preparation.Issues, static issue => issue.Code.StartsWith("input.bank-reference.", StringComparison.Ordinal));
    }

    /// <summary>The shared firmware-inspection result preserves an exact CtrlRAM compilation failure.</summary>
    [Theory]
    [InlineData(
        "nt51950-fw200-single-auto-prj-676-20260717",
        "NT51950",
        "Base firmware BIN length 0x37001 is unsupported for NT51950 / single CtrlRAM Replace; accepted exact reference lengths are 0x37000 / 0x40000.")]
    [InlineData(
        "nt51951-fw200-single-auto-prj-695-20260718",
        "NT51951",
        "Base firmware BIN length 0x37001 is unsupported for NT51951 / single CtrlRAM Replace; accepted exact reference lengths are 0x37000 / 0x80000.")]
    public void FirmwareInspectionPreservesNonMapReferenceIssue(
        string caseId,
        string icId,
        string expectedMessage)
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            caseId);
        JsonElement baseArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("artifactId").GetString() == "tp-input");
        JsonElement replacementArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("originalFileName").GetString() == "NF_Ctrlram.bin");
        byte[] validBase = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(baseArtifact));
        byte[] invalidBase = [.. validBase, 0x00];
        byte[] replacement = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(replacementArtifact));
        const string basePath = "invalid-reference.bin";
        const string replacementPath = "nf-ctrlram.bin";
        ReplaceInputSlot nfSlot = BootstrapTestHost.Services.CtrlRamAuthoring
            .GetDiscoveryDisplay(icId, IcNumberSelectionTokens.SingleChip)
            .InputSlots.Single(static slot => slot.SlotId == "replace-ctrlram-nf");

        IReadOnlyList<FirmwareInspectionSnapshotResult> results =
            BuiltInFirmwareInspection.InspectFirmwareBatch(
                BootstrapTestHost.Canonical,
                icId,
                [
                    new FirmwareInspectionSnapshotInput(
                        CompositionSlotIds.ReplaceBase,
                        basePath,
                        CtrlRamRequest: new CtrlRamInspectionRequest(IcNumberSelectionTokens.SingleChip),
                        CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase),
                    new FirmwareInspectionSnapshotInput(
                        nfSlot.SlotId,
                        replacementPath,
                        CtrlRamReplaceAddressSpaceId: nfSlot.AddressSpaceId),
                ],
                path => path == basePath ? invalidBase : replacement);

        FirmwareInspectionSnapshot baseInspection = results.Single(result =>
            result.InspectionId == CompositionSlotIds.ReplaceBase).Inspection;
        CompositionIssue issue = Assert.Single(baseInspection.AuthoringCompilationIssues);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, issue.Code);
        Assert.Equal(CompositionSlotIds.ReplaceBase, issue.OperationId);
        Assert.Equal(expectedMessage, issue.Message);
        Assert.Empty(results.Single(result => result.InspectionId == nfSlot.SlotId)
            .Inspection.AuthoringCompilationIssues);
    }

    /// <summary>A valid target-family base alone remains a facts-only discovery input.</summary>
    [Theory]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717", "NT51950")]
    [InlineData("nt51951-fw200-single-auto-prj-695-20260718", "NT51951")]
    public void FirmwareInspectionKeepsValidBaseOnlyFreeOfCompilationIssues(
        string caseId,
        string icId)
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", caseId);
        JsonElement baseArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("artifactId").GetString() == "tp-input");
        string basePath = CanonicalGoldenTestData.ArtifactPath(baseArtifact);

        FirmwareInspectionSnapshot inspection = Assert.Single(
            BuiltInFirmwareInspection.InspectFirmwareBatch(
                BootstrapTestHost.Canonical,
                icId,
                [new FirmwareInspectionSnapshotInput(
                    CompositionSlotIds.ReplaceBase,
                    basePath,
                    CtrlRamRequest: new CtrlRamInspectionRequest(IcNumberSelectionTokens.SingleChip),
                    CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)]))
            .Inspection;

        Assert.Empty(inspection.AuthoringCompilationIssues);
        Assert.Null(inspection.InputSlotStatus);
        Assert.NotNull(inspection.CtrlRamDisplay);
    }
}

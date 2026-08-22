using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
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

    /// <summary>A readable base one byte outside every declared map becomes a typed input issue.</summary>
    [Theory]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717", "NT51950", "tp-input", -1)]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717", "NT51950", "tp-input", 1)]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717", "NT51950", "expected-output", -1)]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717", "NT51950", "expected-output", 1)]
    [InlineData("nt51951-fw200-single-auto-prj-695-20260718", "NT51951", "tp-input", -1)]
    [InlineData("nt51951-fw200-single-auto-prj-695-20260718", "NT51951", "tp-input", 1)]
    [InlineData("nt51951-fw200-single-auto-prj-695-20260718", "NT51951", "expected-output", -1)]
    [InlineData("nt51951-fw200-single-auto-prj-695-20260718", "NT51951", "expected-output", 1)]
    public void NonMapReferenceCapacityReturnsTypedLengthIssue(
        string caseId,
        string icId,
        string baseArtifactId,
        int lengthDelta)
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
            issue => issue.Code == CompositionIssueCodes.InputAddressSpaceLengthMismatch &&
                issue.OperationId == CompositionSlotIds.ReplaceBase);
        Assert.Contains("length", issue.Message, StringComparison.OrdinalIgnoreCase);
    }
}

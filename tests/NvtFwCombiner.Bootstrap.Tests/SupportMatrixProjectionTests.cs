using NvtFwCombiner.Application.Support;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks the headless matrix to exact current authoring and execution routes.</summary>
public sealed class SupportMatrixProjectionTests
{
    private const string ExpectedPolicySha256 =
        "eeffb9be1afba4bc834b17fea63f08d628e170847cc4d0e5f50cdd2f39e9009b";

    /// <summary>NT51950/951 AB publication decisions bind only to their exact map/count pairs.</summary>
    [Fact]
    public void AbCandidateRowsUseExactIcCountAndMapPairs()
    {
        SupportMatrix matrix = WorkbenchCompositionService.GetSupportMatrix();

        Assert.Equal(ExpectedPolicySha256, matrix.Policy.Sha256);
        Assert.Equal(
            [
                ("NT51950", "1-ic", "nt51950-ab-merge-512k"),
                ("NT51950", "2-plus-ic", "nt51950-ab-merge-1024k"),
                ("NT51951", "selector-free", "nt51951-ab-merge-1024k"),
            ],
            [
                .. matrix.Rows
                .Where(static row =>
                    row.Route.Identity.WorkflowId == IcWorkflowIds.AbMerge &&
                    row.Route.Identity.IcId is "NT51950" or "NT51951")
                .Select(static row => (
                    row.Route.Identity.IcId,
                    row.Route.Identity.IcCountVariant,
                    row.Route.Identity.MapVariant))
                .OrderBy(static route => route.IcId, StringComparer.Ordinal)
                .ThenBy(static route => route.IcCountVariant, StringComparer.Ordinal),
            ]);
        Assert.All(
            matrix.Rows.Where(static row =>
                row.Route.Identity.WorkflowId == IcWorkflowIds.AbMerge &&
                row.Route.Identity.IcId is "NT51950" or "NT51951"),
            static row =>
            {
                Assert.Equal(
                    SupportAuthoringAvailability.Available,
                    row.Route.AuthoringAvailability);
                Assert.True(row.Route.ExecutionAdmitted);
                Assert.Equal(
                    SupportPublicationStatus.Candidate,
                    row.PublicationStatus);
                Assert.Equal(
                    SupportEvidenceStatus.ContractOnly,
                    row.Evidence.Status);
            });
        Assert.Equal(
            [
                (
                    "NT51950",
                    "nfc-nt51950-ab-merge-combiner-v1:" +
                        "legacy-combiner-1.13.0",
                    "nt51950-ab-merge-1-ic-nt51950-ab-merge-512k-" +
                        "integrity-ccca6b7eefff20fe"),
                (
                    "NT51950",
                    "nfc-nt51950-ab-merge-combiner-v1:" +
                        "legacy-combiner-1.13.0",
                    "nt51950-ab-merge-2-plus-ic-" +
                        "nt51950-ab-merge-1024k-" +
                        "integrity-ccca6b7eefff20fe"),
                (
                    "NT51951",
                    "nfc-nt51951-ab-merge-combiner-v1:" +
                        "legacy-combiner-1.13.0",
                    "nt51951-ab-merge-selector-free-" +
                        "nt51951-ab-merge-1024k-" +
                        "integrity-76ab8160b124f60a"),
            ],
            [
                .. matrix.Rows
                    .Where(static row =>
                        row.Route.Identity.WorkflowId ==
                            IcWorkflowIds.AbMerge &&
                        row.Route.Identity.IcId is "NT51950" or "NT51951")
                    .Select(static row => (
                        row.Route.Identity.IcId,
                        row.Route.Identity.IntegrityRouteId,
                        row.Route.RouteId))
                    .OrderBy(static row => row.IcId, StringComparer.Ordinal)
                    .ThenBy(static row => row.RouteId, StringComparer.Ordinal),
            ]);
    }

    /// <summary>Policy status never disguises authoring/execution divergence.</summary>
    [Fact]
    public void GeneralRoutesRetainIndependentSupportAxes()
    {
        SupportMatrix matrix = WorkbenchCompositionService.GetSupportMatrix();
        SupportMatrixRow generalMerge = Assert.Single(matrix.Rows, static row =>
            row.Route.RouteId == "nt51919-general-merge-generic");
        SupportMatrixRow exactReplace = Assert.Single(matrix.Rows, static row =>
            row.Route.RouteId ==
                "nt51926-general-replace-1-ic-" +
                "nt51926-general-replace-full-flash-256k");

        Assert.Equal(
            SupportAuthoringAvailability.Available,
            generalMerge.Route.AuthoringAvailability);
        Assert.True(generalMerge.Route.ExecutionAdmitted);
        Assert.Equal(
            SupportPublicationStatus.TestOnly,
            generalMerge.PublicationStatus);
        Assert.Equal(
            SupportEvidenceStatus.ContractOnly,
            generalMerge.Evidence.Status);

        Assert.DoesNotContain(matrix.Rows, static row =>
            row.Route.RouteId == "nt51919-general-replace-generic");
        Assert.Contains(matrix.Diagnostics, static diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.PolicyRouteUnresolved &&
            diagnostic.Subject == "nt51919-general-replace-generic");
        Assert.Contains(matrix.Diagnostics, static diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.SourceScopeUnresolved &&
            diagnostic.Subject ==
                "support-source:NT51919:general-replace:authoring");

        Assert.Equal(
            SupportAuthoringAvailability.Unknown,
            exactReplace.Route.AuthoringAvailability);
        Assert.True(exactReplace.Route.ExecutionAdmitted);
        Assert.Equal(
            SupportPublicationStatus.Unclassified,
            exactReplace.PublicationStatus);
        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.AuthoringRouteUnresolved &&
            diagnostic.Subject == exactReplace.Route.RouteId);
        Assert.Contains(matrix.Diagnostics, static diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.SourceScopeUnresolved &&
            diagnostic.Subject ==
                "support-source:NT51926:general-replace:authoring");
    }

    /// <summary>CtrlRAM uses exact typed postbuild selectors even when its map is topology-invariant.</summary>
    [Theory]
    [InlineData(
        "NT51927",
        "2-ic",
        "nt51927-ctrlram-fw132-twochip-full-flash",
        "nfc.nt51927.ctrlram-postbuild-v1:legacy-combiner-1.13.0:TwoChip")]
    [InlineData(
        "NT51928",
        "3-ic",
        "nt51928-ctrlram-fw140-threechip-full-flash",
        "nfc.nt51928.ctrlram-postbuild-v1:legacy-combiner-1.13.0:ThreeChip")]
    [InlineData(
        "NT51929",
        "2-8-ic",
        "nt51929-ctrlram-fw1x-cascade-full-flash",
        "nfc.nt51929.ctrlram-postbuild-v1:legacy-combiner-1.13.0:Cascade")]
    [InlineData(
        "NT51930",
        "2-13-ic",
        "nt51930-ctrlram-fw130-cascade3-full-flash",
        "nfc.nt51930.ctrlram-postbuild-fw1.x:legacy-combiner-1.13.0:Cascade")]
    public void CtrlRamRowsRetainExactPostbuildIcCount(
        string icId,
        string icCountVariant,
        string mapVariant,
        string expectedIntegrityRouteId)
    {
        SupportMatrix matrix = WorkbenchCompositionService.GetSupportMatrix();

        SupportMatrixRow row = Assert.Single(matrix.Rows, candidate =>
            candidate.Route.Identity.IcId == icId &&
            candidate.Route.Identity.WorkflowId ==
                IcWorkflowIds.CtrlRamReplace &&
            candidate.Route.Identity.IcCountVariant == icCountVariant &&
            candidate.Route.Identity.MapVariant == mapVariant);

        Assert.True(row.Route.ExecutionAdmitted);
        Assert.Equal(
            expectedIntegrityRouteId,
            row.Route.Identity.IntegrityRouteId);
    }

    /// <summary>The snapshot is immutable-by-copy, exact, and rebuilt without static UI state.</summary>
    [Fact]
    public void ProjectionHasUniqueStableRoutesAndFreshSnapshots()
    {
        SupportMatrix first = WorkbenchCompositionService.GetSupportMatrix();
        SupportMatrix second = WorkbenchCompositionService.GetSupportMatrix();
        string[] firstRouteIds =
        [
            .. first.Rows.Select(static row => row.Route.RouteId),
        ];
        string[] secondRouteIds =
        [
            .. second.Rows.Select(static row => row.Route.RouteId),
        ];

        Assert.NotSame(first, second);
        Assert.Equal(firstRouteIds, secondRouteIds);
        Assert.Equal(
            firstRouteIds.Length,
            firstRouteIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            first.Rows.Count,
            first.Rows.Select(static row => row.Route.Identity)
                .Distinct()
                .Count());
        Assert.Equal(
            ["nt51919-general-replace-generic"],
            first.Diagnostics
                .Where(static diagnostic =>
                    diagnostic.Code ==
                        SupportMatrixMaterializer.PolicyRouteUnresolved)
                .Select(static diagnostic => diagnostic.Subject));
        Assert.Equal(
            [
                .. IcSupportCatalog.All
                    .Where(static entry =>
                        entry.SupportsWorkflow(IcWorkflowIds.GeneralReplace))
                    .Select(static entry =>
                        $"support-source:{entry.IcId}:general-replace:authoring")
                    .Order(StringComparer.Ordinal),
            ],
            first.Diagnostics
                .Where(static diagnostic =>
                    diagnostic.Code ==
                        SupportMatrixMaterializer.SourceScopeUnresolved)
                .Select(static diagnostic => diagnostic.Subject));
        Assert.Equal(
            [
                "nt51926-general-replace-1-ic-" +
                "nt51926-general-replace-full-flash-256k",
            ],
            first.Diagnostics
                .Where(static diagnostic =>
                    diagnostic.Code ==
                        SupportMatrixMaterializer.AuthoringRouteUnresolved)
                .Select(static diagnostic => diagnostic.Subject));
        Assert.Contains(first.Rows, static row =>
            row.PublicationStatus ==
                SupportPublicationStatus.Unclassified);
        Assert.False(first.IsMigrationReady);
    }

    /// <summary>An unknown profile remains unresolved rather than fabricating a map identity.</summary>
    [Fact]
    public void TrustedMapLookupFailsClosedForUnknownProfile()
    {
        IReadOnlyList<Domain.Firmware.FirmwareImageMap> maps =
            BuiltInV2BundleRegistry.All[
                "nt51926-ctrlram-replace-candidate"].GetMapVariants(
                "missing-profile",
                "0.1.0",
                "NT51926",
                IcWorkflowIds.GeneralReplace,
                out IcNumberInputMode? icNumberInputMode,
                out IReadOnlyList<CompositionIssue> issues);

        Assert.Empty(maps);
        Assert.Null(icNumberInputMode);
        Assert.Contains(issues, issue =>
            issue.Code == "profile.v2.selection.not-found");
    }
}

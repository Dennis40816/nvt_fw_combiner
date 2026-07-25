using NvtFwCombiner.Application.Support;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks the headless matrix to exact current authoring and execution routes.</summary>
public sealed class SupportMatrixProjectionTests
{
    private const string ExpectedPolicySha256 =
        "e0e9c2dec7a5a3875806d2558d775d094420b305edb7fe63a6e7001290d634ae";

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
                    SupportAuthoringAvailability.Unavailable,
                    row.Route.AuthoringAvailability);
                Assert.True(row.Route.ExecutionAdmitted);
                Assert.Equal(
                    SupportPublicationStatus.Candidate,
                    row.PublicationStatus);
                Assert.Equal(
                    SupportEvidenceStatus.ContractOnly,
                    row.Evidence.Status);
            });
    }

    /// <summary>Policy status never disguises authoring/execution divergence.</summary>
    [Fact]
    public void GeneralRoutesRetainIndependentSupportAxes()
    {
        SupportMatrix matrix = WorkbenchCompositionService.GetSupportMatrix();
        SupportMatrixRow generalMerge = Assert.Single(matrix.Rows, static row =>
            row.Route.RouteId == "nt51919-general-merge-generic");
        SupportMatrixRow genericReplace = Assert.Single(matrix.Rows, static row =>
            row.Route.RouteId == "nt51919-general-replace-generic");
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

        Assert.Equal(
            SupportAuthoringAvailability.Available,
            genericReplace.Route.AuthoringAvailability);
        Assert.False(genericReplace.Route.ExecutionAdmitted);
        Assert.Equal(
            SupportPublicationStatus.TestOnly,
            genericReplace.PublicationStatus);
        Assert.Equal(
            SupportEvidenceStatus.Missing,
            genericReplace.Evidence.Status);
        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.SelectableNotExecutable &&
            diagnostic.Subject == genericReplace.Route.RouteId);

        Assert.Equal(
            SupportAuthoringAvailability.Available,
            exactReplace.Route.AuthoringAvailability);
        Assert.True(exactReplace.Route.ExecutionAdmitted);
        Assert.Equal(
            SupportPublicationStatus.Unclassified,
            exactReplace.PublicationStatus);
    }

    /// <summary>CtrlRAM uses exact typed postbuild selectors even when its map is topology-invariant.</summary>
    [Theory]
    [InlineData(
        "NT51927",
        "2-ic",
        "nt51927-ctrlram-fw132-twochip-full-flash")]
    [InlineData(
        "NT51928",
        "3-ic",
        "nt51928-ctrlram-fw140-threechip-full-flash")]
    [InlineData(
        "NT51929",
        "2-8-ic",
        "nt51929-ctrlram-fw1x-cascade-full-flash")]
    [InlineData(
        "NT51930",
        "2-13-ic",
        "nt51930-ctrlram-fw130-cascade3-full-flash")]
    public void CtrlRamRowsRetainExactPostbuildIcCount(
        string icId,
        string icCountVariant,
        string mapVariant)
    {
        SupportMatrix matrix = WorkbenchCompositionService.GetSupportMatrix();

        SupportMatrixRow row = Assert.Single(matrix.Rows, candidate =>
            candidate.Route.Identity.IcId == icId &&
            candidate.Route.Identity.WorkflowId ==
                IcWorkflowIds.CtrlRamReplace &&
            candidate.Route.Identity.IcCountVariant == icCountVariant &&
            candidate.Route.Identity.MapVariant == mapVariant);

        Assert.True(row.Route.ExecutionAdmitted);
        Assert.NotEqual(
            "not-applicable",
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
        Assert.DoesNotContain(first.Diagnostics, diagnostic =>
            diagnostic.Code ==
                SupportMatrixMaterializer.AuthoringRouteUnresolved);
        Assert.DoesNotContain(first.Diagnostics, diagnostic =>
            diagnostic.Code ==
                SupportMatrixMaterializer.PolicyRouteUnresolved);
        Assert.DoesNotContain(first.Diagnostics, diagnostic =>
            diagnostic.Code ==
                SupportMatrixMaterializer.SourceScopeUnresolved);
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

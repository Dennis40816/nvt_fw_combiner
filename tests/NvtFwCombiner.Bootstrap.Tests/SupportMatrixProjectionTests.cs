using NvtFwCombiner.Application.Support;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks the headless matrix to exact current authoring and execution routes.</summary>
public sealed class SupportMatrixProjectionTests
{
    private const string ExpectedPolicySha256 =
        "365a6ee92776bbd6b1aaa155919121dfbbbfc67046c3ab6a2fbfe7fa5d45c5c2";

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
                    "transform-scalar:28:relocate-tpb-diff-for-b-bank|" +
                        "external-processor:32:nfc-nt51950-ab-merge-combiner-v1:" +
                        "22:legacy-combiner-1.13.0|fingerprint:" +
                        "f859b871f87deb06e77b82bcb0dd0055638a23989a44be5344d6e1856cad385d",
                    "route-7-nt51950-8-ab-merge-4-1-ic-21-" +
                        "nt51950-ab-merge-512k-integrity-" +
                        "de1df72be12d1b57dfcbf272889653a8faede4f2334d64e54126bf586902e5ab"),
                (
                    "NT51950",
                    "transform-scalar:28:relocate-tpb-diff-for-b-bank|" +
                        "external-processor:32:nfc-nt51950-ab-merge-combiner-v1:" +
                        "22:legacy-combiner-1.13.0|fingerprint:" +
                        "f859b871f87deb06e77b82bcb0dd0055638a23989a44be5344d6e1856cad385d",
                    "route-7-nt51950-8-ab-merge-9-2-plus-ic-22-" +
                        "nt51950-ab-merge-1024k-integrity-" +
                        "de1df72be12d1b57dfcbf272889653a8faede4f2334d64e54126bf586902e5ab"),
                (
                    "NT51951",
                    "transform-scalar:28:relocate-tpb-diff-for-b-bank|" +
                        "external-processor:32:nfc-nt51951-ab-merge-combiner-v1:" +
                        "22:legacy-combiner-1.13.0|fingerprint:" +
                        "513690905d39f26ac8afb63a63d4efb9f24e9c0cc9bdbfc2df7028d6b655c252",
                    "route-7-nt51951-8-ab-merge-13-selector-free-22-" +
                        "nt51951-ab-merge-1024k-integrity-" +
                        "6435dea0731a432f950b22ae880dab32329ef1f04bb449f8a2556552791f5622"),
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
            row.Route.Identity.IcId == "NT51919" &&
            row.Route.Identity.WorkflowId == IcWorkflowIds.GeneralMerge);
        SupportMatrixRow exactReplace = Assert.Single(matrix.Rows, static row =>
            row.Route.Identity.IcId == "NT51926" &&
            row.Route.Identity.WorkflowId == IcWorkflowIds.GeneralReplace);

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

        SupportMatrixRow generalReplace = Assert.Single(
            matrix.Rows,
            static row =>
                row.Route.Identity.IcId == "NT51919" &&
                row.Route.Identity.WorkflowId ==
                    IcWorkflowIds.GeneralReplace);
        Assert.Equal(
            SupportAuthoringAvailability.Unknown,
            generalReplace.Route.AuthoringAvailability);
        Assert.False(generalReplace.Route.ExecutionAdmitted);
        Assert.Equal(
            SupportPublicationStatus.TestOnly,
            generalReplace.PublicationStatus);
        Assert.Equal(
            SupportEvidenceStatus.Missing,
            generalReplace.Evidence.Status);
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

    /// <summary>Host-side AB header relocation participates in exact integrity identity.</summary>
    [Theory]
    [InlineData("NT51919")]
    [InlineData("NT51929")]
    [InlineData("NT51932")]
    public void AbHeaderRelocationIsAnIntegrityRoute(string icId)
    {
        SupportMatrix matrix = WorkbenchCompositionService.GetSupportMatrix();

        SupportMatrixRow row = Assert.Single(matrix.Rows, candidate =>
            candidate.Route.Identity.IcId == icId &&
            candidate.Route.Identity.WorkflowId == IcWorkflowIds.AbMerge);

        Assert.Contains(
            "transform-scalar",
            row.Route.Identity.IntegrityRouteId,
            StringComparison.Ordinal);
        Assert.Contains(
            "relocate-tpb-ilm",
            row.Route.Identity.IntegrityRouteId,
            StringComparison.Ordinal);
        Assert.Contains(
            "relocate-tpb-dlm",
            row.Route.Identity.IntegrityRouteId,
            StringComparison.Ordinal);
        Assert.Contains(
            "relocate-tpb-diff",
            row.Route.Identity.IntegrityRouteId,
            StringComparison.Ordinal);
    }

    /// <summary>CtrlRAM uses exact typed postbuild selectors even when its map is topology-invariant.</summary>
    [Theory]
    [InlineData(
        "NT51927",
        "2-ic",
        "nt51927-ctrlram-fw132-twochip-full-flash",
        "nfc.nt51927.ctrlram-postbuild-v1:legacy-combiner-1.13.0:TwoChip|" +
        "fingerprint:377e425b48b614b28362f38f4f62308ec79959a76727e4397d3055312e72a245")]
    [InlineData(
        "NT51928",
        "3-ic",
        "nt51928-ctrlram-fw140-threechip-full-flash",
        "nfc.nt51928.ctrlram-postbuild-v1:legacy-combiner-1.13.0:ThreeChip|" +
        "fingerprint:7c8e24d9289f342ab1827dd1cc6b726af979b161f9a44a972f67a80eb3506c1e")]
    [InlineData(
        "NT51929",
        "2-8-ic",
        "nt51929-ctrlram-fw1x-cascade-full-flash",
        "nfc.nt51929.ctrlram-postbuild-v1:legacy-combiner-1.13.0:Cascade|" +
        "fingerprint:5e4ebeb59a8044793163e0a0dc452c91a620dcbfaaacb32b5c0d26c90662793a")]
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
        Assert.True(
            StringComparer.Ordinal.Equals(expectedIntegrityRouteId, row.Route.Identity.IntegrityRouteId),
            $"Expected integrity route: {expectedIntegrityRouteId}{Environment.NewLine}" +
            $"Actual integrity route: {row.Route.Identity.IntegrityRouteId}");
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
        SupportMatrixRow nt51928StandardMerge = Assert.Single(
            first.Rows,
            static row =>
                row.Route.Identity.IcId == "NT51928" &&
                row.Route.Identity.WorkflowId == IcWorkflowIds.StandardMerge);
        Assert.Equal(
            "nt51928-standard-merge-512k",
            nt51928StandardMerge.Route.Identity.MapVariant);
        Assert.Equal(
            first.Rows.Count,
            first.Rows.Select(static row => row.Route.Identity)
                .Distinct()
                .Count());
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
                "route-7-nt51919-15-general-replace-14-" +
                "not-applicable-7-generic",
                "route-7-nt51926-15-general-replace-4-1-ic-39-" +
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

using NvtFwCombiner.Application.Support;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Projection tests for the current registry-backed Support Matrix reporting facade.</summary>
public sealed class SupportMatrixProjectionTests
{
    /// <summary>Verifies explicit current policy rows remain exact and no unmatched route is auto-promoted.</summary>
    [Fact]
    public void CurrentProjectionRetainsExactCandidateRowsWithoutPromotingTheRest()
    {
        SupportMatrix matrix = WorkbenchCompositionService.GetSupportMatrix();

        Assert.Equal("af3feb72cf0db6d90a47199cd4e78d08ac62d15dc5057b9cbb0359cb23fb5851", matrix.Policy.Sha256);
        Assert.Contains(matrix.Rows, row =>
            row.Route.RouteId == "nt51950-ab-merge-single" &&
            row.Route.MapVariant == "nt51950-ab-merge-512k" &&
            row.Route.AuthoringAvailability == SupportAuthoringAvailability.Unknown &&
            row.PublicationStatus == SupportPublicationStatus.Candidate &&
            row.Evidence.Status == SupportEvidenceStatus.ContractOnly);
        Assert.Contains(matrix.Rows, row =>
            row.Route.RouteId == "nt51950-ab-merge-cascade" &&
            row.Route.MapVariant == "nt51950-ab-merge-1024k" &&
            row.PublicationStatus == SupportPublicationStatus.Candidate);
        Assert.Contains(matrix.Rows, row =>
            row.Route.RouteId == "nt51951-ab-merge-selector-free" &&
            row.PublicationStatus == SupportPublicationStatus.Candidate);
        Assert.Contains(matrix.Rows, row =>
            row.Route.RouteId == "nt51919-general-merge-generic" &&
            row.PublicationStatus == SupportPublicationStatus.TestOnly &&
            !row.Route.ExecutionAdmitted &&
            row.Evidence.Status == SupportEvidenceStatus.Missing);
        Assert.Contains(matrix.Rows, row =>
            row.Route.RouteId == "nt51926-general-replace-nt51926-general-replace-full-flash-256k" &&
            row.Route.IcCountVariant == "single" &&
            row.Route.MapVariant == "nt51926-general-replace-full-flash-256k");
        Assert.Contains(matrix.Rows, row =>
            row.Route.IcId == "NT51917" &&
            row.Route.WorkflowId == "dp-replace" &&
            row.Route.IcCountVariant == "single");
        Assert.Contains(matrix.Rows, row =>
            row.Route.IcId == "NT51917" &&
            row.Route.WorkflowId == "ctrlram-replace" &&
            row.Route.MapVariant == "nt51927-ctrlram-fw141-single-full-flash" &&
            row.Route.IntegrityRouteId == "nfc.nt51917.ctrlram-postbuild-v1:SingleChip");
        Assert.Contains(matrix.Rows, row =>
            row.PublicationStatus == SupportPublicationStatus.Unclassified);
    }

    /// <summary>Verifies unresolved policy and coarse authoring sources are visible as fail-closed diagnostics.</summary>
    [Fact]
    public void CurrentProjectionFailsClosedForUnboundPolicyAndCoarseAuthoringScopes()
    {
        SupportMatrix matrix = WorkbenchCompositionService.GetSupportMatrix();

        Assert.False(matrix.IsMigrationReady);
        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.PolicyRouteUnresolved &&
            diagnostic.Subject == "nt51919-general-replace-generic");
        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.SourceScopeUnresolved &&
            diagnostic.Subject == "ic-support:NT51919:general-replace");
        Assert.Contains(matrix.Rows, row =>
            row.Route.IcId == "NT51917" &&
            row.Route.WorkflowId == "ctrlram-replace" &&
            row.Route.ExecutionAdmitted &&
            row.Route.AuthoringAvailability == SupportAuthoringAvailability.Unknown);
        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.SourceScopeUnresolved &&
            diagnostic.Subject == "ic-support:NT51917:ctrlram-replace");
        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.AuthoringRouteUnresolved &&
            diagnostic.Subject == "nt51950-ab-merge-single");
        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.AuthoringRouteUnresolved &&
            diagnostic.Subject == "nt51951-ab-merge-selector-free");
        Assert.DoesNotContain(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.SelectableNotExecutable);
    }

    /// <summary>Verifies a reporting map-identity lookup fails closed instead of fabricating a profile-id map variant.</summary>
    [Fact]
    public void TrustedMapIdentityLookupFailsClosedForAnUnknownRuntimeProfile()
    {
        IReadOnlyList<string> mapIds = BuiltInV2BundleRegistry.All["nt51926-ctrlram-replace-candidate"].GetMapIds(
            "missing-profile",
            "0.1.0",
            "NT51926",
            ExperienceIds.GeneralReplace,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.Empty(mapIds);
        Assert.Contains(issues, issue =>
            issue.Code == "profile.v2.selection.not-found");
    }
}

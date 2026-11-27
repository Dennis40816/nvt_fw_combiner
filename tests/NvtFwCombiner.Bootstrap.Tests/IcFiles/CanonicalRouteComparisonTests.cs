// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Infrastructure.Capabilities;

namespace NvtFwCombiner.Bootstrap.Tests.IcFiles;

/// <summary>Pins every canonical route to a neutral self-comparison or an explicit runtime-input requirement.</summary>
public sealed class CanonicalRouteComparisonTests
{
    /// <summary>The existing compiler path accounts for all 94 policy routes without silently skipping any.</summary>
    [Fact]
    public void EveryCanonicalPolicyRouteIsComparedOrNamesItsRequiredRuntimeInput()
    {
        CanonicalCapabilityPolicySnapshot policy = BuiltInCanonicalCapabilityPolicy.Load();

        CanonicalRouteComparisonReport report = NeutralPlanComparison.CompareCanonicalRoutes(policy.Routes);

        Assert.Equal(94, policy.Routes.Count);
        Assert.Equal(7, report.ComparedRoutes.Length);
        Assert.Equal(87, report.RequiresRuntimeInput.Length);
        Assert.Equal(94, report.ComparedRoutes.Length + report.RequiresRuntimeInput.Length);
        Assert.Equal(ExpectedComparedRouteIds, report.ComparedRoutes.Select(static route => route.RouteId));
        Assert.Equal<RouteRuntimeInputRequirement>(ExpectedRuntimeInputRoutes, report.RequiresRuntimeInput);
        Assert.All(report.ComparedRoutes, static route => Assert.Empty(route.Differences));
        Assert.Equal(
            policy.Routes.Select(static route => route.Identity.RouteId).Order(StringComparer.Ordinal),
            report.ComparedRoutes.Select(static route => route.RouteId)
                .Concat(report.RequiresRuntimeInput.Select(static route => route.RouteId)).Order(StringComparer.Ordinal));
    }

    private static string[] ExpectedComparedRouteIds =>
    [
        "route-7-nt51917-14-standard-merge-13-selector-free-27-nt51927-standard-merge-256k",
        "route-7-nt51919-14-standard-merge-13-selector-free-27-nt51919-standard-merge-256k",
        "route-7-nt51923-14-standard-merge-13-selector-free-27-nt51923-standard-merge-256k",
        "route-7-nt51926-14-standard-merge-13-selector-free-27-nt51926-standard-merge-256k",
        "route-7-nt51927-14-standard-merge-13-selector-free-27-nt51927-standard-merge-256k",
        "route-7-nt51929-14-standard-merge-13-selector-free-27-nt51929-standard-merge-256k",
        "route-7-nt51932-14-standard-merge-13-selector-free-27-nt51932-standard-merge-256k",
    ];

    // Exact policy ids and typed requirements are pinned here, independently of the runtime classifier.
    private static RouteRuntimeInputRequirement[] ExpectedRuntimeInputRoutes =>
    [
        new("route-7-nt51917-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51917-15-ctrlram-replace-4-1-ic-41-nt51927-ctrlram-fw141-single-tp-work-212k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51917-15-ctrlram-replace-4-2-ic-40-nt51927-ctrlram-fw132-twochip-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51917-15-ctrlram-replace-4-2-ic-42-nt51927-ctrlram-fw132-twochip-tp-work-212k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51917-15-ctrlram-replace-4-3-ic-42-nt51927-ctrlram-fw140-threechip-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51917-15-ctrlram-replace-4-3-ic-44-nt51927-ctrlram-fw140-threechip-tp-work-212k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51917-13-general-merge-14-not-applicable-7-generic", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51919-8-ab-merge-13-selector-free-21-nt51919-ab-merge-512k", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51919-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51919-15-ctrlram-replace-6-2-8-ic-39-nt51929-ctrlram-fw1x-cascade-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51919-13-general-merge-14-not-applicable-7-generic", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51923-15-ctrlram-replace-4-1-ic-39-nt51923-ctrlram-fw141-single-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51923-15-ctrlram-replace-4-1-ic-41-nt51923-ctrlram-fw141-single-tp-work-240k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51923-15-ctrlram-replace-9-2-plus-ic-41-nt51923-ctrlram-fw141-cascade3-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51923-15-ctrlram-replace-9-2-plus-ic-43-nt51923-ctrlram-fw141-cascade3-tp-work-240k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51923-13-general-merge-14-not-applicable-7-generic", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51925-15-ctrlram-replace-4-1-ic-44-nt51925-ctrlram-fw141-single-full-flash-256k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51925-15-ctrlram-replace-4-1-ic-41-nt51925-ctrlram-fw141-single-tp-work-192k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51925-15-ctrlram-replace-4-1-ic-44-nt51925-ctrlram-fw200-single-full-flash-256k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51925-15-ctrlram-replace-4-1-ic-41-nt51925-ctrlram-fw200-single-tp-work-192k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51925-15-ctrlram-replace-9-2-plus-ic-45-nt51925-ctrlram-fw141-cascade-full-flash-256k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51925-15-ctrlram-replace-9-2-plus-ic-42-nt51925-ctrlram-fw141-cascade-tp-work-192k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51925-15-ctrlram-replace-9-2-plus-ic-45-nt51925-ctrlram-fw200-cascade-full-flash-256k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51925-15-ctrlram-replace-9-2-plus-ic-42-nt51925-ctrlram-fw200-cascade-tp-work-192k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51925-14-standard-merge-13-selector-free-22-nt51925-common-fw-256k", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51926-15-ctrlram-replace-4-1-ic-37-nt51926-ctrlram-fw141-full-flash-256k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51926-15-ctrlram-replace-4-1-ic-34-nt51926-ctrlram-fw141-tp-work-240k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51926-15-ctrlram-replace-4-1-ic-37-nt51926-ctrlram-fw200-full-flash-256k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51926-15-ctrlram-replace-4-1-ic-34-nt51926-ctrlram-fw200-tp-work-240k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-37-nt51926-ctrlram-fw141-full-flash-256k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-34-nt51926-ctrlram-fw141-tp-work-240k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-37-nt51926-ctrlram-fw200-full-flash-256k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-34-nt51926-ctrlram-fw200-tp-work-240k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51926-13-general-merge-14-not-applicable-7-generic", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51926-15-general-replace-4-1-ic-39-nt51926-general-replace-full-flash-256k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51927-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51927-15-ctrlram-replace-4-1-ic-41-nt51927-ctrlram-fw141-single-tp-work-212k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51927-15-ctrlram-replace-4-2-ic-40-nt51927-ctrlram-fw132-twochip-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51927-15-ctrlram-replace-4-2-ic-42-nt51927-ctrlram-fw132-twochip-tp-work-212k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51927-15-ctrlram-replace-4-3-ic-42-nt51927-ctrlram-fw140-threechip-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51927-15-ctrlram-replace-4-3-ic-44-nt51927-ctrlram-fw140-threechip-tp-work-212k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51927-13-general-merge-14-not-applicable-7-generic", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51928-15-ctrlram-replace-4-1-ic-39-nt51928-ctrlram-fw141-single-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51928-15-ctrlram-replace-4-1-ic-41-nt51928-ctrlram-fw141-single-tp-work-212k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51928-15-ctrlram-replace-4-2-ic-40-nt51928-ctrlram-fw132-twochip-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51928-15-ctrlram-replace-4-2-ic-42-nt51928-ctrlram-fw132-twochip-tp-work-212k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51928-15-ctrlram-replace-4-3-ic-42-nt51928-ctrlram-fw140-threechip-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51928-15-ctrlram-replace-4-3-ic-44-nt51928-ctrlram-fw140-threechip-tp-work-212k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51928-13-general-merge-14-not-applicable-7-generic", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51928-14-standard-merge-13-selector-free-31-nt51928-dual-capacity-256k-512k", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51929-8-ab-merge-13-selector-free-21-nt51929-ab-merge-512k", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51929-15-ctrlram-replace-4-1-ic-21-nt51929-ab-merge-512k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51929-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51929-15-ctrlram-replace-6-2-8-ic-39-nt51929-ctrlram-fw1x-cascade-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51929-13-general-merge-14-not-applicable-7-generic", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51932-8-ab-merge-13-selector-free-21-nt51932-ab-merge-512k", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51932-15-ctrlram-replace-4-1-ic-38-nt51932-ctrlram-fw1x-single-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51932-15-ctrlram-replace-6-2-8-ic-40-nt51932-ctrlram-fw200-cascade-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51932-13-general-merge-14-not-applicable-7-generic", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51950-8-ab-merge-4-1-ic-21-nt51950-ab-merge-maps", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51950-8-ab-merge-9-2-plus-ic-23-nt51950-ab-cascade-maps", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51950-15-ctrlram-replace-4-1-ic-39-nt51950-ctrlram-fw200-single-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51950-15-ctrlram-replace-4-1-ic-36-nt51950-ctrlram-fw200-single-tp-work", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51950-15-ctrlram-replace-4-2-ic-39-nt51950-ctrlram-fw1x-cascade-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51950-15-ctrlram-replace-4-2-ic-36-nt51950-ctrlram-fw1x-cascade-tp-work", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51950-13-general-merge-14-not-applicable-7-generic", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51950-14-standard-merge-13-selector-free-28-nt51950-standard-merge-1024k", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51950-14-standard-merge-13-selector-free-27-nt51950-standard-merge-256k", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51950-14-standard-merge-13-selector-free-27-nt51950-standard-merge-512k", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51951-8-ab-merge-13-selector-free-22-nt51951-ab-merge-1024k", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51951-15-ctrlram-replace-4-1-ic-39-nt51951-ctrlram-fw200-single-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51951-15-ctrlram-replace-4-1-ic-36-nt51951-ctrlram-fw200-single-tp-work", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51951-15-ctrlram-replace-4-2-ic-39-nt51951-ctrlram-fw1x-cascade-full-flash", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51951-15-ctrlram-replace-4-2-ic-36-nt51951-ctrlram-fw1x-cascade-tp-work", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51951-13-general-merge-14-not-applicable-7-generic", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51951-14-standard-merge-13-selector-free-28-nt51951-standard-merge-1024k", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51951-14-standard-merge-13-selector-free-27-nt51951-standard-merge-256k", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51951-14-standard-merge-13-selector-free-27-nt51951-standard-merge-512k", RouteRuntimeInputReason.AuthoringRequest),
        new("route-7-nt51919-15-ctrlram-replace-4-1-ic-21-nt51919-ab-merge-512k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51919-15-ctrlram-replace-6-2-8-ic-21-nt51919-ab-merge-512k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51929-15-ctrlram-replace-6-2-8-ic-21-nt51929-ab-merge-512k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51932-15-ctrlram-replace-4-1-ic-21-nt51932-ab-merge-512k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51932-15-ctrlram-replace-6-2-8-ic-21-nt51932-ab-merge-512k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51950-15-ctrlram-replace-4-1-ic-21-nt51950-ab-merge-512k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51950-15-ctrlram-replace-4-2-ic-22-nt51950-ab-merge-1024k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51951-15-ctrlram-replace-4-1-ic-22-nt51951-ab-merge-1024k", RouteRuntimeInputReason.ReferenceFile),
        new("route-7-nt51951-15-ctrlram-replace-4-2-ic-22-nt51951-ab-merge-1024k", RouteRuntimeInputReason.ReferenceFile),
    ];
}

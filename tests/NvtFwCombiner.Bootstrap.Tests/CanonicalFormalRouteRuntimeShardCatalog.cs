// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Immutable;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Explicit immutable route assignments; catalog additions require reviewed shard placement.</summary>
internal static class CanonicalFormalRouteRuntimeShardCatalog
{
    internal static ImmutableArray<CanonicalFormalRouteRuntimeShard> Shards { get; } =
    [
        new(
            nameof(CanonicalFormalRouteRuntimeStandardMergeNt51917ToNt51932Tests),
            ExperienceIds.StandardMerge,
            ExpectedRouteCount: 8,
            ExpectedCaseCount: 9,
            RouteIds:
            [
                "route-7-nt51917-14-standard-merge-13-selector-free-27-nt51927-standard-merge-256k",
                "route-7-nt51919-14-standard-merge-13-selector-free-27-nt51919-standard-merge-256k",
                "route-7-nt51923-14-standard-merge-13-selector-free-27-nt51923-standard-merge-256k",
                "route-7-nt51926-14-standard-merge-13-selector-free-27-nt51926-standard-merge-256k",
                "route-7-nt51927-14-standard-merge-13-selector-free-27-nt51927-standard-merge-256k",
                "route-7-nt51928-14-standard-merge-13-selector-free-31-nt51928-dual-capacity-256k-512k",
                "route-7-nt51929-14-standard-merge-13-selector-free-27-nt51929-standard-merge-256k",
                "route-7-nt51932-14-standard-merge-13-selector-free-27-nt51932-standard-merge-256k",
            ]),
        new(
            nameof(CanonicalFormalRouteRuntimeStandardMergeNt51950AndNt51951Tests),
            ExperienceIds.StandardMerge,
            ExpectedRouteCount: 6,
            ExpectedCaseCount: 6,
            RouteIds:
            [
                "route-7-nt51950-14-standard-merge-13-selector-free-28-nt51950-standard-merge-1024k",
                "route-7-nt51950-14-standard-merge-13-selector-free-27-nt51950-standard-merge-256k",
                "route-7-nt51950-14-standard-merge-13-selector-free-27-nt51950-standard-merge-512k",
                "route-7-nt51951-14-standard-merge-13-selector-free-28-nt51951-standard-merge-1024k",
                "route-7-nt51951-14-standard-merge-13-selector-free-27-nt51951-standard-merge-256k",
                "route-7-nt51951-14-standard-merge-13-selector-free-27-nt51951-standard-merge-512k",
            ]),
        new(
            nameof(CanonicalFormalRouteRuntimeAbMergeTests),
            ExperienceIds.AbMerge,
            ExpectedRouteCount: 6,
            ExpectedCaseCount: 7,
            RouteIds:
            [
                "route-7-nt51919-8-ab-merge-13-selector-free-21-nt51919-ab-merge-512k",
                "route-7-nt51929-8-ab-merge-13-selector-free-21-nt51929-ab-merge-512k",
                "route-7-nt51932-8-ab-merge-13-selector-free-21-nt51932-ab-merge-512k",
                "route-7-nt51950-8-ab-merge-4-1-ic-21-nt51950-ab-merge-maps",
                "route-7-nt51950-8-ab-merge-9-2-plus-ic-23-nt51950-ab-cascade-maps",
                "route-7-nt51951-8-ab-merge-13-selector-free-22-nt51951-ab-merge-1024k",
            ]),
        new(
            nameof(CanonicalFormalRouteRuntimeCtrlRamReplaceNt51917AndNt51919Tests),
            ExperienceIds.CtrlRamReplace,
            ExpectedRouteCount: 10,
            ExpectedCaseCount: 12,
            RouteIds:
            [
                "route-7-nt51917-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash",
                "route-7-nt51917-15-ctrlram-replace-4-1-ic-41-nt51927-ctrlram-fw141-single-tp-work-212k",
                "route-7-nt51917-15-ctrlram-replace-4-2-ic-40-nt51927-ctrlram-fw132-twochip-full-flash",
                "route-7-nt51917-15-ctrlram-replace-4-2-ic-42-nt51927-ctrlram-fw132-twochip-tp-work-212k",
                "route-7-nt51917-15-ctrlram-replace-4-3-ic-42-nt51927-ctrlram-fw140-threechip-full-flash",
                "route-7-nt51917-15-ctrlram-replace-4-3-ic-44-nt51927-ctrlram-fw140-threechip-tp-work-212k",
                "route-7-nt51919-15-ctrlram-replace-4-1-ic-21-nt51919-ab-merge-512k",
                "route-7-nt51919-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash",
                "route-7-nt51919-15-ctrlram-replace-6-2-8-ic-21-nt51919-ab-merge-512k",
                "route-7-nt51919-15-ctrlram-replace-6-2-8-ic-39-nt51929-ctrlram-fw1x-cascade-full-flash",
            ]),
        new(
            nameof(CanonicalFormalRouteRuntimeCtrlRamReplaceNt51923AndNt51928Tests),
            ExperienceIds.CtrlRamReplace,
            ExpectedRouteCount: 10,
            ExpectedCaseCount: 14,
            RouteIds:
            [
                "route-7-nt51923-15-ctrlram-replace-4-1-ic-39-nt51923-ctrlram-fw141-single-full-flash",
                "route-7-nt51923-15-ctrlram-replace-4-1-ic-41-nt51923-ctrlram-fw141-single-tp-work-240k",
                "route-7-nt51923-15-ctrlram-replace-9-2-plus-ic-41-nt51923-ctrlram-fw141-cascade3-full-flash",
                "route-7-nt51923-15-ctrlram-replace-9-2-plus-ic-43-nt51923-ctrlram-fw141-cascade3-tp-work-240k",
                "route-7-nt51928-15-ctrlram-replace-4-1-ic-39-nt51928-ctrlram-fw141-single-full-flash",
                "route-7-nt51928-15-ctrlram-replace-4-1-ic-41-nt51928-ctrlram-fw141-single-tp-work-212k",
                "route-7-nt51928-15-ctrlram-replace-4-2-ic-40-nt51928-ctrlram-fw132-twochip-full-flash",
                "route-7-nt51928-15-ctrlram-replace-4-2-ic-42-nt51928-ctrlram-fw132-twochip-tp-work-212k",
                "route-7-nt51928-15-ctrlram-replace-4-3-ic-42-nt51928-ctrlram-fw140-threechip-full-flash",
                "route-7-nt51928-15-ctrlram-replace-4-3-ic-44-nt51928-ctrlram-fw140-threechip-tp-work-212k",
            ]),
        new(
            nameof(CanonicalFormalRouteRuntimeCtrlRamReplaceNt51926Tests),
            ExperienceIds.CtrlRamReplace,
            ExpectedRouteCount: 8,
            ExpectedCaseCount: 16,
            RouteIds:
            [
                "route-7-nt51926-15-ctrlram-replace-4-1-ic-37-nt51926-ctrlram-fw141-full-flash-256k",
                "route-7-nt51926-15-ctrlram-replace-4-1-ic-34-nt51926-ctrlram-fw141-tp-work-240k",
                "route-7-nt51926-15-ctrlram-replace-4-1-ic-37-nt51926-ctrlram-fw200-full-flash-256k",
                "route-7-nt51926-15-ctrlram-replace-4-1-ic-34-nt51926-ctrlram-fw200-tp-work-240k",
                "route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-37-nt51926-ctrlram-fw141-full-flash-256k",
                "route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-34-nt51926-ctrlram-fw141-tp-work-240k",
                "route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-37-nt51926-ctrlram-fw200-full-flash-256k",
                "route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-34-nt51926-ctrlram-fw200-tp-work-240k",
            ]),
        new(
            nameof(CanonicalFormalRouteRuntimeCtrlRamReplaceNt51927AndNt51929Tests),
            ExperienceIds.CtrlRamReplace,
            ExpectedRouteCount: 10,
            ExpectedCaseCount: 12,
            RouteIds:
            [
                "route-7-nt51927-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash",
                "route-7-nt51927-15-ctrlram-replace-4-1-ic-41-nt51927-ctrlram-fw141-single-tp-work-212k",
                "route-7-nt51927-15-ctrlram-replace-4-2-ic-40-nt51927-ctrlram-fw132-twochip-full-flash",
                "route-7-nt51927-15-ctrlram-replace-4-2-ic-42-nt51927-ctrlram-fw132-twochip-tp-work-212k",
                "route-7-nt51927-15-ctrlram-replace-4-3-ic-42-nt51927-ctrlram-fw140-threechip-full-flash",
                "route-7-nt51927-15-ctrlram-replace-4-3-ic-44-nt51927-ctrlram-fw140-threechip-tp-work-212k",
                "route-7-nt51929-15-ctrlram-replace-4-1-ic-21-nt51929-ab-merge-512k",
                "route-7-nt51929-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash",
                "route-7-nt51929-15-ctrlram-replace-6-2-8-ic-21-nt51929-ab-merge-512k",
                "route-7-nt51929-15-ctrlram-replace-6-2-8-ic-39-nt51929-ctrlram-fw1x-cascade-full-flash",
            ]),
        new(
            nameof(CanonicalFormalRouteRuntimeCtrlRamReplaceNt51932AndNt51950Tests),
            ExperienceIds.CtrlRamReplace,
            ExpectedRouteCount: 10,
            ExpectedCaseCount: 12,
            RouteIds:
            [
                "route-7-nt51932-15-ctrlram-replace-4-1-ic-21-nt51932-ab-merge-512k",
                "route-7-nt51932-15-ctrlram-replace-4-1-ic-38-nt51932-ctrlram-fw1x-single-full-flash",
                "route-7-nt51932-15-ctrlram-replace-6-2-8-ic-21-nt51932-ab-merge-512k",
                "route-7-nt51932-15-ctrlram-replace-6-2-8-ic-40-nt51932-ctrlram-fw200-cascade-full-flash",
                "route-7-nt51950-15-ctrlram-replace-4-1-ic-21-nt51950-ab-merge-512k",
                "route-7-nt51950-15-ctrlram-replace-4-1-ic-39-nt51950-ctrlram-fw200-single-full-flash",
                "route-7-nt51950-15-ctrlram-replace-4-1-ic-36-nt51950-ctrlram-fw200-single-tp-work",
                "route-7-nt51950-15-ctrlram-replace-4-2-ic-22-nt51950-ab-merge-1024k",
                "route-7-nt51950-15-ctrlram-replace-4-2-ic-39-nt51950-ctrlram-fw1x-cascade-full-flash",
                "route-7-nt51950-15-ctrlram-replace-4-2-ic-36-nt51950-ctrlram-fw1x-cascade-tp-work",
            ]),
        new(
            nameof(CanonicalFormalRouteRuntimeCtrlRamReplaceNt51951Tests),
            ExperienceIds.CtrlRamReplace,
            ExpectedRouteCount: 6,
            ExpectedCaseCount: 6,
            RouteIds:
            [
                "route-7-nt51951-15-ctrlram-replace-4-1-ic-22-nt51951-ab-merge-1024k",
                "route-7-nt51951-15-ctrlram-replace-4-1-ic-39-nt51951-ctrlram-fw200-single-full-flash",
                "route-7-nt51951-15-ctrlram-replace-4-1-ic-36-nt51951-ctrlram-fw200-single-tp-work",
                "route-7-nt51951-15-ctrlram-replace-4-2-ic-22-nt51951-ab-merge-1024k",
                "route-7-nt51951-15-ctrlram-replace-4-2-ic-39-nt51951-ctrlram-fw1x-cascade-full-flash",
                "route-7-nt51951-15-ctrlram-replace-4-2-ic-36-nt51951-ctrlram-fw1x-cascade-tp-work",
            ]),
    ];
}

internal sealed record CanonicalFormalRouteRuntimeShard(
    string Name,
    string WorkflowId,
    int ExpectedRouteCount,
    int ExpectedCaseCount,
    ImmutableArray<string> RouteIds);

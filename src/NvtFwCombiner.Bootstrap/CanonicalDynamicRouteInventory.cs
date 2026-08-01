using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Definition-level inventory for routes compiled from current bounded authoring state.</summary>
internal static class CanonicalDynamicRouteInventory
{
    internal const string Nt51928DualCapacityMapVariantSetId =
        "nt51928-dual-capacity-256k-512k";

    internal static bool IsDynamic(CapabilityRouteIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return (StringComparer.Ordinal.Equals(identity.IcId, "NT51928") &&
               identity.WorkflowId is
                   IcWorkflowIds.StandardMerge or IcWorkflowIds.DpReplace) ||
               identity.WorkflowId is
                   IcWorkflowIds.GeneralMerge or
                   IcWorkflowIds.GeneralReplace or
                   IcWorkflowIds.CtrlRamReplace;
    }

    internal static CanonicalDynamicRoute Resolve(
        CapabilityRouteIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return identity.WorkflowId switch
        {
            IcWorkflowIds.StandardMerge or IcWorkflowIds.DpReplace
                when StringComparer.Ordinal.Equals(identity.IcId, "NT51928") =>
                ResolveNt51928(identity),
            IcWorkflowIds.GeneralMerge => ResolveGeneralMerge(identity),
            IcWorkflowIds.GeneralReplace => ResolveGeneralReplace(identity),
            IcWorkflowIds.CtrlRamReplace => ResolveCtrlRam(identity),
            _ => throw new InvalidDataException(
                $"No dynamic capability definition matches route '{identity.RouteId}'."),
        };
    }

    internal static CapabilityRouteIdentity ResolveCtrlRamIdentity(
        CtrlRamV2Route route,
        LegacyCombinerPostbuildCommandPlan plan,
        long referenceCapacity)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(plan);
        IReadOnlyList<FirmwareImageMap> maps =
            BuiltInV2BundleRegistry.All[route.BundleId].GetMapVariants(
                route.ProfileId,
                route.ProfileVersion,
                route.Key.IcId,
                IcWorkflowIds.CtrlRamReplace,
                out IReadOnlyList<CompositionIssue> issues);
        FirmwareImageMap map = issues.Count == 0
            ? maps.SingleOrDefault(candidate =>
                    candidate.CapacityBytes == referenceCapacity) ??
                throw new InvalidDataException(
                    $"CtrlRAM reference capacity 0x{referenceCapacity:X} has no canonical map.")
            : throw InvalidDefinition(
                new CapabilityRouteIdentity(
                    route.Key.IcId,
                    IcWorkflowIds.CtrlRamReplace,
                    "unresolved",
                    "unresolved"),
                issues);
        return new CapabilityRouteIdentity(
            route.Key.IcId,
            IcWorkflowIds.CtrlRamReplace,
            FormatCtrlRamCountVariant(
                plan.Selector,
                plan.Branch == LegacyCombinerPostbuildBranch.Cascade
                    ? plan.Profile.DiffDlmPolicy
                    : null),
            map.MapId);
    }

    private static CanonicalDynamicRoute ResolveNt51928(
        CapabilityRouteIdentity identity)
    {
        BuiltInV2Registration registration = identity.WorkflowId ==
            IcWorkflowIds.StandardMerge
                ? BuiltInV2RegistrationRegistry.StandardMergeByIc[identity.IcId]
                : BuiltInV2RegistrationRegistry.DpReplaceByIc.Value[identity.IcId];
        IReadOnlyList<FirmwareImageMap> maps = registration.GetMapVariants(
            out IcNumberInputMode? inputMode,
            out IReadOnlyList<CompositionIssue> issues);
        string[] allowedMapIds = issues.Count == 0
            ? [.. maps.Select(static map => map.MapId)]
            : throw InvalidDefinition(identity, issues);
        string[] countVariants =
        [
            .. maps.Select(map => HeadlessRouteSelection.TryFormatIcCountVariant(
                    map.Applicability.TopologyRequirement,
                    inputMode) ??
                throw new InvalidDataException(
                    $"NT51928 route '{identity.RouteId}' has an unresolved IC Count axis."))
                .Distinct(StringComparer.Ordinal),
        ];
        _ = countVariants.Length == 1 &&
            StringComparer.Ordinal.Equals(
                identity.MapVariant,
                Nt51928DualCapacityMapVariantSetId) &&
            StringComparer.Ordinal.Equals(
                identity.IcCountVariant,
                countVariants[0])
            ? true
            : throw new InvalidDataException(
                $"NT51928 dynamic route '{identity.RouteId}' does not match its reviewed map-set axes.");

        return Create(
            identity,
            registration.ProfileId,
            registration.ProfileVersion,
            registration.BundleContentHash,
            allowedMapIds,
            CapabilityDefinitionFingerprint.MapBoundCompilerSemanticId,
            registration.InputSelectionGroupMemberSlotIds);
    }

    private static CanonicalDynamicRoute ResolveGeneralMerge(
        CapabilityRouteIdentity identity)
    {
        GeneralMergeV2CandidateRegistration registration =
            BuiltInV2RegistrationRegistry.GeneralMergeByIc.GetValueOrDefault(
                identity.IcId) ??
            throw new InvalidDataException(
                $"No General Merge definition matches route '{identity.RouteId}'.");
        _ = StringComparer.Ordinal.Equals(
                identity.IcCountVariant,
                "not-applicable") &&
            StringComparer.Ordinal.Equals(identity.MapVariant, "generic")
            ? true
            : throw new InvalidDataException(
                $"General Merge route '{identity.RouteId}' has invalid logical-output axes.");

        return Create(
            identity,
            registration.ProfileId,
            WorkbenchCompositionService.GeneralMergeV2CandidateProfileVersion,
            registration.Bundle.ContentHash,
            ["generic"],
            CapabilityDefinitionFingerprint.LogicalOutputCompilerSemanticId,
            [$"family:{registration.FamilyId}"]);
    }

    private static CanonicalDynamicRoute ResolveGeneralReplace(
        CapabilityRouteIdentity identity)
    {
        if (!StringComparer.Ordinal.Equals(
                identity.IcId,
                WorkbenchCompositionService.Nt51926GeneralReplaceIcId))
        {
            throw new InvalidDataException(
                $"No General Replace definition matches route '{identity.RouteId}'.");
        }

        IReadOnlyList<FirmwareImageMap> maps =
            WorkbenchCompositionService.GetNt51926GeneralReplaceSupportMaps(
                out IcNumberInputMode? inputMode,
                out IReadOnlyList<CompositionIssue> issues);
        FirmwareImageMap map = issues.Count == 0
            ? maps.SingleOrDefault(candidate => StringComparer.Ordinal.Equals(
                    candidate.MapId,
                    identity.MapVariant)) ??
                throw new InvalidDataException(
                    $"No General Replace map matches route '{identity.RouteId}'.")
            : throw InvalidDefinition(identity, issues);
        string? countVariant = HeadlessRouteSelection.TryFormatIcCountVariant(
            map.Applicability.TopologyRequirement,
            inputMode);
        if (!StringComparer.Ordinal.Equals(countVariant, identity.IcCountVariant))
        {
            throw new InvalidDataException(
                $"General Replace route '{identity.RouteId}' has invalid IC Count axes.");
        }

        BuiltInV2Bundle bundle = BuiltInV2BundleRegistry.All[
            WorkbenchCompositionService.Nt51926GeneralReplaceBundleId];
        return Create(
            identity,
            WorkbenchCompositionService.Nt51926GeneralReplaceDpProfileId,
            WorkbenchCompositionService.Nt51926GeneralReplaceDpProfileVersion,
            bundle.ContentHash,
            [map.MapId],
            CapabilityDefinitionFingerprint.RuntimeReferenceReplaceCompilerSemanticId,
            []);
    }

    private static CanonicalDynamicRoute ResolveCtrlRam(
        CapabilityRouteIdentity identity)
    {
        CanonicalCtrlRamDefinition[] matches =
        [
            .. CtrlRamV2RouteRegistry.All.SelectMany(CreateCtrlRamDefinitions)
                .Where(candidate =>
                    StringComparer.Ordinal.Equals(
                        candidate.Identity.RouteId,
                        identity.RouteId)),
        ];
        CanonicalCtrlRamDefinition match = matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException(
                $"CtrlRAM route '{identity.RouteId}' matched {matches.Length} reviewed definitions.");
        BuiltInV2Bundle bundle = BuiltInV2BundleRegistry.All[match.Route.BundleId];
        BuiltInV2Registration reportMetadataRegistration =
            BuiltInV2RegistrationRegistry.StandardMergeByIc.GetValueOrDefault(
                match.Route.Key.IcId) ??
            throw new InvalidDataException(
                $"CtrlRAM route '{identity.RouteId}' has no reviewed Standard Merge metadata definition.");
        return Create(
            identity,
            match.Route.ProfileId,
            match.Route.ProfileVersion,
            bundle.ContentHash,
            [match.Map.MapId],
            CapabilityDefinitionFingerprint.RuntimeReferenceReplaceCompilerSemanticId,
            [
                $"postbuild-processor:{match.Route.Key.PostbuildProcessorId}",
                $"postbuild-selector:{match.Selector.Token}",
                $"postbuild-plan:{match.PlanFingerprint}",
                $"report-metadata-profile:{reportMetadataRegistration.ProfileId}@{reportMetadataRegistration.ProfileVersion}",
                $"report-metadata-bundle:{reportMetadataRegistration.BundleContentHash}",
                $"report-metadata-slot:{CompositionAddressSpaceIds.TpInput}<-{CompositionAddressSpaceIds.ReferenceBase}",
            ]);
    }

    private static IEnumerable<CanonicalCtrlRamDefinition>
        CreateCtrlRamDefinitions(CtrlRamV2Route route)
    {
        LegacyCombinerPostbuildProfile postbuild =
            BuiltInPostbuildProfileCatalog.GetProfiles(route.Key.IcId)
                .Single(profile => StringComparer.Ordinal.Equals(
                    profile.ProcessorId,
                    route.Key.PostbuildProcessorId));
        LegacyCombinerPostbuildPlanSelector selector =
            postbuild.PlanSelectors.Single(candidate =>
                candidate.Branch == route.Key.Branch);
        LegacyCombinerPostbuildCommandPlan plan =
            LegacyCombinerPostbuildPlanner.CreatePlan(postbuild, selector);
        IReadOnlyList<FirmwareImageMap> maps =
            BuiltInV2BundleRegistry.All[route.BundleId].GetMapVariants(
                route.ProfileId,
                route.ProfileVersion,
                route.Key.IcId,
                IcWorkflowIds.CtrlRamReplace,
                out IReadOnlyList<CompositionIssue> issues);
        if (issues.Count != 0)
        {
            throw InvalidDefinition(
                new CapabilityRouteIdentity(
                    route.Key.IcId,
                    IcWorkflowIds.CtrlRamReplace,
                    FormatCtrlRamCountVariant(selector, postbuild.DiffDlmPolicy),
                    "unresolved"),
                issues);
        }

        string countVariant = FormatCtrlRamCountVariant(
            selector,
            postbuild.DiffDlmPolicy);
        foreach (FirmwareImageMap map in maps)
        {
            yield return new CanonicalCtrlRamDefinition(
                new CapabilityRouteIdentity(
                    route.Key.IcId,
                    IcWorkflowIds.CtrlRamReplace,
                    countVariant,
                    map.MapId),
                route,
                selector,
                map,
                LegacyCombinerPostbuildPlanner.CalculateIntegrityFingerprint(
                    plan,
                    map.CapacityBytes));
        }
    }

    private static string FormatCtrlRamCountVariant(
        LegacyCombinerPostbuildPlanSelector selector,
        LegacyCombinerDiffDlmPolicy? diffDlmPolicy)
    {
        int minimum = selector.MinimumCount;
        int maximum = selector.MaximumCount;
        if (selector.Branch == LegacyCombinerPostbuildBranch.Cascade &&
            diffDlmPolicy is not null)
        {
            minimum = Math.Max(minimum, diffDlmPolicy.MinimumIcCount);
            maximum = Math.Min(maximum, diffDlmPolicy.MaximumIcCount);
        }

        return minimum == maximum
            ? FormattableString.Invariant($"{minimum}-ic")
            : maximum == int.MaxValue
                ? FormattableString.Invariant($"{minimum}-plus-ic")
                : FormattableString.Invariant($"{minimum}-{maximum}-ic");
    }

    private static CanonicalDynamicRoute Create(
        CapabilityRouteIdentity identity,
        string profileId,
        string profileVersion,
        string trustedDefinitionSha256,
        IReadOnlyList<string> allowedMapIds,
        string compilerSemanticId,
        IReadOnlyList<string> semanticBindingIds)
    {
        string fingerprint = CapabilityDefinitionFingerprint.Compute(
            identity,
            profileId,
            profileVersion,
            trustedDefinitionSha256,
            allowedMapIds,
            compilerSemanticId,
            semanticBindingIds);
        return new CanonicalDynamicRoute(
            fingerprint,
            new CanonicalCapabilityCompilationContract(
                profileId,
                profileVersion,
                allowedMapIds,
                allowsLogicalOutput: StringComparer.Ordinal.Equals(
                    compilerSemanticId,
                    CapabilityDefinitionFingerprint.LogicalOutputCompilerSemanticId)));
    }

    private static InvalidDataException InvalidDefinition(
        CapabilityRouteIdentity identity,
        IReadOnlyList<CompositionIssue> issues)
    {
        return new InvalidDataException(
            $"Dynamic definition '{identity.RouteId}' was rejected: " +
            string.Join(", ", issues.Select(static issue => issue.Code)));
    }
}

internal sealed record CanonicalDynamicRoute(
    string CapabilityFingerprint,
    CanonicalCapabilityCompilationContract CompilationContract);

internal sealed record CanonicalCtrlRamDefinition(
    CapabilityRouteIdentity Identity,
    CtrlRamV2Route Route,
    LegacyCombinerPostbuildPlanSelector Selector,
    FirmwareImageMap Map,
    string PlanFingerprint);

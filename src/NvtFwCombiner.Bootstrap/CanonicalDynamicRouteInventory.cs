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
    internal static bool IsDynamic(CapabilityRouteIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return (TryGetMapBoundRegistration(identity, out BuiltInV2Registration? registration) &&
                registration.SelectionGroupMapVariantSetId is not null) ||
               identity.WorkflowId is
                   IcWorkflowIds.GeneralMerge or
                   IcWorkflowIds.GeneralReplace or
                   IcWorkflowIds.CtrlRamReplace;
    }

    internal static CanonicalDynamicRoute Resolve(
        CapabilityRouteIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return TryGetMapBoundRegistration(
                   identity,
                   out BuiltInV2Registration? registration) &&
               registration.SelectionGroupMapVariantSetId is not null
            ? ResolveSelectionGroup(identity, registration)
            : identity.WorkflowId switch
            {
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

    private static CanonicalDynamicRoute ResolveSelectionGroup(
        CapabilityRouteIdentity identity,
        BuiltInV2Registration registration)
    {
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
                    $"Selection-group route '{identity.RouteId}' has an unresolved IC Count axis."))
                .Distinct(StringComparer.Ordinal),
        ];
        _ = countVariants.Length == 1 &&
            StringComparer.Ordinal.Equals(
                identity.MapVariant,
                registration.SelectionGroupMapVariantSetId) &&
            StringComparer.Ordinal.Equals(
                identity.IcCountVariant,
                countVariants[0])
            ? true
            : throw new InvalidDataException(
                $"Selection-group route '{identity.RouteId}' does not match its reviewed map-set axes.");

        return Create(
            identity,
            registration.ProfileId,
            registration.ProfileVersion,
            registration.BundleContentHash,
            allowedMapIds,
            CapabilityDefinitionFingerprint.MapBoundCompilerSemanticId,
            registration.InputSelectionGroupMemberSlotIds);
    }

    private static bool TryGetMapBoundRegistration(
        CapabilityRouteIdentity identity,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out BuiltInV2Registration? registration)
    {
        IReadOnlyDictionary<string, BuiltInV2Registration>? registrations =
            identity.WorkflowId switch
            {
                IcWorkflowIds.StandardMerge => BuiltInV2RegistrationRegistry.StandardMergeByIc,
                IcWorkflowIds.DpReplace => BuiltInV2RegistrationRegistry.DpReplaceByIc.Value,
                _ => null,
            };
        registration = registrations?.GetValueOrDefault(identity.IcId);
        return registration is not null;
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
            registration.ProfileVersion,
            registration.Bundle.ContentHash,
            ["generic"],
            CapabilityDefinitionFingerprint.LogicalOutputCompilerSemanticId,
            [$"family:{registration.FamilyId}"]);
    }

    private static CanonicalDynamicRoute ResolveGeneralReplace(
        CapabilityRouteIdentity identity)
    {
        if (!BuiltInV2RegistrationRegistry.GeneralReplaceByIc.TryGetValue(
                identity.IcId,
                out GeneralReplaceV2Registration? registration))
        {
            throw new InvalidDataException(
                $"No General Replace definition matches route '{identity.RouteId}'.");
        }

        IReadOnlyList<FirmwareImageMap> maps =
            registration.GetMapVariants(
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
        _ = StringComparer.Ordinal.Equals(countVariant, identity.IcCountVariant)
            ? true
            : throw new InvalidDataException(
                $"General Replace route '{identity.RouteId}' has invalid IC Count axes.");

        return Create(
            identity,
            registration.ProfileId,
            registration.ProfileVersion,
            registration.BundleContentHash,
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
        var semanticBindings = new List<string>
        {
            $"postbuild-processor:{match.Route.Key.PostbuildProcessorId}",
            $"postbuild-selector:{match.Selector.Token}",
            $"postbuild-plan:{match.PlanFingerprint}",
        };
        if (reportMetadataRegistration.HasReportClassificationMetadata)
        {
            semanticBindings.Add(
                $"report-metadata-profile:{reportMetadataRegistration.ProfileId}@{reportMetadataRegistration.ProfileVersion}");
            semanticBindings.Add(
                $"report-metadata-bundle:{reportMetadataRegistration.BundleContentHash}");
            semanticBindings.Add(
                $"report-metadata-slot:{CompositionAddressSpaceIds.TpInput}<-{CompositionAddressSpaceIds.ReferenceBase}");
        }

        return Create(
            identity,
            match.Route.ProfileId,
            match.Route.ProfileVersion,
            bundle.ContentHash,
            [match.Map.MapId],
            CapabilityDefinitionFingerprint.RuntimeReferenceReplaceCompilerSemanticId,
            semanticBindings);
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
                trustedDefinitionSha256,
                allowedMapIds,
                compilerSemanticId,
                semanticBindingIds,
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

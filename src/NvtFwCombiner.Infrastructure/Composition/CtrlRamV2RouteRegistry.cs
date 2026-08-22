using System.Collections.ObjectModel;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Composition;

/// <summary>Projects production CtrlRAM routes from the reviewed package trust index.</summary>
internal static class CtrlRamV2RouteRegistry
{
    private static readonly ReadOnlyCollection<CtrlRamV2Route> Routes = Array.AsReadOnly(
    [
        .. BuiltInV2BundleRegistry.TrustIndex.Bundles
            .SelectMany(
                static bundle => bundle.RuntimeRegistrations,
                static (bundle, registration) => (Bundle: bundle, Registration: registration))
            .Where(static item => StringComparer.Ordinal.Equals(
                item.Registration.WorkflowId,
                ExperienceIds.CtrlRamReplace))
            .Select(static item => CreateRoute(item.Bundle, item.Registration))
            .OrderBy(static route => route.Key.IcId, StringComparer.Ordinal)
            .ThenBy(static route => route.Key.PostbuildProcessorId, StringComparer.Ordinal)
            .ThenBy(static route => route.Key.Branch),
    ]);

    private static readonly ReadOnlyDictionary<CtrlRamV2RouteKey, CtrlRamV2Route> ByKey =
        new(Routes.ToDictionary(static route => route.Key));

    internal static IReadOnlyList<CtrlRamV2Route> All => Routes;

    internal static bool TryResolve(
        LegacyCombinerPostbuildCommandPlan plan,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out CtrlRamV2Route? route)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return TryResolve(plan.Profile, plan.Branch, out route);
    }

    internal static bool TryResolve(
        LegacyCombinerPostbuildProfile profile,
        LegacyCombinerPostbuildBranch branch,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out CtrlRamV2Route? route)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return ByKey.TryGetValue(
            new CtrlRamV2RouteKey(profile.IcId, profile.ProcessorId, branch),
            out route);
    }

    private static CtrlRamV2Route CreateRoute(
        ProfileBundlePackageTrustEntry bundle,
        ProfileBundleRuntimeRegistration registration)
    {
        BuiltInV2Registration? standardRegistration =
            BuiltInV2RegistrationRegistry.StandardMergeByIc.GetValueOrDefault(
                registration.IcId);
        MetadataPlanDefinition reportMetadataPlan =
            ValidateReportMetadataCounterpart(registration, standardRegistration);
        return new CtrlRamV2Route(
            new CtrlRamV2RouteKey(
                registration.IcId,
                registration.PostbuildProcessorId!,
                ParseBranch(registration.PostbuildBranch!)),
            bundle.BundleDirectory,
            registration.ProfileId,
            registration.ProfileVersion,
            registration.ReportMetadataMapId,
            reportMetadataPlan);
    }

    /// <summary>Fails closed before any CtrlRAM registration becomes a published route.</summary>
    internal static MetadataPlanDefinition ValidateReportMetadataCounterpart(
        ProfileBundleRuntimeRegistration registration,
        BuiltInV2Registration? standardRegistration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (standardRegistration is null ||
            !StringComparer.Ordinal.Equals(
                registration.IcId,
                standardRegistration.IcId))
        {
            throw new InvalidDataException(
                $"CtrlRAM registration for {registration.IcId} has no exact Standard Merge registration.");
        }

        _ = standardRegistration.HasReportClassificationMetadata ==
            (registration.ReportMetadataMapId is not null)
            ? true
            : throw new InvalidDataException(
                $"CtrlRAM registration for {registration.IcId} has incoherent report metadata map authority.");

        return registration.ReportMetadataMapId is null
            ? MetadataPlanDefinition.Empty
            : CreateReportMetadataPlan(
                standardRegistration,
                registration.ReportMetadataMapId);
    }

    private static MetadataPlanDefinition CreateReportMetadataPlan(
        BuiltInV2Registration standardRegistration,
        string reportMetadataMapId)
    {
        MetadataPlanDefinition sourcePlan =
            standardRegistration.CreateExactMapMetadataPlan(reportMetadataMapId);
        MetadataPlanEntry[] entries =
        [
            .. sourcePlan.Entries
                .Where(static entry => entry.Purposes.Contains(
                    MetadataReferencePurpose.ReportClassification))
                .Select(static entry => new MetadataPlanEntry(
                    entry.BindingId,
                    entry.SpaceId,
                    CompositionAddressSpaceIds.ReferenceBase,
                    entry.FamilyDefinition,
                    entry.ResolvedMap,
                    entry.MetadataSetBinding,
                    entry.StructureDefinition,
                    entry.TargetReferences,
                    entry.Purposes,
                    entry.EvidenceRefs)),
        ];
        _ = entries.Length != 0 &&
            entries.All(entry => StringComparer.Ordinal.Equals(
                entry.ResolvedMap.ImageMap.MapId,
                reportMetadataMapId))
            ? true
            : throw new InvalidDataException(
                $"Exact Standard Merge map '{reportMetadataMapId}' has no matching report-classification plan.");

        return new MetadataPlanDefinition(entries, sourcePlan.SourceIdentity);
    }

    private static LegacyCombinerPostbuildBranch ParseBranch(string token)
    {
        return token switch
        {
            "single-chip" => LegacyCombinerPostbuildBranch.SingleChip,
            "two-chip" => LegacyCombinerPostbuildBranch.TwoChip,
            "three-chip" => LegacyCombinerPostbuildBranch.ThreeChip,
            "cascade" => LegacyCombinerPostbuildBranch.Cascade,
            _ => throw new InvalidDataException("Unknown package trust-index postbuild branch."),
        };
    }
}

internal sealed record CtrlRamV2RouteKey(
    string IcId,
    string PostbuildProcessorId,
    LegacyCombinerPostbuildBranch Branch);

internal sealed record CtrlRamV2Route(
    CtrlRamV2RouteKey Key,
    string BundleId,
    string ProfileId,
    string ProfileVersion,
    string? ReportMetadataMapId,
    MetadataPlanDefinition ReportMetadataPlan);

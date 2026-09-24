using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Infrastructure.Composition;

internal sealed record BankReplaceRouteBinding(
    CapabilityRouteIdentity Identity,
    BankReferenceReplaceDefinition Definition,
    CanonicalCtrlRamDefinition Local,
    BuiltInV2Registration Layout);

internal static partial class CanonicalDynamicRouteInventory
{
    private const string PerfectAbBundleId = "nt51919-nt51929-nt51932-ab-merge";
    private const string PartialAbBundleId = "nt51950-ab-merge";

    internal static BankReplaceRouteBinding? FindBankReplaceBinding(string icId, string countVariant)
    {
        CanonicalCtrlRamDefinition[] localDefinitions =
            [.. CtrlRamV2RouteRegistry.All.SelectMany(CreateCtrlRamDefinitions)];
        return CreateBankReplaceBindings(localDefinitions).Values.SingleOrDefault(binding =>
            binding.Identity.IcId == icId && binding.Identity.IcCountVariant == countVariant);
    }

    internal static BankReplaceRouteBinding? FindBankReplaceBinding(string routeId)
    {
        CanonicalCtrlRamDefinition[] localDefinitions =
            [.. CtrlRamV2RouteRegistry.All.SelectMany(CreateCtrlRamDefinitions)];
        return CreateBankReplaceBindings(localDefinitions).GetValueOrDefault(routeId);
    }

    internal static IReadOnlyDictionary<string, BankReplaceRouteBinding> CreateBankReplaceBindings(
        IReadOnlyList<CanonicalCtrlRamDefinition> localDefinitions)
    {
        ArgumentNullException.ThrowIfNull(localDefinitions);
        var bindings = new Dictionary<string, BankReplaceRouteBinding>(StringComparer.Ordinal);
        foreach (string bundleId in new[] { PerfectAbBundleId, PartialAbBundleId })
        {
            BuiltInV2Bundle layoutBundle = BuiltInV2BundleRegistry.All[bundleId];
            foreach (BuiltInV2Registration layout in BuiltInV2RegistrationRegistry.AbMerge.Where(registration =>
                         registration.BundleContentHash == layoutBundle.ContentHash))
            {
                IReadOnlyList<FirmwareImageMap> maps = layout.GetMapVariants(out _, out IReadOnlyList<CompositionIssue> issues);
                if (issues.Count != 0 || maps.Count != 1)
                {
                    throw new InvalidDataException($"AB layout for {layout.IcId} is not one trusted map.");
                }
                FirmwareImageMap map = maps[0];
                foreach (CanonicalCtrlRamDefinition local in localDefinitions)
                {
                    BankReferenceReplaceAdmission? admission = layoutBundle.TryGetBankReplaceAdmission(
                        BuiltInV2BundleRegistry.All[local.Route.BundleId], layout.IcId, local.Identity.IcId,
                        layout.ProfileId, layout.ProfileVersion, map.MapId,
                        local.Route.ProfileId, local.Route.ProfileVersion, local.Map.MapId);
                    if (admission is null) { continue; }
                    var identity = new CapabilityRouteIdentity(layout.IcId, ExperienceIds.CtrlRamReplace,
                        admission.IcCountVariant, map.MapId);
                    BankReferenceReplaceDefinition definition = admission.Definition;
                    if (!bindings.TryAdd(identity.RouteId, new(identity, definition, local, layout)))
                    {
                        throw new InvalidDataException($"Duplicate AB Replace route '{identity.RouteId}'.");
                    }
                }
            }
        }
        return bindings;
    }

    private static CanonicalDynamicRoute ResolveBankReplace(BankReplaceRouteBinding binding)
    {
        CanonicalCtrlRamDefinition local = binding.Local;
        BankReferenceReplaceDefinition definition = binding.Definition;
        return Create(binding.Identity, definition.DefinitionId, definition.Version, definition.ContentHash,
            [definition.Layout.MapId], BankReferenceReplaceDefinition.CompilerSemanticId,
            ["bank-definition:" + definition.ContentHash,
                "postbuild-selector:" + local.Selector.Token, "postbuild-plan:" + local.PlanFingerprint]);
    }
}

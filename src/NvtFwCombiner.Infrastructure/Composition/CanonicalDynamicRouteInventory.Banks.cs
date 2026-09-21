using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.Composition;

internal static partial class CanonicalDynamicRouteInventory
{
    internal static CapabilityRouteIdentity BankReplaceIdentity { get; } = new(
        "NT51929", ExperienceIds.CtrlRamReplace, "1-ic", "nt51929-ab-merge-512k");

    internal static BankReferenceReplaceDefinition CreateBankReplaceDefinition()
    {
        return BuiltInV2BundleRegistry.All.TryGetValue("nt51919-nt51929-nt51932-ab-merge", out BuiltInV2Bundle? layout) &&
            BuiltInV2BundleRegistry.All.TryGetValue("nt51929-ctrlram-replace-candidate", out BuiltInV2Bundle? local)
            ? layout.GetBankReplaceDefinition(local)
            : throw new InvalidDataException($"AB Replace route '{BankReplaceIdentity.RouteId}' requires both trusted parent bundles.");
    }

    private static CanonicalDynamicRoute ResolveBankReplace(CapabilityRouteIdentity identity, BankReferenceReplaceDefinition definition,
        IReadOnlyList<CanonicalCtrlRamDefinition> localDefinitions)
    {
        CanonicalCtrlRamDefinition[] matches = [.. localDefinitions.Where(candidate => candidate.Route.ProfileId == definition.Local.ProfileId &&
            candidate.Route.ProfileVersion == definition.Local.ProfileVersion && candidate.Identity.IcId == definition.Local.MemberId &&
            candidate.Map.MapId == definition.Local.MapId && candidate.Map.CapacityBytes == definition.Local.CapacityBytes)];
        CanonicalCtrlRamDefinition local = matches.Length == 1 ? matches[0] : throw new InvalidDataException(
            $"AB Replace route '{identity.RouteId}' matched {matches.Length} definitions for parent '{definition.Local.ProfileId}@{definition.Local.ProfileVersion}'.");
        return Create(identity, definition.DefinitionId, definition.Version, definition.ContentHash,
            [definition.Layout.MapId], BankReferenceReplaceDefinition.CompilerSemanticId,
            ["bank-definition:" + definition.ContentHash,
                "postbuild-selector:" + local.Selector.Token, "postbuild-plan:" + local.PlanFingerprint]);
    }
}

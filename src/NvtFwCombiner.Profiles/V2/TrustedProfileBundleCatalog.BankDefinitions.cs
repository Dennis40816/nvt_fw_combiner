using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal sealed partial class TrustedProfileBundleCatalog
{
    /// <summary>Resolves the closed composite from declarations only; no firmware payload is required.</summary>
    internal BankReferenceReplaceDefinition CreateBankReplaceDefinition(TrustedProfileBundleCatalog local)
    {
        ArgumentNullException.ThrowIfNull(local);
        return new BankReferenceReplaceDefinition(
            Source(this, "nt51929-ab-merge", "0.4.0", ExperienceIds.AbMerge, "nt51929-ab-merge-512k"),
            Source(local, "nt51929-ctrlram-replace-fw200-single", "0.3.0", ExperienceIds.CtrlRamReplace, "nt51929-ctrlram-fw200-single-full-flash"));

        static BankReferenceDefinitionSource Source(TrustedProfileBundleCatalog catalog, string profileId, string version, string mode, string mapId)
        {
            TrustedCompositionProfileCatalogEntry profile = catalog.SelectProfile(profileId, version, out _) ??
                throw new ArgumentException($"AB Replace parent '{profileId}@{version}' is unavailable.");
            IReadOnlyList<FirmwareImageMap> maps = catalog.GetMapVariants(profileId, version, "NT51929", mode, out _, out IReadOnlyList<CompositionIssue> issues);
            FirmwareImageMap map = issues.Count == 0
                ? maps.SingleOrDefault(candidate => candidate.MapId == mapId) ?? throw new ArgumentException($"AB Replace parent map '{mapId}' is unavailable.")
                : throw new ArgumentException(string.Join("; ", issues.Select(static issue => issue.Message)));
            return new BankReferenceDefinitionSource(profileId, version, catalog.BundleIdentity, profile.EntryIdentity,
                profile.Family.Family.FamilyContentHash, "NT51929", map.MapId, map.CapacityBytes);
        }
    }
}

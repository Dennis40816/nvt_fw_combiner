using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Composition;

internal sealed partial class BuiltInFirmwareInspection
{
    /// <summary>Reads FWConfig display metadata from the canonical NVT-located Backup in a selected firmware image.</summary>
    internal static FirmwareConfigMetadataSnapshot? TryReadFirmwareConfigMetadata(
        ICompositionCapabilityExperience projection,
        string icId,
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        byte[]? image = TryReadFirmwareImage(path);
        return image is null
            ? null
            : ReadFirmwareConfigMetadata(projection, icId, image);
    }

    internal static bool TryReadBaseCommonFwVersion(
        ICompositionCapabilityExperience projection,
        string icId,
        string basePath,
        out string? commonFwVersion)
    {
        commonFwVersion = null;
        if (!TryReadFirmwareConfigBackupMetadata(
                projection,
                icId,
                basePath,
                out FirmwareConfigMetadata metadata))
        {
            return false;
        }

        commonFwVersion = metadata.CommonFwVersion;
        return true;
    }

    private static bool TryResolveNumberTokenForFirmwareConfig(
        ICompositionCapabilityExperience projection,
        string icId,
        FirmwareConfigMetadata firmwareConfig,
        out string? numberToken)
    {
        numberToken = null;
        if (!TryResolvePostbuildProfileForDisplay(
                projection,
                icId,
                firmwareConfig,
                out LegacyCombinerPostbuildProfile? profile) ||
            profile is null)
        {
            return false;
        }

        LegacyCombinerPostbuildPlanSelector[] matches =
        [
            .. profile.PlanSelectors.Where(selector =>
                selector.MatchesReportedChipCount(firmwareConfig.ChipNumber)),
        ];
        if (matches.Length != 1 ||
            !CtrlRamV2RouteRegistry.TryResolve(profile, matches[0].Branch, out _))
        {
            return false;
        }

        numberToken = matches[0].Token;
        return true;
    }

    internal static bool TryReadFirmwareConfigBackupMetadata(
        ICompositionCapabilityExperience projection,
        string icId,
        string path,
        out FirmwareConfigMetadata metadata)
    {
        metadata = default;
        if (!projection.IsKnownIcId(icId))
        {
            return false;
        }

        byte[]? image = TryReadFirmwareImage(path);
        return image is not null && TryReadFirmwareConfigBackupMetadata(
            projection,
            icId,
            image,
            out metadata);
    }

    internal static byte[]? TryReadFirmwareImage(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    internal static bool TryReadFirmwareConfigBackupMetadata(
        ICompositionCapabilityExperience projection,
        string icId,
        ReadOnlySpan<byte> image,
        out FirmwareConfigMetadata metadata)
    {
        metadata = default;
        if (!projection.IsKnownIcId(icId) ||
            !FirmwareConfigMetadataReader.TryReadBackup(image, out FirmwareConfigMetadata backup))
        {
            return false;
        }

        metadata = backup;
        return true;
    }

    internal static bool TryResolvePostbuildProfileFromBasePathForDisplay(
        ICompositionCapabilityExperience projection,
        string icId,
        string? basePath,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        postbuildProfile = null;
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles =
            BuiltInPostbuildProfileCatalog.GetProfiles(
                IcIdentifier.Normalize(icId));
        if (profiles.Count == 0)
        {
            return false;
        }

        if (profiles.Count == 1)
        {
            postbuildProfile = profiles[0];
            return true;
        }

        bool hasReadableBase = !string.IsNullOrWhiteSpace(basePath) && File.Exists(basePath);
        bool matchedBaseProfile = hasReadableBase &&
            TryReadBaseCommonFwVersion(
                projection,
                icId,
                basePath!,
                out string? commonFwVersion) &&
            BuiltInPostbuildProfileCatalog.TrySelectProfileForCommonFwVersion(
                IcIdentifier.Normalize(icId),
                commonFwVersion,
                out postbuildProfile,
                out _);

        postbuildProfile ??= !hasReadableBase && profiles.Count != 0
            ? profiles[0]
            : null;
        return matchedBaseProfile || postbuildProfile is not null;
    }

    internal static bool TryResolvePostbuildProfileFromAcceptedBaseForDisplay(
        ICompositionCapabilityExperience projection,
        string icId,
        ReadOnlySpan<byte> acceptedBaseBytes,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        FirmwareConfigMetadata? metadata =
            TryReadFirmwareConfigBackupMetadata(
                projection,
                icId,
                acceptedBaseBytes,
                out FirmwareConfigMetadata parsed)
                    ? parsed
                    : null;
        return TryResolvePostbuildProfileForDisplay(
            projection,
            icId,
            metadata,
            out postbuildProfile);
    }

    private static (
        DpVersionMetadata? Version,
        CmiDpCodeMetadata? Cmi,
        FirmwareMetadataPrerequisite? Prerequisite)
        ReadDpMetadata(
            byte[] image,
            byte[]? tpImage,
            string? standardMergeAddressSpaceId,
            FirmwareMetadataPlanAuthority metadataAuthority)
    {
        if (TryReadCanonicalDpcmi(
                image,
                tpImage,
                standardMergeAddressSpaceId,
                metadataAuthority,
                out DpcmiMetadataFacts? dpcmi,
                out FirmwareMetadataPrerequisite? prerequisite))
        {
            return dpcmi is null
                ? (null, null, prerequisite)
                : (
                    new DpVersionMetadata(dpcmi.VersionToken),
                    new CmiDpCodeMetadata(
                        dpcmi.MajorVersion,
                        dpcmi.MinorVersion,
                        dpcmi.JiraNumber,
                        checked((int)dpcmi.ResolvedRange.Start)),
                    null);
        }

        return (null, null, null);
    }

    private static bool TryReadCanonicalDpcmi(
        byte[] image,
        byte[]? tpImage,
        string? standardMergeAddressSpaceId,
        FirmwareMetadataPlanAuthority metadataAuthority,
        out DpcmiMetadataFacts? facts,
        out FirmwareMetadataPrerequisite? prerequisite)
    {
        facts = null;
        prerequisite = null;
        ArgumentNullException.ThrowIfNull(metadataAuthority);
        if (!metadataAuthority.IsApplicable)
        {
            return false;
        }

        bool isStandardMergeDpInput = StringComparer.Ordinal.Equals(
            standardMergeAddressSpaceId,
            CompositionAddressSpaceIds.DpInput);
        ResolvedMetadataPlan? plan = metadataAuthority.Plan;

        if (!DeclaresDpcmi(plan))
        {
            return false;
        }

        if (image.Length == 0)
        {
            return true;
        }

        FirmwareArtifactPayload[] artifacts =
        [
            .. plan!.Entries
                .Select(static entry => entry.Definition.SpaceId)
                .Distinct(StringComparer.Ordinal)
                .Where(spaceId =>
                    !isStandardMergeDpInput ||
                    tpImage is not null ||
                    !StringComparer.Ordinal.Equals(
                        spaceId,
                        CompositionAddressSpaceIds.TpInput))
                .Select(spaceId => new FirmwareArtifactPayload(
                    spaceId,
                    StringComparer.Ordinal.Equals(
                            spaceId,
                            CompositionAddressSpaceIds.TpInput) &&
                        tpImage is not null
                            ? tpImage
                            : image)),
        ];
        MetadataInspectionSnapshot snapshot = FirmwareMetadataInspector.Inspect(
            plan,
            artifacts);
        if (DpcmiMetadataProjector.TryProject(snapshot, out DpcmiMetadataFacts projected))
        {
            facts = projected;
        }
        else
        {
            prerequisite = snapshot.Results
                .Single(result => StringComparer.Ordinal.Equals(
                    result.PlanEntry.Definition.StructureDefinition.Definition.DefinitionId,
                    DpcmiMetadataContract.StructureId))
                .Resolution?
                .Prerequisite;
        }

        // A declared canonical DPCMI route owns both success and failure. Never
        // fall back to a second physical-offset interpretation for that route.
        return true;
    }

    private static bool DeclaresDpcmi(ResolvedMetadataPlan? plan)
    {
        return plan?.Entries.Any(entry =>
            StringComparer.Ordinal.Equals(
                entry.Definition.StructureDefinition.Definition.DefinitionId,
                DpcmiMetadataContract.StructureId)) == true;
    }
}

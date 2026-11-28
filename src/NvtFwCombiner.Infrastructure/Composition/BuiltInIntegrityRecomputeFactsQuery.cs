// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.Composition;

/// <summary>
/// Resolves the recompute facts through the Common FW selection and the postbuild plan checks of General Replace
/// planning. It adds no selection or admission rule of its own.
/// </summary>
internal sealed class BuiltInIntegrityRecomputeFactsQuery(ICompositionCapabilityExperience projection)
    : IIntegrityRecomputeFactsQuery
{
    private readonly ICompositionCapabilityExperience _projection =
        projection ?? throw new ArgumentNullException(nameof(projection));

    public IntegrityRecomputeFacts Resolve(string icId, string icNumberToken, ReadOnlyMemory<byte> baseFirmware)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(icNumberToken);
        if (baseFirmware.IsEmpty)
        {
            return IntegrityRecomputeFacts.Unknown(new CompositionIssue(
                CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                "No base flash BIN was supplied.",
                CompositionSlotIds.ReplaceBase));
        }

        FirmwareConfigMetadataSnapshot? metadata =
            BuiltInFirmwareInspection.ReadFirmwareConfigMetadata(_projection, icId, baseFirmware.Span);
        bool profileResolved = BuiltInCommonFwSelector.TrySelect(
            icId,
            metadata is not null,
            metadata?.CommonFwVersion,
            out BuiltInCommonFwSelection? selection,
            out CompositionIssue? profileIssue);
        if (!profileResolved &&
            profileIssue?.Code == CompositionPlanningIssueCodes.ReplaceCtrlRamPostbuildProfileMissing)
        {
            return IntegrityRecomputeFacts.NoRecomputeRule(profileIssue);
        }

        // Every other refusal, including the selector's "no TP flash map is provided" check that runs before the
        // profile lookup, stays Unknown: it does not prove that the IC declares no recompute route.
        LegacyCombinerPostbuildCommandPlan? plan = BuiltInGeneralAuthoringPlanner.TryResolveGeneralReplacePostbuildPlan(
            icId,
            IcNumberSelection.FromToken(icNumberToken),
            baseFirmware.Length,
            profileResolved,
            selection?.PostbuildProfile,
            profileIssue,
            out CompositionIssue? planIssue);
        return plan is null
            ? IntegrityRecomputeFacts.Unknown(planIssue)
            : IntegrityRecomputeFacts.FromPostbuild(plan, baseFirmware.Length);
    }
}

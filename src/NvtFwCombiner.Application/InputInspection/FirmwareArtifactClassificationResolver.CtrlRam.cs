using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.InputInspection;

internal sealed partial class FirmwareArtifactClassificationResolver
{
    internal bool IsCurrent(CtrlRamBaseInspection inspection)
    {
        return IsCurrent(inspection.ResolutionToken);
    }

    public bool IsCurrent(ResolutionToken resolutionToken)
    {
        return _catalog.TryGetCurrentSnapshot()?.ResolutionToken == resolutionToken;
    }

    internal CtrlRamBaseInspection ResolveCtrlRamBase(string icId, ResolvedCapability? exactCapability,
        ReadOnlyMemory<byte> candidate, CtrlRamAuthoringDraftState? draft, ICtrlRamAuthoringAdapter adapter)
    {
        CanonicalCapabilityCatalogSnapshot? publication = _catalog.TryGetCurrentSnapshot();
        FileStamp referenceStamp = FileStamp.FromBytes(candidate.Span);
        string ic = IcIdentifier.Normalize(icId);
        if (publication is null || (exactCapability is not null && !IsCurrentCapability(publication, ic, exactCapability)))
        {
            return new(CtrlRamBaseKind.Unknown, draft, [],
                [new(AuthoringSessionIssueCodes.StaleInspection, "Reference classification requires the current catalog publication.")],
                publication?.ResolutionToken ?? default, referenceStamp);
        }

        if (ic == "NT51929" && _compiler.TryCompileAbMergeCapability(ic, null, ["dp-ab-input"],
                out CompiledComposition? layout, out ResolvedCapability? layoutCapability, out _) &&
            layoutCapability is not null && IsCurrentCapability(publication, ic, layoutCapability) &&
            candidate.Length == layout!.V2Details.Provenance.ResolvedMap.CapacityBytes &&
            _compiler.TryCompileStandardMerge(ic, null, out CompiledComposition? standard,
                out ResolvedCapability? standardCapability, out _) &&
            standardCapability is not null && IsCurrentCapability(publication, ic, standardCapability) &&
            ReferenceEquals(standardCapability.CompiledComposition, standard) &&
            standardCapability.MetadataPlan.ResolutionToken == publication.ResolutionToken)
        {
            FirmwareRegion[] banks = [.. layout.V2Details.Provenance.ResolvedMap.ImageMap.Regions
                .Where(static region => region.RegionId is "a-bank" or "b-bank").OrderBy(static region => region.Range.Start)];
            if (banks.Length == 2 && banks.All(bank => bank.Range.EndExclusive <= candidate.Length))
            {
                int plausibleBanks = banks.Count(bank => CompiledFirmwareArtifactClassifier.Classify(standard,
                    candidate.Span.Slice(checked((int)bank.Range.Start), checked((int)bank.Range.Length))).Kind == CompiledFirmwareArtifactKind.FlashCode);
                var facts = new List<CtrlRamBaseBankInspection>();
                var issues = new List<CompositionIssue>();
                foreach (FirmwareRegion bank in banks)
                {
                    ReadOnlyMemory<byte> bytes = candidate.Slice(checked((int)bank.Range.Start), checked((int)bank.Range.Length));
                    bool readable = FirmwareConfigMetadataReader.TryReadBackup(bytes.Span, out FirmwareConfigMetadata config, out int markers);
                    bool valid = readable && config.IsFirmwareVersionBarValid && config.ChipNumber > 0;
                    CompositionIssue[] bankIssues = valid ? [] :
                        [!readable
                            ? new(markers > 1 ? "input.bank-reference.metadata-ambiguous" : "input.bank-reference.metadata-unreadable",
                                $"{bank.RegionId}: FWConfig/IC count unreadable; expected one valid Backup, marker count={markers}.", CompositionSlotIds.ReplaceBase)
                            : !config.IsFirmwareVersionBarValid
                                ? new("input.bank-reference.version-bar", $"{bank.RegionId}: FWConfig version/bar mismatch.", CompositionSlotIds.ReplaceBase)
                                : new("input.bank-reference.count-zero", $"{bank.RegionId}: IC count Read 0; a positive count is required.", CompositionSlotIds.ReplaceBase)];
                    FirmwareConfigMetadataSnapshot? snapshot = valid ? new(config.StructureStart, config.CommonFwVersion,
                        config.FirmwareVersion, config.FirmwareVersionBar, config.IsFirmwareVersionBarValid, config.FirmwareSubVersion,
                        config.ChipNumber, config.ProjectId, null, config.Hardware) : null;
                    facts.Add(new(bank.RegionId, bank.Range, snapshot,
                        valid ? new(CompiledInputVersionKind.TpReferenceFirmwareConfig, config.FirmwareVersion, config.FirmwareSubVersion) : null,
                        CompiledInputArtifactObservationService.DecodeDpRegion(layout,
                            bank.RegionId == "a-bank" ? CompiledInputVersionKind.DpA : CompiledInputVersionKind.DpB,
                            bank.RegionId == "a-bank" ? "a-cmi-dp-version" : "b-cmi-dp-version", candidate),
                        eventBufferFormatVersion: valid
                            ? ReadBankEventBufferFormat(standard, standardCapability, bytes, config)
                            : null, bankIssues));
                    issues.AddRange(bankIssues);
                }
                if (facts.All(static bank => bank.FirmwareConfig is not null) &&
                    facts[0].FirmwareConfig!.ChipNumber != facts[1].FirmwareConfig!.ChipNumber)
                {
                    issues.Add(new("input.bank-reference.count", $"AB IC count mismatch: a-bank Read {facts[0].FirmwareConfig!.ChipNumber}, b-bank Read {facts[1].FirmwareConfig!.ChipNumber}.", CompositionSlotIds.ReplaceBase));
                }
                IReadOnlyList<CompositionIssue> structureIssues = adapter.ValidateAbReference(layout, candidate);
                // A verified canonical header/native structure plus both valid Backups remains AB evidence
                // even if corrupted DP contents no longer satisfy the Standard Flash plausibility signal.
                if (plausibleBanks > 0 || (issues.Count == 0 && structureIssues.Count == 0))
                {
                    issues.AddRange(structureIssues);
                    if (plausibleBanks != banks.Length)
                    {
                        issues.Add(new("input.bank-reference.content", "AB bank contents do not satisfy the declared Flash plausibility checks.", CompositionSlotIds.ReplaceBase));
                    }
                    return IsCurrentSnapshot(publication)
                        ? new(CtrlRamBaseKind.AbFlash, draft as AbCtrlRamDraftState ?? new AbCtrlRamDraftState(),
                            facts, issues, publication.ResolutionToken, referenceStamp)
                        : new(CtrlRamBaseKind.Unknown, draft, [],
                            [new(AuthoringSessionIssueCodes.StaleInspection,
                                "The catalog changed during Reference classification.")],
                            publication.ResolutionToken, referenceStamp);
                }
            }
        }

        (CompiledFirmwareArtifactClassification? classified, ResolvedCapability? exactStandard,
            IReadOnlyList<ResolvedCapability>? consensusStandards) =
            ResolveWithExactStandardCapability(ic, exactCapability, candidate.Span);
        CtrlRamBaseKind kind = classified?.Kind switch
        {
            CompiledFirmwareArtifactKind.TpFirmware => CtrlRamBaseKind.StandardTp,
            CompiledFirmwareArtifactKind.FlashCode => CtrlRamBaseKind.StandardFlash,
            CompiledFirmwareArtifactKind.Unknown or null => CtrlRamBaseKind.Unknown,
            _ => throw new InvalidOperationException("Unknown compiled firmware artifact kind."),
        };
        byte? standardEventBufferFormat = null;
        if (kind is CtrlRamBaseKind.StandardTp or CtrlRamBaseKind.StandardFlash &&
            IsCurrentSnapshot(publication) &&
            FirmwareConfigMetadataReader.TryReadBackup(candidate.Span, out FirmwareConfigMetadata standardConfig,
                out _) && standardConfig.IsFirmwareVersionBarValid)
        {
            standardEventBufferFormat = exactStandard?.MetadataPlan.ResolutionToken == publication.ResolutionToken
                ? FirmwareConfigGeneralParametersProjector.ReadEventBufferFormatVersion(
                    exactStandard.MetadataPlan, candidate, standardConfig.StructureStart)
                : consensusStandards is not null
                    ? ReadConsensusEventBufferFormat(consensusStandards, publication.ResolutionToken,
                        candidate, standardConfig.StructureStart)
                    : null;
        }
        return new(kind, kind == CtrlRamBaseKind.Unknown ? draft : draft as CtrlRamFirmwareVersionDraftState, [],
            !IsCurrentSnapshot(publication)
                ? [new(AuthoringSessionIssueCodes.StaleInspection, "The catalog changed during Reference classification.")]
                : kind == CtrlRamBaseKind.Unknown
                    ? [new("input.reference.unrecognized", "The captured Base is not an unambiguous Standard or trusted AB Reference.", CompositionSlotIds.ReplaceBase)]
                    : [],
            publication.ResolutionToken, referenceStamp, standardEventBufferFormat);
    }

    private static byte? ReadConsensusEventBufferFormat(
        IReadOnlyList<ResolvedCapability> candidates, ResolutionToken resolutionToken,
        ReadOnlyMemory<byte> candidate, long structureStart)
    {
        var observations = new List<CanonicalEventBufferFieldObservation?>(candidates.Count);
        foreach (ResolvedCapability capability in candidates)
        {
            if (capability.ResolutionToken != resolutionToken ||
                capability.MetadataPlan.ResolutionToken != resolutionToken)
            {
                return null;
            }

            observations.Add(FirmwareConfigGeneralParametersProjector.ReadEventBufferFormatObservation(
                capability.MetadataPlan, candidate, structureStart));
        }

        return SelectCommonEventBufferFormat(observations);
    }

    internal static byte? SelectCommonEventBufferFormat(
        IReadOnlyList<CanonicalEventBufferFieldObservation?> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        CanonicalEventBufferFieldObservation? first = observations.Count == 0 ? null : observations[0];
        return first is not null && observations.All(observed => observed == first)
            ? first.Value
            : null;
    }

    private static byte? ReadBankEventBufferFormat(CompiledComposition standard,
        ResolvedCapability standardCapability, ReadOnlyMemory<byte> bankBytes, FirmwareConfigMetadata validatedConfig)
    {
        MetadataPlanEntry[] matches =
        [
            .. standardCapability.MetadataPlan.Entries
                .Select(static item => item.Definition)
                .Where(entry => StringComparer.Ordinal.Equals(entry.StructureDefinition.StructureId,
                        FirmwareConfigGeneralParametersContract.StructureId) &&
                    StringComparer.Ordinal.Equals(entry.ImageMap.MapId,
                        standard.V2Details.Provenance.ResolvedMap.ImageMap.MapId) &&
                    StringComparer.Ordinal.Equals(entry.MemberId,
                        standard.V2Details.Provenance.Context.MemberId))
                .Take(2),
        ];
        if (matches.Length != 1)
        {
            return null;
        }

        MetadataPlanEntry entry = matches[0];
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap map =
            standard.V2Details.Provenance.ResolvedMap;
        return bankBytes.Length == map.CapacityBytes &&
            StringComparer.Ordinal.Equals(entry.FamilyDefinition.FamilyContentHash,
                standard.V2Details.Provenance.Context.FamilyContentHash) &&
            StringComparer.Ordinal.Equals(entry.ResolvedMap.ResolutionFingerprint,
                map.ResolutionFingerprint) &&
            StringComparer.Ordinal.Equals(entry.SpaceId, entry.StructureDefinition.ArtifactBindingId)
                ? FirmwareConfigGeneralParametersProjector.ReadEventBufferFormatVersion(
                    standardCapability.MetadataPlan, bankBytes, validatedConfig.StructureStart,
                    requireFieldTarget: false)
                : null;
    }
}

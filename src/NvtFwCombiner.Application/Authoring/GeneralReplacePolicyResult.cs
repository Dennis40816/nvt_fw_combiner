// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>Immutable policy decision; blocked decisions grant no staged-host write authority.</summary>
public sealed class GeneralReplacePolicyResult
{
    internal GeneralReplacePolicyResult(string outputAddressSpaceId, IntegrityRecomputeFacts recomputeFacts,
        IntegrityRecomputeChoice choice, IEnumerable<CompositionIssue> issues,
        IEnumerable<FirmwareAddressedRange> declaredWriteRanges,
        MemoryLayoutFactSet<IntegrityStorageHit> storageHits,
        MemoryLayoutFactSet<IntegrityWriteRangeHit> writeRangeHits)
    {
        OutputAddressSpaceId = outputAddressSpaceId;
        RecomputeFacts = recomputeFacts;
        Choice = choice;
        CompositionIssue[] snapshot = [.. issues];
        Blocks = Array.AsReadOnly<CompositionIssue>([.. snapshot.Where(static issue => issue.Severity == CompositionIssueSeverity.Error)]);
        AdvisoryIssues = Array.AsReadOnly<CompositionIssue>([.. snapshot.Where(static issue => issue.Severity == CompositionIssueSeverity.Warning)]);
        FirmwareAddressedRange[] ranges = Blocks.Count == 0 ? [.. declaredWriteRanges] : [];
        DeclaredWriteRanges = Array.AsReadOnly(ranges);
        StorageHits = storageHits;
        WriteRangeHits = writeRangeHits;
    }

    /// <summary>Canonical output space shared by all integrity locations and hit ranges.</summary>
    public string OutputAddressSpaceId { get; }
    /// <summary>Exact facts used for this decision; neither missing nor unknown becomes none.</summary>
    public IntegrityRecomputeFacts RecomputeFacts { get; }
    /// <summary>Effective choice; an IC without a rule can only write as is.</summary>
    public IntegrityRecomputeChoice Choice { get; }
    /// <summary>Output-blocking issues.</summary>
    public IReadOnlyList<CompositionIssue> Blocks { get; }
    /// <summary>Warnings passed unchanged to CompositionRunRequest.AdvisoryIssues.</summary>
    public IReadOnlyList<CompositionIssue> AdvisoryIssues { get; }
    /// <summary>Whether policy admission succeeded; this grants no runtime tool or execution readiness.</summary>
    public bool CanOutput => Blocks.Count == 0;
    /// <summary>User segments plus recompute writes when selected; empty on any block.</summary>
    public IReadOnlyList<FirmwareAddressedRange> DeclaredWriteRanges { get; }
    /// <summary>Storage hits projected by the integrity facts owner.</summary>
    public MemoryLayoutFactSet<IntegrityStorageHit> StorageHits { get; }
    /// <summary>All rewritten-range hits, including bytes outside CRC storage.</summary>
    public MemoryLayoutFactSet<IntegrityWriteRangeHit> WriteRangeHits { get; }

    /// <summary>Creates the typed confirmation details; the caller supplies its declared NFC version.</summary>
    public GeneralReplaceConfirmation CreateConfirmation(string nfcVersion)
    {
        return new(this, nfcVersion);
    }
}

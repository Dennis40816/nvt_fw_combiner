// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.MemoryLayout;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>Presentation-localized actions; Cancel has no IntegrityRecomputeChoice.</summary>
public enum GeneralReplaceConfirmationButton
{
    /// <summary>Apply the declared recompute route, then output.</summary>
    RecomputeAndOutput,
    /// <summary>Output the user segments without recompute.</summary>
    WriteAsIsAndOutput,
    /// <summary>Leave the confirmation without building.</summary>
    Cancel,
}

/// <summary>Typed explanation key; Presentation owns its wording.</summary>
public enum GeneralReplaceConfirmationReason
{
    /// <summary>Replaced content can invalidate header values and CRC.</summary>
    ReplacedContentRequiresRecompute,
    /// <summary>The IC declares no recompute route.</summary>
    NoRecomputeRule,
    /// <summary>The route cannot be resolved.</summary>
    RecomputeUnavailable,
}

/// <summary>
/// Confirmation projection of one policy result. All integrity ranges use OutputAddressSpaceId.
/// ToolBindingId identifies the declared versioned tool binding; command modes are never parsed from text.
/// </summary>
public sealed class GeneralReplaceConfirmation
{
    private readonly GeneralReplacePolicyResult _decision;

    internal GeneralReplaceConfirmation(GeneralReplacePolicyResult decision, string nfcVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nfcVersion);
        _decision = decision;
        NfcVersion = nfcVersion;
        GeneralReplaceConfirmationButton[] buttons = Availability switch
        {
            IntegrityRecomputeAvailability.Declared =>
                [GeneralReplaceConfirmationButton.RecomputeAndOutput, GeneralReplaceConfirmationButton.WriteAsIsAndOutput,
                    GeneralReplaceConfirmationButton.Cancel],
            IntegrityRecomputeAvailability.NoRecomputeRule =>
                [GeneralReplaceConfirmationButton.WriteAsIsAndOutput, GeneralReplaceConfirmationButton.Cancel],
            IntegrityRecomputeAvailability.Unknown => [GeneralReplaceConfirmationButton.Cancel],
            _ => throw new ArgumentOutOfRangeException(nameof(decision)),
        };
        Buttons = Array.AsReadOnly(buttons);
    }

    /// <summary>Declared, missing or unresolved recompute route.</summary>
    public IntegrityRecomputeAvailability Availability => _decision.RecomputeFacts.Availability;
    /// <summary>Explanation key, independent of localized text.</summary>
    public GeneralReplaceConfirmationReason Reason => Availability switch
    {
        IntegrityRecomputeAvailability.Declared => GeneralReplaceConfirmationReason.ReplacedContentRequiresRecompute,
        IntegrityRecomputeAvailability.NoRecomputeRule => GeneralReplaceConfirmationReason.NoRecomputeRule,
        IntegrityRecomputeAvailability.Unknown => GeneralReplaceConfirmationReason.RecomputeUnavailable,
        _ => throw new InvalidOperationException("Unknown recompute availability."),
    };
    /// <summary>Version supplied by the host's existing version owner.</summary>
    public string NfcVersion { get; }
    /// <summary>Declared tool binding, including its version identity; unavailable when the route is unresolved.</summary>
    public string? ToolBindingId => _decision.RecomputeFacts.ToolBindingId;
    /// <summary>Ordered command family, mode and CRC arguments from the postbuild owner.</summary>
    public IReadOnlyList<IntegrityRecomputeCommand> Commands => _decision.RecomputeFacts.Commands;
    /// <summary>Canonical output address space for all integrity ranges.</summary>
    public string OutputAddressSpaceId => _decision.OutputAddressSpaceId;
    /// <summary>Declared storage locations, preserving unavailable state.</summary>
    public MemoryLayoutFactSet<IntegrityStorageLocation> StorageLocations => _decision.RecomputeFacts.StorageLocations;
    /// <summary>Every range the declared route may rewrite.</summary>
    public MemoryLayoutFactSet<IntegrityRecomputeWriteRange> WriteRanges => _decision.RecomputeFacts.WriteRanges;
    /// <summary>Coverage; NotDeclared remains distinct from a known empty set.</summary>
    public MemoryLayoutFactSet<IntegrityCoverage> Coverage => _decision.RecomputeFacts.Coverage;
    /// <summary>User segments touching stored integrity values.</summary>
    public MemoryLayoutFactSet<IntegrityStorageHit> StorageHits => _decision.StorageHits;
    /// <summary>User segments touching any rewritten bytes.</summary>
    public MemoryLayoutFactSet<IntegrityWriteRangeHit> WriteRangeHits => _decision.WriteRangeHits;
    /// <summary>Available actions in display order.</summary>
    public IReadOnlyList<GeneralReplaceConfirmationButton> Buttons { get; }
    /// <summary>Recompute on a new declared rule; restored WriteAsIs remains selected. Unknown selects Cancel.</summary>
    public GeneralReplaceConfirmationButton DefaultButton => Availability == IntegrityRecomputeAvailability.Unknown
        ? GeneralReplaceConfirmationButton.Cancel
        : _decision.Choice == IntegrityRecomputeChoice.Recompute
            ? GeneralReplaceConfirmationButton.RecomputeAndOutput
            : GeneralReplaceConfirmationButton.WriteAsIsAndOutput;
}

// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>
/// Single owner of the output-file-name marker for executable candidates that still wait for a
/// Golden result. The decision uses only declared compile facts (promotion stage, blocker kinds
/// and the typed renderer); it never reads a file name, a PID or a hash.
/// </summary>
internal static class UnverifiedCandidateOutputName
{
    /// <summary>Text inserted before the file extension of an unverified candidate output.</summary>
    internal const string Marker = "_UNVERIFIED";

    /// <summary>
    /// Whether the automatic output name of this composition carries <see cref="Marker"/>.
    /// Only the typed normal FlashCode and TP-firmware renderers are covered. AB Code, static and
    /// logical-output candidates keep their names; static names are declared by their profile.
    /// </summary>
    internal static bool Applies(CompiledComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        V2CompiledCompositionDetails details = composition.V2Details;
        return composition.Eligibility == CompiledCompositionEligibility.V2PlanCompiled &&
            details.Provenance.Promotion.Stage == CompiledProfilePromotionStage.ExecutableCandidate &&
            details.OutputNamingRequirement.RendererKind is
                CompiledOutputNameRendererKind.NormalFlashCodeV1 or
                CompiledOutputNameRendererKind.TpFirmwareV1 &&
            details.Provenance.Promotion.Blockers.Any(static blocker =>
                blocker.Kind == CompiledProfilePromotionBlockerKind.Golden);
    }

    /// <summary>Inserts <see cref="Marker"/> before the final .bin extension; repeated calls change nothing.</summary>
    internal static string Apply(string automaticFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automaticFileName);
        const string Extension = ".bin";
        bool hasExtension = automaticFileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);
        string stem = hasExtension ? automaticFileName[..^Extension.Length] : automaticFileName;
        return stem.EndsWith(Marker, StringComparison.Ordinal)
            ? automaticFileName
            : stem + Marker + automaticFileName[stem.Length..];
    }
}

using System.Security.Cryptography;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Prepares and publishes the optional A-bank FlashCode declared by a compiled AB profile.</summary>
public static class AbMergeAFlashCodeExportService
{
    internal const string AFlashCodeDeliveryKind = "ab-a-flashcode";

    private static readonly string[] s_aBankRegionIds =
    [
        "dp-a-before-cmi",
        "a-cmi-dp-version",
        "dp-a-after-cmi",
        "tpa-code",
    ];

    /// <summary>Returns the optional A-bank delivery only when the selected compiled profile declares one contiguous A image.</summary>
    public static ValueTask<WorkbenchAbAFlashCodeDeliveryPlan?> TryCreatePlanAsync(
        CompiledComposition composition,
        IReadOnlyDictionary<string, string> slotPaths,
        CompositionOutputNamePreview outputNamePreview,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(slotPaths);
        ArgumentNullException.ThrowIfNull(outputNamePreview);
        cancellationToken.ThrowIfCancellationRequested();

        return TryResolveAFlashCodeRange(composition, out ByteRange range) &&
               TryRenderSuggestedFileName(outputNamePreview.OutputNaming, out string? fileName) &&
               fileName is not null
            ? ValueTask.FromResult<WorkbenchAbAFlashCodeDeliveryPlan?>(new WorkbenchAbAFlashCodeDeliveryPlan(
                composition.ProfileId,
                // Keep every selected source path.  A case-insensitive de-duplication would
                // discard a distinct input on a case-sensitive filesystem, leaving that path
                // outside the delivery-time overwrite guard.
                [.. slotPaths.Values.Select(Path.GetFullPath)],
                range,
                fileName))
            : ValueTask.FromResult<WorkbenchAbAFlashCodeDeliveryPlan?>(null);
    }

    /// <summary>Validates both chosen paths before the primary AB output is committed.</summary>
    public static void ValidateOutputPath(
        WorkbenchAbAFlashCodeDeliveryPlan plan,
        string primaryOutputPath,
        string aFlashCodeOutputPath)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryOutputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(aFlashCodeOutputPath);

        string fullPrimaryOutputPath = Path.GetFullPath(primaryOutputPath);
        string fullAFlashCodeOutputPath = Path.GetFullPath(aFlashCodeOutputPath);
        ValidateConcreteOutputPath(fullAFlashCodeOutputPath, nameof(aFlashCodeOutputPath));
        List<ProtectedPathGuard.ProtectedPath> protectedPaths =
        [
            .. plan.InputPaths.Select(path => new ProtectedPathGuard.ProtectedPath(path, "AB input artifact")),
            new ProtectedPathGuard.ProtectedPath(fullPrimaryOutputPath, "AB FlashCode output"),
        ];
        ProtectedPathGuard.EnsureDoesNotAlias(
            fullAFlashCodeOutputPath,
            "A FlashCode output path",
            protectedPaths,
            nameof(aFlashCodeOutputPath));
    }

    /// <summary>Atomically writes the immutable profile-declared A-bank slice after the primary AB output commits.</summary>
    public static async ValueTask<WorkbenchDeliveryArtifact> ExportAsync(
        WorkbenchAbAFlashCodeDeliveryPlan plan,
        ReadOnlyMemory<byte> primaryOutputBytes,
        string primaryOutputPath,
        string aFlashCodeOutputPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryOutputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(aFlashCodeOutputPath);
        ValidateOutputPath(plan, primaryOutputPath, aFlashCodeOutputPath);

        string fullOutputPath = Path.GetFullPath(aFlashCodeOutputPath);
        string outputDirectory = Path.GetDirectoryName(fullOutputPath)!;
        string outputFileName = Path.GetFileName(fullOutputPath);
        ReadOnlyMemory<byte> aBank = SliceDeclaredRange(primaryOutputBytes, plan.SourceRange);
        string sha256 = Convert.ToHexString(SHA256.HashData(aBank.Span)).ToLowerInvariant();
        var writer = new AtomicFileCompositionOutputWriter(outputDirectory, overwrite: true);
        string committedPath = await writer.CommitAsync(outputFileName, aBank, cancellationToken).ConfigureAwait(false);
        return new WorkbenchDeliveryArtifact(
            AFlashCodeDeliveryKind,
            committedPath,
            outputFileName,
            aBank.Length,
            plan.SourceRange,
            sha256);
    }

    /// <summary>Builds the report-safe delivery summary from the exact immutable A-bank bytes.</summary>
    public static DeliveryArtifactSummary CreateReportSummary(
        WorkbenchAbAFlashCodeDeliveryPlan plan,
        ReadOnlyMemory<byte> primaryOutputBytes,
        string aFlashCodeOutputPath,
        bool committed)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(aFlashCodeOutputPath);
        ReadOnlyMemory<byte> aBank = SliceDeclaredRange(primaryOutputBytes, plan.SourceRange);
        return new DeliveryArtifactSummary(
            AFlashCodeDeliveryKind,
            Path.GetFileName(Path.GetFullPath(aFlashCodeOutputPath)),
            aBank.Length,
            Convert.ToHexString(SHA256.HashData(aBank.Span)).ToLowerInvariant(),
            committed,
            plan.SourceRange);
    }

    internal static bool TryResolveAFlashCodeRange(CompiledComposition composition, out ByteRange range)
    {
        range = default;
        IReadOnlyList<FirmwareRegion> regions = composition.V2Details.Provenance.ResolvedMap.ImageMap.Regions;

        FirmwareRegion?[] declaredARegions = [.. s_aBankRegionIds
            .Select(regionId => regions.SingleOrDefault(region => StringComparer.Ordinal.Equals(region.RegionId, regionId)))];
        if (declaredARegions.Any(static region => region is null))
        {
            return false;
        }

        FirmwareRegion[] aBankRegions = [.. declaredARegions.Select(static region => region!)];
        if (aBankRegions[0].Range.Start != 0 || !RangesAreContiguous(aBankRegions))
        {
            return false;
        }

        long endExclusive = aBankRegions[^1].Range.EndExclusive;
        range = new ByteRange(0, endExclusive);
        return range.EndExclusive <= composition.Plan.OutputInitialization.Capacity;
    }

    internal static bool TryRenderSuggestedFileName(OutputNamingSummary? naming, out string? fileName)
    {
        fileName = null;
        if (naming is null || !StringComparer.Ordinal.Equals(naming.RendererKind, "ab-code-v1"))
        {
            return false;
        }

        var tokens = naming.Tokens.ToDictionary(
            static token => token.TokenId,
            static token => token.Value,
            StringComparer.Ordinal);
        if (!tokens.TryGetValue("ic", out string? ic) ||
            !tokens.TryGetValue("dp-a", out string? dpA) ||
            !tokens.TryGetValue("tp-a", out string? tpA) ||
            !tokens.TryGetValue("date", out string? date) ||
            string.IsNullOrWhiteSpace(ic) ||
            string.IsNullOrWhiteSpace(dpA) ||
            string.IsNullOrWhiteSpace(tpA) ||
            string.IsNullOrWhiteSpace(date))
        {
            return false;
        }

        fileName = $"NT{ic}_FlashCode_{dpA}{tpA}_{date}.bin";
        return Path.GetFileName(fileName) == fileName;
    }

    private static void ValidateConcreteOutputPath(string fullOutputPath, string parameterName)
    {
        string? outputDirectory = Path.GetDirectoryName(fullOutputPath);
        string outputFileName = Path.GetFileName(fullOutputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory) || string.IsNullOrWhiteSpace(outputFileName))
        {
            throw new ArgumentException("A FlashCode export requires one concrete output file path.", parameterName);
        }
    }

    private static bool RangesAreContiguous(FirmwareRegion[] regions)
    {
        for (int index = 1; index < regions.Length; index++)
        {
            if (regions[index - 1].Range.EndExclusive != regions[index].Range.Start)
            {
                return false;
            }
        }

        return true;
    }

    private static ReadOnlyMemory<byte> SliceDeclaredRange(ReadOnlyMemory<byte> output, ByteRange range)
    {
        return range.Start < 0 || range.EndExclusive > output.Length || range.Start > int.MaxValue || range.Length > int.MaxValue
            ? throw new InvalidOperationException("The compiled A FlashCode export range is outside the AB output image.")
            : output.Slice(checked((int)range.Start), checked((int)range.Length));
    }
}

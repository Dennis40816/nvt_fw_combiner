using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class CompositionExecutionAdapter
{
    private const string RunIdPrefix = "ui-merge-ab";

    /// <summary>Runs AB Merge preview or build through the shared Application composition service.</summary>
    public static ValueTask<WorkbenchRunResult> RunAbMergeAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        TopologySelection? abMergeTopologySelection = null,
        string? aFlashCodeOutputPath = null,
        bool outputPathUsesAutomaticName = false,
        bool aFlashCodeOutputPathUsesAutomaticName = false)
    {
        return RunAbMergeCoreAsync(
            icId,
            slotPaths,
            build,
            outputPath,
            previewOutputFileName: null,
            progress: null,
            abMergeTopologySelection: abMergeTopologySelection,
            aFlashCodeOutputPath: aFlashCodeOutputPath,
            automaticOutputDirectory: null,
            additionalOutputProtectedPaths: null,
            outputPathUsesAutomaticName: outputPathUsesAutomaticName,
            aFlashCodeOutputPathUsesAutomaticName: aFlashCodeOutputPathUsesAutomaticName,
            cancellationToken: cancellationToken,
            acceptedCapability: null);
    }

    /// <summary>Runs AB Merge for the focused CLI adapter while preserving explicit output policy.</summary>
    internal static ValueTask<WorkbenchRunResult> RunAbMergeForCliAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        string? previewOutputFileName,
        TopologySelection? abMergeTopologySelection,
        string? automaticOutputDirectory,
        string? reportPath,
        CancellationToken cancellationToken)
    {
        return RunAbMergeCoreAsync(
            icId,
            slotPaths,
            build,
            outputPath,
            build ? null : previewOutputFileName,
            progress: null,
            abMergeTopologySelection: abMergeTopologySelection,
            aFlashCodeOutputPath: null,
            automaticOutputDirectory: automaticOutputDirectory,
            additionalOutputProtectedPaths: string.IsNullOrWhiteSpace(reportPath)
                ? null
                : [new ProtectedPathGuard.ProtectedPath(reportPath, "CLI report")],
            outputPathUsesAutomaticName: false,
            aFlashCodeOutputPathUsesAutomaticName: false,
            cancellationToken: cancellationToken,
            acceptedCapability: null);
    }

    /// <summary>Runs AB Merge and publishes bounded Application-owned lifecycle phases.</summary>
    public static ValueTask<WorkbenchRunResult> RunAbMergeWithProgressAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null,
        TopologySelection? abMergeTopologySelection = null,
        string? aFlashCodeOutputPath = null,
        bool outputPathUsesAutomaticName = false,
        bool aFlashCodeOutputPathUsesAutomaticName = false)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return RunAbMergeCoreAsync(
            icId,
            slotPaths,
            build,
            outputPath,
            previewOutputFileName: null,
            progress: progress,
            abMergeTopologySelection: abMergeTopologySelection,
            aFlashCodeOutputPath: aFlashCodeOutputPath,
            automaticOutputDirectory: null,
            additionalOutputProtectedPaths: null,
            outputPathUsesAutomaticName: outputPathUsesAutomaticName,
            aFlashCodeOutputPathUsesAutomaticName: aFlashCodeOutputPathUsesAutomaticName,
            cancellationToken: cancellationToken,
            acceptedCapability: null);
    }

    /// <summary>Runs AB Merge from one exact accepted desktop session.</summary>
    public static ValueTask<WorkbenchRunResult> RunAbMergeAcceptedSessionWithProgressAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null,
        string? abMergeTopologyToken = null,
        string? aFlashCodeOutputPath = null,
        bool outputPathUsesAutomaticName = false,
        bool aFlashCodeOutputPathUsesAutomaticName = false)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ResolvedCapability capability = AcceptedAuthoringSessionBinding.RequireCapability(
            acceptedSession,
            IcWorkflowIds.AbMerge,
            icId,
            AuthoringDerivedResultKind.Inspection);
        return RunAbMergeCoreAsync(
            icId, slotPaths, build, outputPath, previewOutputFileName: null, progress,
            CanonicalCapabilityResolution.ResolveAbMergeTopologySelection(
                icId,
                abMergeTopologyToken),
            aFlashCodeOutputPath,
            automaticOutputDirectory: null, additionalOutputProtectedPaths: null,
            outputPathUsesAutomaticName, aFlashCodeOutputPathUsesAutomaticName,
            capability, cancellationToken, acceptedSession);
    }

    /// <summary>Runs AB Merge with a Bootstrap-owned symbolic topology token.</summary>
    public static ValueTask<WorkbenchRunResult> RunAbMergeWithProgressAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null,
        string? abMergeTopologyToken = null,
        string? aFlashCodeOutputPath = null,
        bool outputPathUsesAutomaticName = false,
        bool aFlashCodeOutputPathUsesAutomaticName = false)
    {
        return RunAbMergeWithProgressAsync(
            icId: icId,
            slotPaths: slotPaths,
            build: build,
            progress: progress,
            cancellationToken: cancellationToken,
            outputPath: outputPath,
            abMergeTopologySelection:
                CanonicalCapabilityResolution.ResolveAbMergeTopologySelection(
                    icId,
                    abMergeTopologyToken),
            aFlashCodeOutputPath: aFlashCodeOutputPath,
            outputPathUsesAutomaticName: outputPathUsesAutomaticName,
            aFlashCodeOutputPathUsesAutomaticName: aFlashCodeOutputPathUsesAutomaticName);
    }

    private static async ValueTask<WorkbenchRunResult> RunAbMergeCoreAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        string? previewOutputFileName,
        CompositionRunProgressFeed? progress,
        TopologySelection? abMergeTopologySelection,
        string? aFlashCodeOutputPath,
        string? automaticOutputDirectory,
        IReadOnlyList<ProtectedPathGuard.ProtectedPath>? additionalOutputProtectedPaths,
        bool outputPathUsesAutomaticName,
        bool aFlashCodeOutputPathUsesAutomaticName,
        ResolvedCapability? acceptedCapability,
        CancellationToken cancellationToken,
        ActiveSessionSnapshot? acceptedSession = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(slotPaths);
        string normalizedIcId = IcIdentifier.Normalize(icId);
        CompiledComposition composition = acceptedCapability?.CompiledComposition ??
            WorkbenchAbMergeInputProjection.ResolveComposition(
                normalizedIcId,
                abMergeTopologySelection);

        InputArtifactBinding[] bindings = WorkbenchAbMergeInputProjection.CreateInputBindings(
            composition, slotPaths, acceptedSession);
        string firstInputPath = bindings.Single(static binding =>
            binding.AddressSpaceId == CompositionAddressSpaceIds.DpAbInput).ArtifactId;

        WorkbenchAbAFlashCodeDeliveryPlan? aFlashCodePlan = null;
        string? resolvedAFlashCodeOutputPath = null;
        Action<string, OutputNamingSummary?>? additionalOutputPreflight = null;
        if (!string.IsNullOrWhiteSpace(aFlashCodeOutputPath))
        {
            if (!build || string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException(
                    "A FlashCode delivery requires one explicit AB Build output path.",
                    nameof(aFlashCodeOutputPath));
            }

            CompositionOutputNamePreview preview = await
                CompositionOutputNaming.ResolveAutomaticOutputNameAsync(
                    RunIdPrefix,
                    composition,
                    bindings,
                    abMergeTopologySelection,
                    cancellationToken)
                .ConfigureAwait(false);
            WorkbenchAbAFlashCodeDeliveryPlan plan = !preview.CanUseAutomaticName
                ? throw new InvalidOperationException(FormatIssues(preview.Issues))
                : await AbMergeAFlashCodeExportService.TryCreatePlanAsync(
                    composition,
                    slotPaths,
                    preview,
                    cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException(
                        "The selected AB profile does not declare an optional A FlashCode delivery.");
            aFlashCodePlan = plan;
            string selectedAFlashCodeOutputPath = Path.GetFullPath(aFlashCodeOutputPath);
            additionalOutputPreflight = (resolvedPrimaryOutputPath, outputNaming) =>
            {
                string candidateAFlashCodeOutputPath = ResolveAFlashCodeOutputPath(
                    selectedAFlashCodeOutputPath,
                    aFlashCodeOutputPathUsesAutomaticName,
                    outputNaming);
                AbMergeAFlashCodeExportService.ValidateOutputPath(
                    plan,
                    resolvedPrimaryOutputPath,
                    candidateAFlashCodeOutputPath);
                resolvedAFlashCodeOutputPath = candidateAFlashCodeOutputPath;
            };
        }

        CompositionRunResult result = await RunCompiledCompositionResultAsync(
            RunIdPrefix,
            composition,
            bindings,
            firstInputPath,
            build,
            outputPath,
            externalProcessor: ExternalProcessorFactory.GetOrCreateOrNull(),
            icNumberSelection: null,
            cancellationToken: cancellationToken,
            progress: progress,
            previewOutputFileName: previewOutputFileName,
            abMergeTopologySelection: abMergeTopologySelection,
            automaticOutputDirectory: automaticOutputDirectory,
            additionalOutputProtectedPaths: additionalOutputProtectedPaths,
            outputPathUsesAutomaticName: outputPathUsesAutomaticName,
            additionalOutputPreflight: additionalOutputPreflight,
            resolvedCapability: acceptedCapability).ConfigureAwait(false);
        if (aFlashCodePlan is null || !build || result.Status != CompositionExecutionStatus.Succeeded ||
            string.IsNullOrWhiteSpace(result.CommittedOutputId) ||
            string.IsNullOrWhiteSpace(resolvedAFlashCodeOutputPath))
        {
            return ToWorkbenchRunResult(result);
        }

        try
        {
            WorkbenchDeliveryArtifact delivery = await AbMergeAFlashCodeExportService.ExportAsync(
                aFlashCodePlan,
                result.OutputBytes,
                result.CommittedOutputId,
                resolvedAFlashCodeOutputPath,
                cancellationToken).ConfigureAwait(false);
            DeliveryArtifactSummary reportDelivery = AbMergeAFlashCodeExportService.CreateReportSummary(
                aFlashCodePlan,
                result.OutputBytes,
                resolvedAFlashCodeOutputPath,
                committed: true);
            return ToWorkbenchRunResult(
                result,
                [reportDelivery],
                deliveredArtifacts: [delivery]);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            DeliveryArtifactSummary reportDelivery = AbMergeAFlashCodeExportService.CreateReportSummary(
                aFlashCodePlan,
                result.OutputBytes,
                resolvedAFlashCodeOutputPath,
                committed: false);
            var issue = new CompositionIssue(
                "delivery.ab-a-flashcode.failed",
                $"The primary AB FlashCode committed, but the requested A FlashCode could not be delivered: {exception.Message}",
                AbMergeAFlashCodeExportService.AFlashCodeDeliveryKind);
            return ToWorkbenchRunResult(
                result,
                [reportDelivery],
                [issue],
                isDeliveryComplete: false,
                deliveryFailureMessage: issue.Message);
        }
    }

    private static string ResolveAFlashCodeOutputPath(
        string selectedOutputPath,
        bool usesAutomaticName,
        OutputNamingSummary? outputNaming)
    {
        if (!usesAutomaticName)
        {
            return selectedOutputPath;
        }

        if (!AbMergeAFlashCodeExportService.TryRenderSuggestedFileName(outputNaming, out string? fileName) ||
            string.IsNullOrWhiteSpace(fileName))
        {
            throw new InvalidOperationException(
                "The execution-derived AB naming provenance cannot render the requested A FlashCode output name.");
        }

        string outputDirectory = Path.GetDirectoryName(selectedOutputPath)
            ?? throw new ArgumentException(
                "An automatic A FlashCode output requires a concrete destination directory.",
                nameof(selectedOutputPath));
        return Path.Combine(outputDirectory, fileName);
    }

}

using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Focused workbench adapter for the owner-approved AB Merge pilot.</summary>
public static class AbMergeWorkbenchCompositionService
{
    private const string RunIdPrefix = "ui-merge-ab";

    /// <summary>Returns whether the selected IC owns an executable AB Merge profile.</summary>
    public static bool IsAbMergeSupported(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return BuiltInV2RegistrationRegistry.AbMergeByIc.TryGetValue(
            IcSupportCatalog.NormalizeIcId(icId),
            out BuiltInV2Registration? registration) &&
            registration.CreateProfileSummary().CompileSucceeded;
    }

    internal static bool TryCompileAbMerge(
        string icId,
        TopologySelection? requestedTopology,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        composition = null;
        issues = [];
        if (!BuiltInV2RegistrationRegistry.AbMergeByIc.TryGetValue(
                IcSupportCatalog.NormalizeIcId(icId),
                out BuiltInV2Registration? registration))
        {
            return false;
        }

        registration.TryCompile(inputLength: null, requestedTopology, out composition, out issues);
        return composition is not null;
    }

    internal static bool TryCompileAbMerge(
        string icId,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCompileAbMerge(icId, requestedTopology: null, out composition, out issues);
    }

    /// <summary>Returns the two symbolic topology choices only for AB profiles with topology-specific maps.</summary>
    public static IReadOnlyList<WorkbenchAbMergeTopologyChoice> GetTopologyChoices(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return BuiltInV2RegistrationRegistry.AbMergeByIc.TryGetValue(
                   IcSupportCatalog.NormalizeIcId(icId),
                   out BuiltInV2Registration? registration) &&
               registration.HasMultipleMapCapacities
            ?
            [
                new WorkbenchAbMergeTopologyChoice("single", "1 IC"),
                new WorkbenchAbMergeTopologyChoice("cascade", "Cascade"),
            ]
            : [];
    }

    /// <summary>Converts a profile-owned symbolic AB topology token into its typed selection.</summary>
    /// <param name="token">The profile-owned topology token.</param>
    /// <param name="topology">The resulting selection when the token is recognized.</param>
    /// <returns><see langword="true"/> when <paramref name="token"/> is recognized.</returns>
    public static bool TryCreateTopologySelection(
        string token,
        [NotNullWhen(true)] out TopologySelection? topology)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        topology = token.Trim().ToLowerInvariant() switch
        {
            "single" => new TopologySelection(1, "1 IC", TopologySelectionSource.Requested, "ab-topology"),
            "cascade" => new TopologySelection(2, "Cascade", TopologySelectionSource.Requested, "ab-topology"),
            _ => null,
        };
        return topology is not null;
    }

    /// <summary>Resolves one Bootstrap-owned symbolic topology token or fails before profile compilation.</summary>
    internal static TopologySelection? ResolveTopologySelection(string? token)
    {
        return string.IsNullOrWhiteSpace(token)
            ? null
            : TryCreateTopologySelection(token, out TopologySelection? topology)
            ? topology
            : throw new ArgumentException("The AB Merge topology token is not profile-owned.", nameof(token));
    }

    /// <summary>Runs AB Merge preview or build through the shared Application composition service.</summary>
    public static ValueTask<WorkbenchRunResult> RunAbMergeAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        TopologySelection? abMergeTopologySelection = null,
        string? aFlashCodeOutputPath = null,
        bool outputPathUsesAutomaticName = false)
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
            cancellationToken: cancellationToken);
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
            cancellationToken: cancellationToken);
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
        bool outputPathUsesAutomaticName = false)
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
            cancellationToken: cancellationToken);
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
        bool outputPathUsesAutomaticName = false)
    {
        return RunAbMergeWithProgressAsync(
            icId: icId,
            slotPaths: slotPaths,
            build: build,
            progress: progress,
            cancellationToken: cancellationToken,
            outputPath: outputPath,
            abMergeTopologySelection: ResolveTopologySelection(abMergeTopologyToken),
            aFlashCodeOutputPath: aFlashCodeOutputPath,
            outputPathUsesAutomaticName: outputPathUsesAutomaticName);
    }

    /// <summary>Resolves the compiled AB automatic filename without executing or publishing firmware output.</summary>
    public static async ValueTask<string> ResolveAutomaticOutputFileNameAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        CancellationToken cancellationToken,
        string? abMergeTopologyToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(slotPaths);
        string normalizedIcId = IcSupportCatalog.NormalizeIcId(icId);
        TopologySelection? topology = ResolveTopologySelection(abMergeTopologyToken);
        CompiledComposition composition = CompileExecutableAbMerge(normalizedIcId, topology);

        InputArtifactBinding[] bindings = CreateInputBindings(composition, slotPaths);
        CompositionOutputNamePreview preview = await WorkbenchCompositionService
            .ResolveAutomaticOutputNameAsync(
                RunIdPrefix,
                composition,
                bindings,
                topology,
                cancellationToken)
            .ConfigureAwait(false);
        return !preview.CanUseAutomaticName
            ? throw new InvalidOperationException(WorkbenchCompositionService.FormatIssues(preview.Issues))
            : preview.FileName;
    }

    /// <summary>Returns the profile-declared optional A-bank delivery before any AB output is committed.</summary>
    public static async ValueTask<WorkbenchAbAFlashCodeDeliveryPlan?> TryCreateAFlashCodeDeliveryPlanAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        CancellationToken cancellationToken,
        string? abMergeTopologyToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(slotPaths);
        string normalizedIcId = IcSupportCatalog.NormalizeIcId(icId);
        TopologySelection? topology = ResolveTopologySelection(abMergeTopologyToken);
        CompiledComposition composition = CompileExecutableAbMerge(normalizedIcId, topology);
        InputArtifactBinding[] bindings = CreateInputBindings(composition, slotPaths);
        CompositionOutputNamePreview preview = await WorkbenchCompositionService
            .ResolveAutomaticOutputNameAsync(
                RunIdPrefix,
                composition,
                bindings,
                topology,
                cancellationToken)
            .ConfigureAwait(false);
        return !preview.CanUseAutomaticName
            ? throw new InvalidOperationException(WorkbenchCompositionService.FormatIssues(preview.Issues))
            : await AbMergeAFlashCodeExportService.TryCreatePlanAsync(
                composition,
                slotPaths,
                preview,
                cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(slotPaths);
        string normalizedIcId = IcSupportCatalog.NormalizeIcId(icId);
        CompiledComposition composition = CompileExecutableAbMerge(normalizedIcId, abMergeTopologySelection);

        InputArtifactBinding[] bindings = CreateInputBindings(composition, slotPaths);
        string firstInputPath = bindings.Single(static binding =>
            binding.AddressSpaceId == CompositionAddressSpaceIds.DpAbInput).ArtifactId;

        List<ProtectedPathGuard.ProtectedPath>? primaryOutputProtectedPaths = additionalOutputProtectedPaths is null
            ? null
            : [.. additionalOutputProtectedPaths];
        WorkbenchAbAFlashCodeDeliveryPlan? aFlashCodePlan = null;
        if (!string.IsNullOrWhiteSpace(aFlashCodeOutputPath))
        {
            if (!build || string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException(
                    "A FlashCode delivery requires one explicit AB Build output path.",
                    nameof(aFlashCodeOutputPath));
            }

            CompositionOutputNamePreview preview = await WorkbenchCompositionService
                .ResolveAutomaticOutputNameAsync(
                    RunIdPrefix,
                    composition,
                    bindings,
                    abMergeTopologySelection,
                    cancellationToken)
                .ConfigureAwait(false);
            aFlashCodePlan = !preview.CanUseAutomaticName
                ? throw new InvalidOperationException(WorkbenchCompositionService.FormatIssues(preview.Issues))
                : await AbMergeAFlashCodeExportService.TryCreatePlanAsync(
                    composition,
                    slotPaths,
                    preview,
                    cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException(
                        "The selected AB profile does not declare an optional A FlashCode delivery.");
            AbMergeAFlashCodeExportService.ValidateOutputPath(
                aFlashCodePlan,
                outputPath,
                aFlashCodeOutputPath);
            primaryOutputProtectedPaths ??= [];
            primaryOutputProtectedPaths.Add(new ProtectedPathGuard.ProtectedPath(
                Path.GetFullPath(aFlashCodeOutputPath),
                "A FlashCode output"));
        }

        CompositionRunResult result = await WorkbenchCompositionService.RunCompiledCompositionResultAsync(
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
            additionalOutputProtectedPaths: primaryOutputProtectedPaths,
            outputPathUsesAutomaticName: outputPathUsesAutomaticName).ConfigureAwait(false);
        if (aFlashCodePlan is null || !build || result.Status != CompositionExecutionStatus.Succeeded ||
            string.IsNullOrWhiteSpace(result.CommittedOutputId))
        {
            return WorkbenchCompositionService.ToWorkbenchRunResult(result);
        }

        try
        {
            WorkbenchDeliveryArtifact delivery = await AbMergeAFlashCodeExportService.ExportAsync(
                aFlashCodePlan,
                result.OutputBytes,
                result.CommittedOutputId,
                aFlashCodeOutputPath!,
                cancellationToken).ConfigureAwait(false);
            DeliveryArtifactSummary reportDelivery = AbMergeAFlashCodeExportService.CreateReportSummary(
                aFlashCodePlan,
                result.OutputBytes,
                aFlashCodeOutputPath!,
                committed: true);
            return WorkbenchCompositionService.ToWorkbenchRunResult(
                result,
                [reportDelivery],
                deliveredArtifacts: [delivery]);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            DeliveryArtifactSummary reportDelivery = AbMergeAFlashCodeExportService.CreateReportSummary(
                aFlashCodePlan,
                result.OutputBytes,
                aFlashCodeOutputPath!,
                committed: false);
            var issue = new CompositionIssue(
                "delivery.ab-a-flashcode.failed",
                $"The primary AB FlashCode committed, but the requested A FlashCode could not be delivered: {exception.Message}",
                AbMergeAFlashCodeExportService.AFlashCodeDeliveryKind);
            return WorkbenchCompositionService.ToWorkbenchRunResult(
                result,
                [reportDelivery],
                [issue],
                isDeliveryComplete: false,
                deliveryFailureMessage: issue.Message);
        }
    }

    private static InputArtifactBinding[] CreateInputBindings(
        CompiledComposition composition,
        IReadOnlyDictionary<string, string> slotPaths)
    {
        return
        [
            .. composition.Plan.RequiredInputAddressSpaceIds
                .Order(StringComparer.Ordinal)
                .Select(addressSpaceId => slotPaths.TryGetValue(addressSpaceId, out string? path) &&
                    !string.IsNullOrWhiteSpace(path)
                        ? CompiledCompositionInputBindingFactory.Create(
                            composition,
                            addressSpaceId,
                            Path.GetFullPath(path))
                        : throw new InvalidOperationException($"Input slot '{addressSpaceId}' is required.")),
        ];
    }

    private static CompiledComposition CompileExecutableAbMerge(
        string normalizedIcId,
        TopologySelection? topology)
    {
        if (!WorkbenchCompositionService.GetAbMergeProfileSummaries().Any(
                profile => StringComparer.Ordinal.Equals(profile.IcId, normalizedIcId)))
        {
            throw new InvalidOperationException($"AB Merge is not available for '{normalizedIcId}'.");
        }

        ValidateTopologySelection(GetTopologyChoices(normalizedIcId), topology);
        return !TryCompileAbMerge(normalizedIcId, topology, out CompiledComposition? composition, out IReadOnlyList<CompositionIssue> issues)
            ? throw new InvalidOperationException(WorkbenchCompositionService.FormatIssues(issues))
            : composition;
    }

    private static void ValidateTopologySelection(
        IReadOnlyList<WorkbenchAbMergeTopologyChoice> topologyChoices,
        TopologySelection? topology)
    {
        if (topologyChoices.Count > 0 && topology is null)
        {
            throw new InvalidOperationException("AB Merge requires one explicit topology choice: 1 IC or Cascade.");
        }

        if (topologyChoices.Count == 0 && topology is not null)
        {
            throw new InvalidOperationException("The selected AB Merge profile does not expose an IC topology choice.");
        }
    }
}

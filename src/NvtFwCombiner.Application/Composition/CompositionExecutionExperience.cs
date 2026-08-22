using System.Globalization;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Executes every accepted workflow through one Application-owned operation.</summary>
internal sealed class CompositionExecutionExperience : ICompositionExecution
{
    private const string StandardMergeRunIdPrefix = "ui";
    private const string GeneralMergeRunIdPrefix = "ui-merge-general";
    private const string AbMergeRunIdPrefix = "ui-merge-ab";
    private const string DpReplaceRunIdPrefix = "ui-replace-dp";
    private const string CtrlRamReplaceRunIdPrefix = "ui-replace-ctrlram";
    private const string GeneralReplaceRunIdPrefix = "ui-replace-general";

    private readonly ICanonicalCapabilityQuery _capabilities;
    private readonly ICompositionExecutionDestinationProvider _destinations;
    private readonly Func<CompositionExternalProcessorLease> _acquireExternalProcessor;
    private readonly Func<long, bool> _externalProcessorGenerationIsCurrent;
    private readonly ISystemClock _clock;

    internal CompositionExecutionExperience(
        ICanonicalCapabilityQuery capabilities,
        ICompositionExecutionDestinationProvider destinations,
        Func<CompositionExternalProcessorLease> acquireExternalProcessor,
        Func<long, bool> externalProcessorGenerationIsCurrent,
        ISystemClock clock)
    {
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _destinations = destinations ?? throw new ArgumentNullException(nameof(destinations));
        _acquireExternalProcessor = acquireExternalProcessor ??
            throw new ArgumentNullException(nameof(acquireExternalProcessor));
        _externalProcessorGenerationIsCurrent = externalProcessorGenerationIsCurrent ??
            throw new ArgumentNullException(nameof(externalProcessorGenerationIsCurrent));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public ValueTask<CompositionRunResult> ExecuteAsync(
        AcceptedCompositionExecutionRequest request,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(progress);
        ValidateBundleRequestOptions(request);
        ActiveSessionSnapshot session = request.AcceptedSession;
        ResolvedCapability capability = AcceptedSessionExecutionInputs.RequireCapability(
            session,
            session.WorkflowId,
            request.IcId,
            session.WorkflowId is ExperienceIds.GeneralMerge or ExperienceIds.GeneralReplace
                ? AuthoringDerivedResultKind.Validation
                : AuthoringDerivedResultKind.Inspection);
        return session.WorkflowId switch
        {
            ExperienceIds.StandardMerge => ExecuteAcceptedCompositionAsync(
                StandardMergeRunIdPrefix,
                request,
                capability,
                progress,
                firstInputAddressSpaceId: null,
                externalProcessor: null,
                icNumberSelection: null,
                abMergeTopologySelection: null,
                additionalProtectedPaths: [],
                additionalDelivery: null,
                cancellationToken),
            ExperienceIds.GeneralMerge => ExecuteGeneralMergeAsync(
                request,
                capability,
                progress,
                cancellationToken),
            ExperienceIds.AbMerge => ExecuteAbMergeAsync(
                request,
                capability,
                progress,
                cancellationToken),
            ExperienceIds.GeneralReplace => ExecuteGeneralReplaceAsync(
                request,
                capability,
                progress,
                cancellationToken),
            ExperienceIds.DpReplace => ExecuteAcceptedCompositionAsync(
                DpReplaceRunIdPrefix,
                request,
                capability,
                progress,
                CompositionAddressSpaceIds.ReferenceBase,
                externalProcessor: null,
                capability.DpExecutionPlan?.IcNumberSelection ??
                    throw new InvalidOperationException(
                        "Accepted DP Replace capability has no execution selection."),
                abMergeTopologySelection: null,
                additionalProtectedPaths: [],
                additionalDelivery: null,
                cancellationToken),
            ExperienceIds.CtrlRamReplace => ExecuteCtrlRamReplaceAsync(
                request,
                capability,
                progress,
                cancellationToken),
            _ => throw new InvalidOperationException(
                $"Accepted workflow '{session.WorkflowId}' has no execution path."),
        };
    }

    private ValueTask<CompositionRunResult> ExecuteAbMergeAsync(
        AcceptedCompositionExecutionRequest request,
        ResolvedCapability capability,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken)
    {
        CompositionExecutionDeliveryTarget? additionalDelivery = null;
        if (!string.IsNullOrWhiteSpace(request.AdditionalDeliveryOutputPath))
        {
            if (!request.Build || string.IsNullOrWhiteSpace(request.OutputPath))
            {
                throw new ArgumentException(
                    "A FlashCode delivery requires one explicit AB Build output path.",
                    nameof(request));
            }

            additionalDelivery = new CompositionExecutionDeliveryTarget(
                CompiledAdditionalDelivery.AbAFlashCodeKind,
                request.AdditionalDeliveryOutputPath,
                request.AdditionalDeliveryOutputPathUsesAutomaticName,
                [
                    .. request.AcceptedSession.Slots
                        .Where(static slot => slot.SelectedPath is not null)
                        .Select(static slot => new CompositionExecutionProtectedPath(
                            Path.GetFullPath(slot.SelectedPath!),
                            "AB input artifact")),
                ]);
        }

        CompositionExecutionProtectedPath[] protectedPaths =
        [
            .. string.IsNullOrWhiteSpace(request.ReportPath)
                ? Array.Empty<CompositionExecutionProtectedPath>()
                : [new CompositionExecutionProtectedPath(request.ReportPath, "CLI report")],
        ];
        return ExecuteAcceptedCompositionAsync(
            AbMergeRunIdPrefix,
            request,
            capability,
            progress,
            CompositionAddressSpaceIds.DpAbInput,
            _acquireExternalProcessor().Processor,
            icNumberSelection: null,
            abMergeTopologySelection:
                CapabilityPublicationCoherence.GetAcceptedAbMergeTopologySelection(capability),
            additionalProtectedPaths: protectedPaths,
            additionalDelivery,
            cancellationToken);
    }

    private async ValueTask<CompositionRunResult> ExecuteGeneralMergeAsync(
        AcceptedCompositionExecutionRequest request,
        ResolvedCapability capability,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken)
    {
        ActiveSessionSnapshot session = request.AcceptedSession;
        GeneralMergeDraftState draft = session.DraftState as GeneralMergeDraftState ??
            throw new InvalidOperationException(
                "The accepted General Merge session has no exact typed draft.");
        AcceptedGeneralExecutionPlan plan = capability.GeneralExecutionPlan ??
            throw new InvalidOperationException(
                "The accepted General Merge capability has no retained execution plan.");
        if (!plan.Admission.Draft.HasSameCompilationInputs(draft.Mappings))
        {
            throw new InvalidOperationException(
                "The accepted General Merge draft does not match its retained execution plan.");
        }

        CompiledComposition composition = capability.CompiledComposition;
        (InputArtifactBinding[] bindings, IReadOnlyDictionary<string, byte[]> artifacts) =
            AcceptedSessionExecutionInputs.CreateGeneralBindings(
                composition,
                session,
                plan.InputBindings);
        CompositionRunResult result = await RunCompiledCompositionAsync(
                GeneralMergeRunIdPrefix,
                composition,
                bindings,
                bindings[0].ArtifactId,
                request,
                externalProcessor: null,
                icNumberSelection: null,
                artifacts,
                progress,
                additionalProtectedPaths: [],
                additionalDelivery: null,
                advisoryIssues: null,
                generalAdmission: plan.Admission,
                capability,
                cancellationToken)
            .ConfigureAwait(false);
        result.AcceptedGeneralMappingDraft = draft.Mappings;
        return result;
    }

    private async ValueTask<CompositionRunResult> ExecuteGeneralReplaceAsync(
        AcceptedCompositionExecutionRequest request,
        ResolvedCapability capability,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken)
    {
        ActiveSessionSnapshot session = request.AcceptedSession;
        GeneralMappingDraftState draft = session.DraftState as GeneralMappingDraftState ??
            throw new InvalidOperationException(
                "The accepted General Replace session has no exact typed draft.");
        AcceptedGeneralExecutionPlan plan = capability.GeneralExecutionPlan ??
            throw new InvalidOperationException(
                "The accepted General Replace capability has no retained execution plan.");
        if (!plan.Admission.Draft.HasSameCompilationInputs(draft))
        {
            throw new InvalidOperationException(
                "The accepted General Replace draft does not match its retained execution plan.");
        }

        RequireGeneralReplaceActionReadiness(request, capability);
        CompiledComposition composition = capability.CompiledComposition;
        string referenceAddressSpaceId =
            AcceptedSessionExecutionInputs.ResolveReferenceImageAddressSpaceId(composition);
        (InputArtifactBinding[] bindings, IReadOnlyDictionary<string, byte[]> artifacts) =
            AcceptedSessionExecutionInputs.CreateGeneralReplaceBindings(
                composition,
                session,
                plan.InputBindings,
                plan.VirtualArtifacts,
                referenceAddressSpaceId);
        IExternalProcessor? processor = composition.Plan.OrderedOperations.Any(
            static operation =>
                operation.Kind == CompositionOperationKind.RunExternalProcessor)
                    ? _acquireExternalProcessor().Processor
                    : null;
        CompositionRunResult result = await RunCompiledCompositionAsync(
                GeneralReplaceRunIdPrefix,
                composition,
                bindings,
                bindings[0].ArtifactId,
                request,
                processor,
                plan.IcNumberSelection ?? throw new InvalidOperationException(
                    "The accepted General Replace plan has no IC-number selection."),
                artifacts,
                progress,
                additionalProtectedPaths: [],
                additionalDelivery: null,
                advisoryIssues: null,
                generalAdmission: plan.Admission,
                capability,
                cancellationToken)
            .ConfigureAwait(false);
        result.AcceptedGeneralMappingDraft = draft;
        return result;
    }

    private async ValueTask<CompositionRunResult> ExecuteCtrlRamReplaceAsync(
        AcceptedCompositionExecutionRequest request,
        ResolvedCapability capability,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken)
    {
        CompositionExternalProcessorLease runtime = _acquireExternalProcessor();
        ArgumentOutOfRangeException.ThrowIfLessThan(runtime.Generation, 1);
        ActiveSessionSnapshot session = request.AcceptedSession;
        AcceptedCtrlRamExecutionPlan plan = capability.CtrlRamExecutionPlan ??
            throw new InvalidOperationException(
                "The accepted CtrlRAM capability has no retained execution plan.");
        if (!CtrlRamAuthoringExperience.HasSameCtrlRamVersionDraft(
                session.DraftState,
                plan.FirmwareVersionDraft))
        {
            throw new InvalidOperationException(
                "The accepted CtrlRAM firmware-version draft does not match its retained execution plan.");
        }

        RequireCtrlRamActionReadiness(request, capability, runtime.Generation);
        CompiledComposition composition = capability.CompiledComposition;
        (InputArtifactBinding[] bindings, IReadOnlyDictionary<string, byte[]> artifacts) =
            AcceptedSessionExecutionInputs.CreateBindings(composition, session);
        InputArtifactBinding reference = bindings.Single(static binding =>
            StringComparer.Ordinal.Equals(
                binding.AddressSpaceId,
                CompositionAddressSpaceIds.ReferenceBase));
        return await RunCompiledCompositionAsync(
                CtrlRamReplaceRunIdPrefix,
                composition,
                bindings,
                reference.ArtifactId,
                request,
                runtime.Processor,
                plan.IcNumberSelection,
                artifacts,
                progress,
                additionalProtectedPaths: [],
                additionalDelivery: null,
                advisoryIssues: plan.AdvisoryIssues,
                generalAdmission: null,
                capability,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private ValueTask<CompositionRunResult> ExecuteAcceptedCompositionAsync(
        string runIdPrefix,
        AcceptedCompositionExecutionRequest request,
        ResolvedCapability capability,
        CompositionRunProgressFeed progress,
        string? firstInputAddressSpaceId,
        IExternalProcessor? externalProcessor,
        IcNumberSelection? icNumberSelection,
        TopologySelection? abMergeTopologySelection,
        IReadOnlyList<CompositionExecutionProtectedPath> additionalProtectedPaths,
        CompositionExecutionDeliveryTarget? additionalDelivery,
        CancellationToken cancellationToken)
    {
        CompiledComposition composition = capability.CompiledComposition;
        (InputArtifactBinding[] bindings, IReadOnlyDictionary<string, byte[]> artifacts) =
            AcceptedSessionExecutionInputs.CreateBindings(
                composition,
                request.AcceptedSession);
        InputArtifactBinding firstInput = firstInputAddressSpaceId is null
            ? bindings[0]
            : bindings.Single(binding => StringComparer.Ordinal.Equals(
                binding.AddressSpaceId,
                firstInputAddressSpaceId));
        return RunCompiledCompositionAsync(
            runIdPrefix,
            composition,
            bindings,
            firstInput.ArtifactId,
            request,
            externalProcessor,
            icNumberSelection,
            artifacts,
            progress,
            additionalProtectedPaths,
            additionalDelivery,
            advisoryIssues: null,
            generalAdmission: null,
            capability,
            cancellationToken,
            abMergeTopologySelection);
    }

    private async ValueTask<CompositionRunResult> RunCompiledCompositionAsync(
        string runIdPrefix,
        CompiledComposition composition,
        IReadOnlyList<InputArtifactBinding> bindings,
        string firstInputPath,
        AcceptedCompositionExecutionRequest request,
        IExternalProcessor? externalProcessor,
        IcNumberSelection? icNumberSelection,
        IReadOnlyDictionary<string, byte[]> artifacts,
        CompositionRunProgressFeed progress,
        IReadOnlyList<CompositionExecutionProtectedPath> additionalProtectedPaths,
        CompositionExecutionDeliveryTarget? additionalDelivery,
        IReadOnlyList<CompositionIssue>? advisoryIssues,
        GeneralAuthoringAdmissionResult? generalAdmission,
        ResolvedCapability capability,
        CancellationToken cancellationToken,
        TopologySelection? abMergeTopologySelection = null)
    {
        CompositionExecutionBundleDelivery? bundleDelivery = CreateBundleDelivery(request);
        if (request.OutputPathUsesAutomaticName &&
            (string.IsNullOrWhiteSpace(request.OutputPath) ||
             request.PreviewOutputFileName is not null))
        {
            throw new ArgumentException(
                "An automatic output directory requires a selected output path and cannot be combined with a Preview output name.",
                nameof(request));
        }

        (string outputDirectory, string outputFileName) = bundleDelivery is null
            ? ResolveOutputTarget(
                firstInputPath,
                request.Build,
                request.OutputPath,
                composition.V2Details.OutputNamingRequirement.FileNameTemplate,
                request.AutomaticOutputDirectory)
            : (
                bundleDelivery.ParentDirectory,
                bundleDelivery.Admission.OutputPreparation.OutputName.FileName);
        if (request.OutputPathUsesAutomaticName)
        {
            outputFileName =
                composition.V2Details.OutputNamingRequirement.FileNameTemplate;
        }

        if (request.PreviewOutputFileName is not null)
        {
            if (request.Build ||
                string.IsNullOrWhiteSpace(request.PreviewOutputFileName) ||
                Path.GetFileName(request.PreviewOutputFileName) !=
                    request.PreviewOutputFileName ||
                request.PreviewOutputFileName.IndexOfAny(['/', '\\', ':']) >= 0)
            {
                throw new ArgumentException(
                    "A preview output name must be one plain filename and is not valid for Build.",
                    nameof(request));
            }

            outputFileName = request.PreviewOutputFileName;
        }

        CompositionExecutionDestination destination = _destinations.Prepare(
            new CompositionExecutionDestinationRequest(
                request.Build,
                outputDirectory,
                outputFileName,
                request.OutputPathUsesAutomaticName,
                bindings,
                additionalProtectedPaths,
                additionalDelivery,
                bundleDelivery));
        string executionOutputFileName = bundleDelivery is null
            ? outputFileName
            : composition.V2Details.OutputNamingRequirement.FileNameTemplate;
        return await AcceptedSessionCompositionExecution.ExecuteAsync(
                _capabilities,
                CreateRunId(runIdPrefix, request.Build),
                request.AcceptedSession,
                capability,
                bindings,
                artifacts,
                executionOutputFileName,
                request.Build,
                _clock,
                destination.OutputWriter,
                externalProcessor,
                destination.DeliveryWriter,
                icNumberSelection,
                (request.OutputPath is not null &&
                 !request.OutputPathUsesAutomaticName) ||
                    request.PreviewOutputFileName is not null,
                abMergeTopologySelection,
                advisoryIssues,
                generalAdmission?.ToSummary(),
                bundleDelivery,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static CompositionExecutionBundleDelivery? CreateBundleDelivery(
        AcceptedCompositionExecutionRequest request)
    {
        return request.OutputBundle is null
            ? null
            : request.Build
                ? CreateAdmittedBundleDelivery(request)
                : throw new ArgumentException(
                    "Atomic bundle delivery is available only for Build.",
                    nameof(request));
    }

    private static CompositionExecutionBundleDelivery CreateAdmittedBundleDelivery(
        AcceptedCompositionExecutionRequest request)
    {
        request.OutputBundle!.Admission.ValidateSession(request.AcceptedSession);
        return new CompositionExecutionBundleDelivery(request.OutputBundle);
    }

    private static void ValidateBundleRequestOptions(
        AcceptedCompositionExecutionRequest request)
    {
        ValidateBundleOptionCombination(
            request.OutputBundle is not null,
            request.Build,
            request.OutputPath is not null,
            request.PreviewOutputFileName is not null,
            request.AutomaticOutputDirectory is not null,
            request.OutputPathUsesAutomaticName,
            request.AdditionalDeliveryOutputPath is not null,
            request.AdditionalDeliveryOutputPathUsesAutomaticName);
    }

    internal static void ValidateBundleOptionCombination(
        bool hasBundle,
        bool build,
        bool hasOutputPath,
        bool hasPreviewOutputFileName,
        bool hasAutomaticOutputDirectory,
        bool outputPathUsesAutomaticName,
        bool hasAdditionalDeliveryOutputPath,
        bool additionalDeliveryOutputPathUsesAutomaticName)
    {
        if (!hasBundle)
        {
            return;
        }

        if (!build ||
            hasOutputPath ||
            hasPreviewOutputFileName ||
            hasAutomaticOutputDirectory ||
            outputPathUsesAutomaticName ||
            hasAdditionalDeliveryOutputPath ||
            additionalDeliveryOutputPathUsesAutomaticName)
        {
            throw new ArgumentException(
                "Prepared bundle delivery cannot be combined with Preview, primary-name overrides, automatic output paths, or a separate loose additional-delivery path.");
        }
    }

    private void RequireCtrlRamActionReadiness(
        AcceptedCompositionExecutionRequest request,
        ResolvedCapability capability,
        long runtimeDependencyGeneration)
    {
        CapabilityActionReadinessSnapshot readiness = request.ActionReadiness ??
            throw new InvalidOperationException(
                "CtrlRAM Replace execution requires its exact typed action readiness.");
        ActiveSessionSnapshot session = request.AcceptedSession;
        if (readiness.ResolutionToken != session.ResolutionToken ||
            readiness.AuthoringRevision != session.AuthoringRevision ||
            readiness.RuntimeDependencyGeneration != runtimeDependencyGeneration ||
            !_externalProcessorGenerationIsCurrent(runtimeDependencyGeneration) ||
            !StringComparer.Ordinal.Equals(
                readiness.RouteId,
                capability.Identity.RouteId) ||
            !StringComparer.Ordinal.Equals(
                readiness.CapabilityFingerprint,
                capability.CapabilityFingerprint) ||
            !StringComparer.Ordinal.Equals(
                readiness.CompilationFingerprint,
                capability.CompiledComposition.CompilationFingerprint))
        {
            throw new InvalidOperationException(
                "CtrlRAM Replace action readiness does not match the accepted publication and runtime generation.");
        }

        RequireAvailableAction(request, readiness);
    }

    private static void RequireGeneralReplaceActionReadiness(
        AcceptedCompositionExecutionRequest request,
        ResolvedCapability capability)
    {
        CapabilityActionReadinessSnapshot readiness = request.ActionReadiness ??
            throw new InvalidOperationException(
                "General Replace execution requires its exact typed action readiness.");
        ActiveSessionSnapshot session = request.AcceptedSession;
        if (readiness.ResolutionToken != session.ResolutionToken ||
            readiness.AuthoringRevision != session.AuthoringRevision ||
            !StringComparer.Ordinal.Equals(
                readiness.CapabilityFingerprint,
                capability.CapabilityFingerprint) ||
            !StringComparer.Ordinal.Equals(
                readiness.CompilationFingerprint,
                capability.CompiledComposition.CompilationFingerprint))
        {
            throw new InvalidOperationException(
                "General Replace action readiness does not match the accepted publication.");
        }

        RequireAvailableAction(request, readiness);
        if (!request.Build && !readiness.Build.IsAvailable)
        {
            throw new InvalidOperationException(
                "Plan-only General Replace Preview must consume its typed diagnostic outcome without executing bytes.");
        }
    }

    private static void RequireAvailableAction(
        AcceptedCompositionExecutionRequest request,
        CapabilityActionReadinessSnapshot readiness)
    {
        CapabilityActionAvailability action = request.Build
            ? readiness.Build
            : readiness.Preview;
        if (!action.IsAvailable)
        {
            throw new InvalidOperationException(
                action.PrimaryBlocker?.Message ??
                "The selected composition action is unavailable.");
        }
    }

    private string CreateRunId(string prefix, bool build)
    {
        string suffix = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string action = build ? "build" : "preview";
        string timestamp = _clock.UtcNow.ToUnixTimeMilliseconds()
            .ToString(CultureInfo.InvariantCulture);
        return $"{prefix}-{action}-{timestamp}-{suffix}";
    }

    private static (string Directory, string FileName) ResolveOutputTarget(
        string firstInputPath,
        bool build,
        string? outputPath,
        string defaultOutputFileName,
        string? automaticOutputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            string automaticDirectory = string.IsNullOrWhiteSpace(automaticOutputDirectory)
                ? Path.GetDirectoryName(firstInputPath)!
                : Path.GetFullPath(automaticOutputDirectory);
            return (automaticDirectory, defaultOutputFileName);
        }

        if (!build)
        {
            throw new ArgumentException(
                "Preview does not accept an output file path.",
                nameof(outputPath));
        }

        string fullPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        string fileName = Path.GetFileName(fullPath);
        return string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName)
            ? throw new ArgumentException(
                "Build output path must include a directory and file name.",
                nameof(outputPath))
            : (directory, fileName);
    }
}

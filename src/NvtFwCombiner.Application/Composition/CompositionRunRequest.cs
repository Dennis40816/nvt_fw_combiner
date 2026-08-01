using System.Globalization;
using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Application request for previewing or building a compiled composition profile.</summary>
public sealed class CompositionRunRequest
{
    /// <summary>Creates a run request with one compiled composition, input bindings, and runtime output options.</summary>
    public CompositionRunRequest(
        string runId,
        CompiledComposition compiledComposition,
        IEnumerable<InputArtifactBinding> artifactBindings,
        string outputFileName,
        string? approvedPreviewToken = null,
        IcNumberSelection? icNumberSelection = null,
        bool outputFileNameIsOverride = false,
        TopologySelection? abMergeTopologySelection = null,
        IEnumerable<CompositionIssue>? advisoryIssues = null,
        GeneralAuthoringAdmissionSummary? generalAdmission = null,
        AcceptedOutputNamingInspection? outputNamingInspection = null,
        OutputNamingAdmissionIdentity? outputNamingAdmission = null,
        ResolvedCapability? resolvedCapability = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(compiledComposition);
        ArgumentNullException.ThrowIfNull(artifactBindings);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFileName);
        Dictionary<string, InputArtifactBinding> copiedBindings = CopyBindings(artifactBindings);
        bool effectiveOutputFileNameIsOverride = outputFileNameIsOverride ||
            IsImplicitStaticOutputOverride(compiledComposition, outputFileName);
        ValidateOutputFileName(outputFileName);
        ValidateExecutableComposition(compiledComposition);
        ValidateRuntimeValidationRequirements(compiledComposition);
        ValidateIcNumberSelection(compiledComposition, icNumberSelection);
        ValidateAbMergeTopologySelection(compiledComposition, abMergeTopologySelection);
        ValidateOutputNamingAdmission(
            compiledComposition,
            outputNamingInspection,
            outputNamingAdmission);
        ValidateResolvedCapability(compiledComposition, resolvedCapability);
        ValidateV2RuntimeRequest(
            compiledComposition,
            copiedBindings,
            outputFileName,
            effectiveOutputFileNameIsOverride);

        RunId = runId;
        CompiledComposition = compiledComposition;
        ArtifactBindings = new ReadOnlyDictionary<string, InputArtifactBinding>(copiedBindings);
        OutputFileName = outputFileName;
        IsOutputFileNameOverride = effectiveOutputFileNameIsOverride;
        ApprovedPreviewToken = string.IsNullOrWhiteSpace(approvedPreviewToken) ? null : approvedPreviewToken;
        IcNumberSelection = icNumberSelection;
        AbMergeTopologySelection = abMergeTopologySelection;
        AdvisoryIssues = CopyAdvisoryIssues(advisoryIssues);
        GeneralAdmission = generalAdmission;
        OutputNamingInspection = outputNamingInspection;
        OutputNamingAdmission = outputNamingAdmission;
        ResolvedCapability = resolvedCapability;
    }

    /// <summary>Stable run id for reports and diagnostics.</summary>
    public string RunId { get; }

    /// <summary>Atomic compiler output containing identity, policy, and the sole execution plan.</summary>
    public CompiledComposition CompiledComposition { get; }

    /// <summary>Maps required address-space ids to copied artifact bindings.</summary>
    public IReadOnlyDictionary<string, InputArtifactBinding> ArtifactBindings { get; }

    /// <summary>Output file name proposed by profile naming policy or caller override.</summary>
    public string OutputFileName { get; }

    /// <summary>Whether the caller supplied an explicit UI/CLI filename override.</summary>
    public bool IsOutputFileNameOverride { get; }

    /// <summary>Preview token that authorizes a matching build request.</summary>
    public string? ApprovedPreviewToken { get; }

    /// <summary>IC number selected for Replace profile binding.</summary>
    public IcNumberSelection? IcNumberSelection { get; }

    /// <summary>Explicit topology chosen only for an AB Merge profile whose resolved map requires it.</summary>
    public TopologySelection? AbMergeTopologySelection { get; }

    /// <summary>Caller-supplied typed warnings or information retained in Preview/Build reports.</summary>
    public IReadOnlyList<CompositionIssue> AdvisoryIssues { get; }

    /// <summary>Path-free General admission provenance shared by Preview and Build.</summary>
    public GeneralAuthoringAdmissionSummary? GeneralAdmission { get; }

    /// <summary>
    /// Accepted canonical metadata inspection used only by a compiled normal output-name renderer.
    /// </summary>
    public AcceptedOutputNamingInspection? OutputNamingInspection { get; }

    /// <summary>Current publication and revision admitted for normal output naming.</summary>
    public OutputNamingAdmissionIdentity? OutputNamingAdmission { get; }

    /// <summary>Publication-bound capability that owns report metadata for this exact compilation.</summary>
    public ResolvedCapability? ResolvedCapability { get; }

    /// <summary>Returns a copy of this request with a preview token approved for build.</summary>
    public CompositionRunRequest WithApprovedPreviewToken(string previewToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(previewToken);
        return OutputNamingAdmission is not null
            ? throw new InvalidOperationException(
                "Normal output naming requires a freshly captured build admission.")
            : new CompositionRunRequest(
                RunId,
                CompiledComposition,
                ArtifactBindings.Values,
                OutputFileName,
                previewToken,
                IcNumberSelection,
                IsOutputFileNameOverride,
                AbMergeTopologySelection,
                AdvisoryIssues,
                GeneralAdmission,
                OutputNamingInspection,
                resolvedCapability: ResolvedCapability);
    }

    /// <summary>
    /// Returns a build request only when the freshly captured admission still
    /// matches the accepted inspection used by the preview.
    /// </summary>
    public CompositionRunRequest WithApprovedPreviewToken(
        string previewToken,
        OutputNamingAdmissionIdentity currentAdmission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(previewToken);
        ArgumentNullException.ThrowIfNull(currentAdmission);
        return new CompositionRunRequest(
            RunId,
            CompiledComposition,
            ArtifactBindings.Values,
            OutputFileName,
            previewToken,
            IcNumberSelection,
            IsOutputFileNameOverride,
            AbMergeTopologySelection,
            AdvisoryIssues,
            GeneralAdmission,
            OutputNamingInspection,
            currentAdmission,
            ResolvedCapability);
    }

    private static void ValidateResolvedCapability(
        CompiledComposition compiledComposition,
        ResolvedCapability? resolvedCapability)
    {
        if (resolvedCapability is null)
        {
            if (compiledComposition.CapabilityFingerprint is not null)
            {
                throw new ArgumentException(
                    "Capability-bound compilations require their exact current resolved capability.",
                    nameof(resolvedCapability));
            }

            return;
        }

        if (resolvedCapability.Authoring.Value !=
                CapabilityAuthoringAvailability.Available ||
            !resolvedCapability.ExecutionAdmitted ||
            !ReferenceEquals(
                resolvedCapability.CompiledComposition,
                compiledComposition) ||
            !StringComparer.Ordinal.Equals(
                resolvedCapability.CapabilityFingerprint,
                compiledComposition.CapabilityFingerprint) ||
            !StringComparer.Ordinal.Equals(
                resolvedCapability.CompiledComposition.CompilationFingerprint,
                compiledComposition.CompilationFingerprint))
        {
            throw new ArgumentException(
                "Resolved report capability must own the exact executable compilation.",
                nameof(resolvedCapability));
        }
    }

    private static ReadOnlyCollection<CompositionIssue> CopyAdvisoryIssues(
        IEnumerable<CompositionIssue>? advisoryIssues)
    {
        CompositionIssue[] copy = advisoryIssues is null ? [] : [.. advisoryIssues];
        return copy.Any(static issue => issue is null)
            ? throw new ArgumentException(
                "Advisory issues cannot contain null.",
                nameof(advisoryIssues))
            : copy.Any(static issue =>
                StringComparer.Ordinal.Equals(issue.Severity, CompositionIssueSeverity.Error))
            ? throw new ArgumentException(
                "Advisory issues must use info or warning severity.",
                nameof(advisoryIssues))
            : Array.AsReadOnly(copy);
    }

    private static Dictionary<string, InputArtifactBinding> CopyBindings(IEnumerable<InputArtifactBinding> bindings)
    {
        Dictionary<string, InputArtifactBinding> copy = new(StringComparer.Ordinal);
        foreach (InputArtifactBinding binding in bindings)
        {
            if (!copy.TryAdd(binding.AddressSpaceId, binding))
            {
                throw new ArgumentException(
                    $"Artifact binding for address space '{binding.AddressSpaceId}' is declared more than once.",
                    nameof(bindings));
            }
        }

        return copy;
    }

    private static void ValidateOutputFileName(string outputFileName)
    {
        if (outputFileName.IndexOfAny(['/', '\\', ':']) >= 0 ||
            outputFileName is "." or ".." ||
            Path.GetFileName(outputFileName) != outputFileName)
        {
            throw new ArgumentException("Output file name must be a plain filename without path syntax.", nameof(outputFileName));
        }
    }

    private static bool IsImplicitStaticOutputOverride(
        CompiledComposition compiledComposition,
        string outputFileName)
    {
        CompiledOutputNamingRequirement? output = compiledComposition.V2Details?.OutputNamingRequirement;
        return output is { AllowOverride: true, RendererKind: CompiledOutputNameRendererKind.Static } &&
            !string.Equals(outputFileName, output.FileNameTemplate, StringComparison.Ordinal);
    }

    private static void ValidateExecutableComposition(CompiledComposition compiledComposition)
    {
        if (compiledComposition.Eligibility == CompiledCompositionEligibility.LegacyRuntimeExecutable &&
            compiledComposition.Authority is LegacyProfileCompilationAuthority)
        {
            return;
        }

        if (compiledComposition.Eligibility == CompiledCompositionEligibility.V2RuntimeExecutable &&
            compiledComposition.Authority is ProfileBundleV2CompilationAuthority &&
            compiledComposition.V2Details is not null)
        {
            return;
        }

        if (compiledComposition.Eligibility == CompiledCompositionEligibility.V2PlanCompiled &&
            compiledComposition.Authority is ProfileBundleV2CompilationAuthority &&
            compiledComposition.V2Details is { Provenance.Promotion.Stage: CompiledProfilePromotionStage.ExecutableCandidate } details &&
            (details.Provenance.Context is LogicalOutputV2CompilationContext or RuntimeReferenceReplaceV2CompilationContext ||
             compiledComposition.IsV2AbFunctionOpenCandidate))
        {
            return;
        }

        throw new ArgumentException(
            "Compiled composition is not executable by the current application runtime.",
            nameof(compiledComposition));
    }

    private static void ValidateV2RuntimeRequest(
        CompiledComposition compiledComposition,
        Dictionary<string, InputArtifactBinding> bindings,
        string outputFileName,
        bool outputFileNameIsOverride)
    {
        if (compiledComposition.Authority is not ProfileBundleV2CompilationAuthority)
        {
            return;
        }

        V2CompiledCompositionDetails details = compiledComposition.V2Details ?? throw new ArgumentException(
            "V2 runtime artifacts require compiled V2 details.",
            nameof(compiledComposition));
        CompiledOutputNamingRequirement output = details.OutputNamingRequirement;
        if (output.InvalidCharacterPolicy != CompiledOutputInvalidCharacterPolicy.Reject ||
            output.RendererKind == CompiledOutputNameRendererKind.DeferredTokenTemplate)
        {
            throw new ArgumentException(
                "V2 runtime artifacts require an executable reject output renderer.",
                nameof(compiledComposition));
        }

        bool isSnapshotRenderedAutomatic = (output.RendererKind is
                CompiledOutputNameRendererKind.AbCodeV1 or
                CompiledOutputNameRendererKind.NormalFlashCodeV1 or
                CompiledOutputNameRendererKind.TpFirmwareV1) &&
            !outputFileNameIsOverride;
        if (isSnapshotRenderedAutomatic)
        {
            if (!string.Equals(outputFileName, output.FileNameTemplate, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Automatic output naming must retain the compiled template until accepted snapshots are read.",
                    nameof(outputFileName));
            }
        }
        else
        {
            CompiledOutputNamingRequirement.ValidateRuntimeLiteralFileName(outputFileName, nameof(outputFileName));
        }

        if (outputFileNameIsOverride && !output.AllowOverride)
        {
            throw new ArgumentException(
                "The compiled V2 output policy forbids explicit filename overrides.",
                nameof(outputFileName));
        }

        if (!outputFileNameIsOverride && output.RendererKind == CompiledOutputNameRendererKind.Static &&
            !string.Equals(outputFileName, output.FileNameTemplate, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Automatic static output naming must match the compiled V2 template.",
                nameof(outputFileName));
        }

        IReadOnlyList<CompiledInputSpaceBinding> expectedBindings = details.InputContract.SpaceBindings;
        if (bindings.Count != expectedBindings.Count)
        {
            throw new ArgumentException(
                "V2 runtime bindings must exactly match the compiled input contract.",
                nameof(bindings));
        }

        var slots = details.InputContract.Slots.ToDictionary(
            static slot => slot.SlotId,
            StringComparer.Ordinal);
        bool isLogicalOutput = details.Provenance.Context is LogicalOutputV2CompilationContext;
        bool isRuntimeReferenceReplace = details.Provenance.Context is RuntimeReferenceReplaceV2CompilationContext;
        CompiledInputArtifactClass runtimeReferenceSourceClass =
            details.Provenance.Context is RuntimeReferenceReplaceV2CompilationContext runtimeReferenceContext &&
            StringComparer.Ordinal.Equals(runtimeReferenceContext.ModeId, ExperienceIds.CtrlRamReplace)
                ? CompiledInputArtifactClass.CtrlRamReplacement
                : CompiledInputArtifactClass.Auxiliary;
        foreach (CompiledInputSpaceBinding expected in expectedBindings)
        {
            if (!bindings.TryGetValue(expected.AddressSpaceId, out InputArtifactBinding? binding))
            {
                throw new ArgumentException(
                    $"V2 runtime requires an artifact binding for address space '{expected.AddressSpaceId}'.",
                    nameof(bindings));
            }

            CompiledInputSlotRequirement slot = slots[expected.SlotId];
            bool satisfiesInputContract = isLogicalOutput
                ? expected.InstancePolicy == CompiledInputInstancePolicy.PerBinding &&
                  slot.Required &&
                  slot.Cardinality == CompiledInputSlotCardinality.OneOrMore &&
                  binding.BindingId == expected.AddressSpaceId &&
                  binding.ArtifactClass == slot.ArtifactClass &&
                  binding.OriginalFileName is not null
                : isRuntimeReferenceReplace
                    ? ((expected.InstancePolicy == CompiledInputInstancePolicy.Singleton &&
                        slot.Required &&
                        slot.Cardinality == CompiledInputSlotCardinality.ExactlyOne &&
                        slot.ArtifactClass == CompiledInputArtifactClass.ReferenceImage) ||
                       (expected.InstancePolicy == CompiledInputInstancePolicy.PerBinding &&
                        slot.Required &&
                        slot.Cardinality == CompiledInputSlotCardinality.OneOrMore &&
                        slot.ArtifactClass == runtimeReferenceSourceClass)) &&
                      binding.BindingId == expected.AddressSpaceId &&
                      binding.ArtifactClass == slot.ArtifactClass &&
                      binding.OriginalFileName is not null
                    : expected.InstancePolicy == CompiledInputInstancePolicy.Singleton &&
                  slot.Required &&
                  slot.Cardinality == CompiledInputSlotCardinality.ExactlyOne &&
                  binding.ArtifactClass == slot.ArtifactClass &&
                  binding.OriginalFileName is not null;
            if (!satisfiesInputContract)
            {
                throw new ArgumentException(
                    $"V2 runtime binding '{expected.AddressSpaceId}' does not satisfy the compiled input slot contract.",
                    nameof(bindings));
            }

            string originalFileName = binding.OriginalFileName ?? throw new InvalidOperationException(
                "A contract-matching V2 binding must retain its original file name.");
            string extension = Path.GetExtension(originalFileName);
            if (!slot.AcceptedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"V2 runtime binding '{expected.AddressSpaceId}' has an unaccepted original file extension.",
                    nameof(bindings));
            }
        }
    }

    private static void ValidateOutputNamingAdmission(
        CompiledComposition compiledComposition,
        AcceptedOutputNamingInspection? inspection,
        OutputNamingAdmissionIdentity? admission)
    {
        CompiledOutputNameRendererKind? renderer =
            compiledComposition.V2Details?.OutputNamingRequirement.RendererKind;
        bool requiresAcceptedInspection = renderer is
            CompiledOutputNameRendererKind.NormalFlashCodeV1 or
            CompiledOutputNameRendererKind.TpFirmwareV1;
        if (inspection is null)
        {
            if (requiresAcceptedInspection || admission is not null)
            {
                throw new ArgumentException(
                    "Compiled normal output naming requires one accepted inspection and current admission.",
                    nameof(inspection));
            }

            return;
        }

        if (admission is null)
        {
            throw new ArgumentException(
                "Compiled normal output naming requires a current publication admission.",
                nameof(admission));
        }

        if (!StringComparer.Ordinal.Equals(
                inspection.CompilationFingerprint,
                compiledComposition.CompilationFingerprint) ||
            !StringComparer.Ordinal.Equals(
                admission.CompilationFingerprint,
                compiledComposition.CompilationFingerprint) ||
            !StringComparer.Ordinal.Equals(inspection.RouteId, admission.RouteId) ||
            inspection.ResolutionToken != admission.ResolutionToken ||
            inspection.AuthoringRevision != admission.AuthoringRevision)
        {
            throw new ArgumentException(
                "Output naming inspection and admission must belong to the exact current route, publication, revision, and compiled capability.",
                nameof(inspection));
        }

        if (!requiresAcceptedInspection)
        {
            throw new ArgumentException(
                "Output naming inspection and admission are valid only for a compiled normal output-name renderer.",
                nameof(inspection));
        }
    }

    private static void ValidateIcNumberSelection(
        CompiledComposition compiledComposition,
        IcNumberSelection? selection)
    {
        if (compiledComposition.IcNumberPolicy == CompiledIcNumberPolicy.NotApplicable)
        {
            if (selection is not null)
            {
                throw new ArgumentException("IC number selection is allowed only for Replace runs.", nameof(selection));
            }

            return;
        }

        if (selection is null)
        {
            throw new ArgumentException("Replace runs require an IC number selection.", nameof(selection));
        }

        IcNumberInputMode expectedMode = compiledComposition.IcNumberPolicy switch
        {
            CompiledIcNumberPolicy.SingleSelector => IcNumberInputMode.SingleSelector,
            CompiledIcNumberPolicy.CascadeSelector => IcNumberInputMode.CascadeSelector,
            CompiledIcNumberPolicy.NumericSelector => IcNumberInputMode.NumericSelector,
            CompiledIcNumberPolicy.NotApplicable => throw new InvalidOperationException(
                "A non-applicable IC-number policy cannot require a selection."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(compiledComposition),
                compiledComposition.IcNumberPolicy,
                "Unknown compiled IC-number policy."),
        };
        if (selection.Mode != expectedMode)
        {
            throw new ArgumentException(
                "IC number selection mode must match the compiled composition IC number policy.",
                nameof(selection));
        }

        if (selection.Mode == IcNumberInputMode.NumericSelector &&
            !IsPositiveInteger(selection.Parts[^1]))
        {
            throw new ArgumentException("Numeric IC number selection must be a positive integer.", nameof(selection));
        }
    }

    private static void ValidateAbMergeTopologySelection(
        CompiledComposition compiledComposition,
        TopologySelection? selection)
    {
        if (!compiledComposition.IsV2AbMergeRuntimeRoute)
        {
            if (selection is not null)
            {
                throw new ArgumentException(
                    "AB topology selection is allowed only for an admitted AB Merge route.",
                    nameof(selection));
            }

            return;
        }

        TopologyRequirement requirement = compiledComposition.V2Details!
            .Provenance.ResolvedMap.ImageMap.Applicability.TopologyRequirement;
        if (requirement.Kind == TopologyRequirementKind.None)
        {
            if (selection is not null)
            {
                throw new ArgumentException(
                    "The resolved AB Merge map does not expose a topology selection.",
                    nameof(selection));
            }

            return;
        }

        if (selection is null)
        {
            throw new ArgumentException(
                "The resolved AB Merge map requires an explicit topology selection.",
                nameof(selection));
        }

        if (selection.Source != TopologySelectionSource.Requested ||
            !requirement.Matches(selection))
        {
            throw new ArgumentException(
                "The requested AB Merge topology does not match the resolved profile map.",
                nameof(selection));
        }
    }

    private static void ValidateRuntimeValidationRequirements(CompiledComposition compiledComposition)
    {
        foreach (CompiledValidationRequirement requirement in compiledComposition.ValidationRequirements)
        {
            if (compiledComposition.Authority is LegacyProfileCompilationAuthority or ProfileBundleV2CompilationAuthority &&
                requirement switch
                {
                    CompiledFirmwareConfigBackupVersionValidation =>
                        requirement.Stage == CompiledValidationStage.FinalOutput &&
                        requirement.Severity == CompiledValidationSeverity.Error,
                    CompiledFirmwareConfigBackupPlacementAuthorityValidation =>
                        requirement.Stage == CompiledValidationStage.FinalOutput &&
                        requirement.Severity == CompiledValidationSeverity.Error,
                    CompiledFirmwareConfigBackupExpectedAddressValidation =>
                        requirement.Stage == CompiledValidationStage.FinalOutput &&
                        requirement.Severity == CompiledValidationSeverity.Warning,
                    CompiledUniformInputRangeValidation =>
                        requirement.Stage == CompiledValidationStage.InputLoad &&
                        requirement.Severity is
                            CompiledValidationSeverity.Error or
                            CompiledValidationSeverity.Warning,
                    _ => false,
                })
            {
                continue;
            }

            throw new ArgumentException(
                $"Compiled validation rule '{requirement.RuleId}' is not executable by the current application runtime.",
                nameof(compiledComposition));
        }
    }

    private static bool IsPositiveInteger(string value)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) && parsed > 0;
    }
}

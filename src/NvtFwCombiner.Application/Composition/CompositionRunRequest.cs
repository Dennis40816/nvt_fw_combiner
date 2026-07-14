using System.Globalization;
using System.Collections.ObjectModel;
using NvtFwCombiner.Domain.Composition;

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
        IcNumberSelection? icNumberSelection = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(compiledComposition);
        ArgumentNullException.ThrowIfNull(artifactBindings);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFileName);
        Dictionary<string, InputArtifactBinding> copiedBindings = CopyBindings(artifactBindings);
        ValidateOutputFileName(outputFileName);
        ValidateExecutableComposition(compiledComposition);
        ValidateRuntimeValidationRequirements(compiledComposition);
        ValidateIcNumberSelection(compiledComposition, icNumberSelection);
        ValidateV2RuntimeRequest(compiledComposition, copiedBindings, outputFileName);

        RunId = runId;
        CompiledComposition = compiledComposition;
        ArtifactBindings = new ReadOnlyDictionary<string, InputArtifactBinding>(copiedBindings);
        OutputFileName = outputFileName;
        ApprovedPreviewToken = string.IsNullOrWhiteSpace(approvedPreviewToken) ? null : approvedPreviewToken;
        IcNumberSelection = icNumberSelection;
    }

    /// <summary>Stable run id for reports and diagnostics.</summary>
    public string RunId { get; }

    /// <summary>Atomic compiler output containing identity, policy, and the sole execution plan.</summary>
    public CompiledComposition CompiledComposition { get; }

    /// <summary>Maps required address-space ids to copied artifact bindings.</summary>
    public IReadOnlyDictionary<string, InputArtifactBinding> ArtifactBindings { get; }

    /// <summary>Output file name proposed by profile naming policy or caller override.</summary>
    public string OutputFileName { get; }

    /// <summary>Preview token that authorizes a matching build request.</summary>
    public string? ApprovedPreviewToken { get; }

    /// <summary>IC number selected for Replace profile binding.</summary>
    public IcNumberSelection? IcNumberSelection { get; }

    /// <summary>Returns a copy of this request with a preview token approved for build.</summary>
    public CompositionRunRequest WithApprovedPreviewToken(string previewToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(previewToken);
        return new CompositionRunRequest(
            RunId,
            CompiledComposition,
            ArtifactBindings.Values,
            OutputFileName,
            previewToken,
            IcNumberSelection);
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

        throw new ArgumentException(
            "Compiled composition is not executable by the current application runtime.",
            nameof(compiledComposition));
    }

    private static void ValidateV2RuntimeRequest(
        CompiledComposition compiledComposition,
        Dictionary<string, InputArtifactBinding> bindings,
        string outputFileName)
    {
        if (compiledComposition.Authority is not ProfileBundleV2CompilationAuthority)
        {
            return;
        }

        V2CompiledCompositionDetails details = compiledComposition.V2Details ?? throw new ArgumentException(
            "V2 runtime artifacts require compiled V2 details.",
            nameof(compiledComposition));
        CompiledOutputNamingRequirement output = details.OutputNamingRequirement;
        if (output.RequiredTokenIds.Count != 0 ||
            output.InvalidCharacterPolicy != CompiledOutputInvalidCharacterPolicy.Reject)
        {
            throw new ArgumentException(
                "V2 runtime artifacts require a token-free reject output template until token rendering is available.",
                nameof(compiledComposition));
        }

        CompiledOutputNamingRequirement.ValidateRuntimeLiteralFileName(outputFileName, nameof(outputFileName));

        if (!output.AllowOverride &&
            !string.Equals(outputFileName, output.FileNameTemplate, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Output file name must match the compiled V2 template when overrides are forbidden.",
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
        foreach (CompiledInputSpaceBinding expected in expectedBindings)
        {
            if (!bindings.TryGetValue(expected.AddressSpaceId, out InputArtifactBinding? binding))
            {
                throw new ArgumentException(
                    $"V2 runtime requires an artifact binding for address space '{expected.AddressSpaceId}'.",
                    nameof(bindings));
            }

            CompiledInputSlotRequirement slot = slots[expected.SlotId];
            if (expected.InstancePolicy != CompiledInputInstancePolicy.Singleton ||
                !slot.Required ||
                slot.Cardinality != CompiledInputSlotCardinality.ExactlyOne ||
                binding.ArtifactClass != slot.ArtifactClass ||
                binding.OriginalFileName is null)
            {
                throw new ArgumentException(
                    $"V2 runtime binding '{expected.AddressSpaceId}' does not satisfy the compiled singleton slot contract.",
                    nameof(bindings));
            }

            string extension = Path.GetExtension(binding.OriginalFileName);
            if (!slot.AcceptedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"V2 runtime binding '{expected.AddressSpaceId}' has an unaccepted original file extension.",
                    nameof(bindings));
            }
        }
    }

    private static void ValidateRuntimeValidationRequirements(CompiledComposition compiledComposition)
    {
        foreach (CompiledValidationRequirement requirement in compiledComposition.ValidationRequirements)
        {
            if (compiledComposition.Authority is LegacyProfileCompilationAuthority &&
                requirement is CompiledFirmwareConfigBackupVersionValidation &&
                requirement.Stage == CompiledValidationStage.FinalOutput &&
                requirement.Severity == CompiledValidationSeverity.Error)
            {
                continue;
            }

            throw new ArgumentException(
                $"Compiled validation rule '{requirement.RuleId}' is not executable by the current application runtime.",
                nameof(compiledComposition));
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

    private static bool IsPositiveInteger(string value)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) && parsed > 0;
    }
}

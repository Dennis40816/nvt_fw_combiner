using System.Globalization;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Application request for previewing or building a compiled composition profile.</summary>
public sealed class CompositionRunRequest
{
    private readonly Dictionary<string, InputArtifactBinding> _artifactBindings;

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
        ValidateOutputFileName(outputFileName);
        ValidateExecutableComposition(compiledComposition);
        ValidateIcNumberSelection(compiledComposition, icNumberSelection);

        RunId = runId;
        CompiledComposition = compiledComposition;
        _artifactBindings = CopyBindings(artifactBindings);
        OutputFileName = outputFileName;
        ApprovedPreviewToken = string.IsNullOrWhiteSpace(approvedPreviewToken) ? null : approvedPreviewToken;
        IcNumberSelection = icNumberSelection;
    }

    /// <summary>Stable run id for reports and diagnostics.</summary>
    public string RunId { get; }

    /// <summary>Atomic compiler output containing identity, policy, and the sole execution plan.</summary>
    public CompiledComposition CompiledComposition { get; }

    /// <summary>Maps required address-space ids to copied artifact bindings.</summary>
    public IReadOnlyDictionary<string, InputArtifactBinding> ArtifactBindings => _artifactBindings;

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
            _artifactBindings.Values,
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
        if (compiledComposition.Eligibility != CompiledCompositionEligibility.LegacyRuntimeExecutable ||
            compiledComposition.Authority is not LegacyProfileCompilationAuthority)
        {
            throw new ArgumentException(
                "Compiled composition is not executable by the current application runtime.",
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

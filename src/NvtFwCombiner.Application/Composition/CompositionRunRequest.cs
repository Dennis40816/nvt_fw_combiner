using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Application request for previewing or building a compiled composition profile.</summary>
public sealed class CompositionRunRequest
{
    private readonly Dictionary<string, InputArtifactBinding> _artifactBindings;

    /// <summary>Creates a run request with typed profile, plan, input bindings, and output name.</summary>
    public CompositionRunRequest(
        string runId,
        CompositionRunProfile profile,
        CompositionPlan plan,
        IEnumerable<InputArtifactBinding> artifactBindings,
        string outputFileName,
        string? approvedPreviewToken = null,
        IcNumberSelection? icNumberSelection = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(artifactBindings);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFileName);
        ValidateOutputFileName(outputFileName);
        ValidateProfileMatchesPlan(profile, plan);
        ValidateIcNumberSelection(profile, icNumberSelection);

        RunId = runId;
        Profile = profile;
        Plan = plan;
        _artifactBindings = CopyBindings(artifactBindings);
        OutputFileName = outputFileName;
        ApprovedPreviewToken = string.IsNullOrWhiteSpace(approvedPreviewToken) ? null : approvedPreviewToken;
        IcNumberSelection = icNumberSelection;
    }

    /// <summary>Stable run id for reports and diagnostics.</summary>
    public string RunId { get; }

    /// <summary>Profile metadata used for report generation.</summary>
    public CompositionRunProfile Profile { get; }

    /// <summary>Compiled plan to execute.</summary>
    public CompositionPlan Plan { get; }

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
            Profile,
            Plan,
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

    private static void ValidateProfileMatchesPlan(CompositionRunProfile profile, CompositionPlan plan)
    {
        CompositionPlanProvenance? provenance = plan.Provenance;
        if (provenance is null)
        {
            return;
        }

        if (!string.Equals(profile.ProfileId, provenance.ProfileId, StringComparison.Ordinal) ||
            !string.Equals(profile.ProfileVersion, provenance.ProfileVersion, StringComparison.Ordinal) ||
            !string.Equals(profile.IcId, provenance.IcId, StringComparison.Ordinal) ||
            !string.Equals(profile.ModeId, provenance.ModeId, StringComparison.Ordinal) ||
            !string.Equals(profile.ExperienceId, provenance.ExperienceId, StringComparison.Ordinal) ||
            profile.CompositionKind != provenance.CompositionKind)
        {
            throw new ArgumentException("Run profile metadata must match compiled plan provenance.", nameof(profile));
        }
    }

    private static void ValidateIcNumberSelection(
        CompositionRunProfile profile,
        IcNumberSelection? selection)
    {
        if (profile.CompositionKind != CompositionKind.Replace)
        {
            if (selection is not null)
            {
                throw new ArgumentException("IC number selection is allowed only for Replace runs.", nameof(selection));
            }

            return;
        }

        if (profile.IcNumberInputMode is null)
        {
            throw new ArgumentException("Replace run profile must declare an IC number input mode.", nameof(profile));
        }

        if (selection is null)
        {
            throw new ArgumentException("Replace runs require an IC number selection.", nameof(selection));
        }

        if (profile.IcNumberInputMode == IcNumberInputMode.NumericSelector)
        {
            throw new ArgumentException("Numeric IC number input mode is reserved and is not enabled yet.", nameof(profile));
        }

        if (selection.Mode != profile.IcNumberInputMode)
        {
            throw new ArgumentException(
                "IC number selection mode must match the run profile IC number input mode.",
                nameof(selection));
        }
    }
}

namespace NvtFwCombiner.Domain.Composition;

/// <summary>Atomic compiler output containing one executable plan and its run identity.</summary>
public sealed partial class CompiledComposition
{
    private CompiledComposition(
        CompositionPlan plan,
        LegacyCompiledCompositionIdentity identity,
        string defaultOutputFileName,
        CompiledIcNumberPolicy icNumberPolicy)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(identity);
        ValidateIcNumberPolicy(identity.CompositionKind, icNumberPolicy);
        ValidateDefaultOutputFileName(defaultOutputFileName);

        Plan = plan;
        ProfileId = identity.ProfileId;
        ProfileVersion = identity.ProfileVersion;
        IcId = identity.IcId;
        ModeId = identity.ModeId;
        ExperienceId = identity.ExperienceId;
        CompositionKind = identity.CompositionKind;
        DefaultOutputFileName = defaultOutputFileName;
        IcNumberPolicy = icNumberPolicy;
        Eligibility = CompiledCompositionEligibility.LegacyRuntimeExecutable;
        Authority = new LegacyProfileCompilationAuthority();
        CompilationFingerprint = CalculateCompilationFingerprint(this);
    }

    /// <summary>The sole validated byte-execution plan.</summary>
    public CompositionPlan Plan { get; }

    /// <summary>Stable profile id.</summary>
    public string ProfileId { get; }

    /// <summary>Profile content version.</summary>
    public string ProfileVersion { get; }

    /// <summary>IC id declared by the profile.</summary>
    public string IcId { get; }

    /// <summary>Mode id declared by the profile.</summary>
    public string ModeId { get; }

    /// <summary>Experience id declared by the profile.</summary>
    public string ExperienceId { get; }

    /// <summary>Merge or Replace composition kind.</summary>
    public CompositionKind CompositionKind { get; }

    /// <summary>Profile-rendered default output file name.</summary>
    public string DefaultOutputFileName { get; }

    /// <summary>Compiled IC-number input policy.</summary>
    public CompiledIcNumberPolicy IcNumberPolicy { get; }

    /// <summary>Execution eligibility established by the compiler authority.</summary>
    public CompiledCompositionEligibility Eligibility { get; }

    /// <summary>Authority that established this artifact.</summary>
    public CompositionCompilationAuthority Authority { get; }

    /// <summary>Canonical lowercase SHA-256 over the complete compiled policy and plan.</summary>
    public string CompilationFingerprint { get; }

    /// <summary>Creates an artifact from the existing typed profile compiler without bundle or map claims.</summary>
    internal static CompiledComposition CreateLegacy(
        CompositionPlan plan,
        LegacyCompiledCompositionIdentity identity,
        string defaultOutputFileName,
        CompiledIcNumberPolicy icNumberPolicy)
    {
        return new CompiledComposition(plan, identity, defaultOutputFileName, icNumberPolicy);
    }

    private static void ValidateIcNumberPolicy(
        CompositionKind compositionKind,
        CompiledIcNumberPolicy icNumberPolicy)
    {
        if (!Enum.IsDefined(icNumberPolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(icNumberPolicy),
                icNumberPolicy,
                "Unknown compiled IC-number policy.");
        }

        if (compositionKind == CompositionKind.Merge && icNumberPolicy != CompiledIcNumberPolicy.NotApplicable)
        {
            throw new ArgumentException("Merge compositions cannot accept IC-number input.", nameof(icNumberPolicy));
        }

        if (compositionKind == CompositionKind.Replace && icNumberPolicy == CompiledIcNumberPolicy.NotApplicable)
        {
            throw new ArgumentException("Replace compositions require an IC-number input policy.", nameof(icNumberPolicy));
        }
    }

    private static void ValidateDefaultOutputFileName(string defaultOutputFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultOutputFileName);
        if (defaultOutputFileName.IndexOfAny(['/', '\\', ':']) >= 0 ||
            defaultOutputFileName is "." or ".." ||
            defaultOutputFileName.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Default output file name must be a plain filename without path or control syntax.",
                nameof(defaultOutputFileName));
        }
    }
}

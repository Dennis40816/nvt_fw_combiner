namespace NvtFwCombiner.Domain.Composition;

/// <summary>Atomic compiler output containing one executable plan and its run identity.</summary>
public sealed partial class CompiledComposition
{
    private CompiledComposition(
        CompositionPlan plan,
        V2CompiledCompositionDetails details,
        CompiledIcNumberPolicy icNumberPolicy,
        CompiledCompositionEligibility eligibility)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(details);

        Plan = plan;
        IcNumberPolicy = icNumberPolicy;
        Eligibility = eligibility;
        V2Details = details;
        IntegrityFingerprint = CalculateIntegrityFingerprint(plan);
        CompilationFingerprint = CalculateCompilationFingerprint(this);
    }

    private CompiledComposition(
        CompiledComposition source,
        string capabilityFingerprint)
    {
        Plan = source.Plan;
        IcNumberPolicy = source.IcNumberPolicy;
        Eligibility = source.Eligibility;
        V2Details = source.V2Details;
        IntegrityFingerprint = source.IntegrityFingerprint;
        CapabilityFingerprint = capabilityFingerprint;
        CompilationFingerprint = CalculateCompilationFingerprint(this);
    }

    /// <summary>The sole validated byte-execution plan.</summary>
    public CompositionPlan Plan { get; }

    /// <summary>Stable profile id.</summary>
    public string ProfileId => V2Details.ProfileId;

    /// <summary>Profile content version.</summary>
    public string ProfileVersion => V2Details.ProfileVersion;

    /// <summary>IC id declared by the profile.</summary>
    public string IcId => V2Details.Provenance.Context.MemberId;

    /// <summary>Mode id declared by the profile.</summary>
    public string ModeId => V2Details.Provenance.Context.ModeId;

    /// <summary>Experience id declared by the profile.</summary>
    public string ExperienceId => V2Details.ExperienceId;

    /// <summary>Merge or Replace composition kind.</summary>
    public CompositionKind CompositionKind => V2Details.CompositionKind;

    /// <summary>Profile-rendered default output file name.</summary>
    public string DefaultOutputFileName => V2Details.OutputNamingRequirement.FileNameTemplate;

    /// <summary>Compiled IC-number input policy.</summary>
    public CompiledIcNumberPolicy IcNumberPolicy { get; }

    /// <summary>Execution eligibility established by the compiler authority.</summary>
    public CompiledCompositionEligibility Eligibility { get; }

    /// <summary>
    /// Whether this profile-bundle candidate is the deliberately narrow AB Code
    /// function-open route: executable product behavior with only golden or
    /// firmware-owner certification debt remaining.
    /// </summary>
    public bool IsV2AbFunctionOpenCandidate =>
        Eligibility == CompiledCompositionEligibility.V2PlanCompiled &&
        V2Details.Provenance.Promotion.Stage == CompiledProfilePromotionStage.ExecutableCandidate &&
        V2Details.Provenance.Context is ResolvedMapV2CompilationContext &&
        CompositionKind == CompositionKind.Merge &&
        V2Details.OutputNamingRequirement.RendererKind == CompiledOutputNameRendererKind.AbCodeV1 &&
        V2Details.Provenance.Promotion.Blockers.Count != 0 &&
        V2Details.Provenance.Promotion.Blockers.All(static blocker =>
            blocker.Kind is CompiledProfilePromotionBlockerKind.Golden or
                CompiledProfilePromotionBlockerKind.HumanReview);

    /// <summary>
    /// Whether this trusted V2 artifact is an AB Merge route executable by the
    /// current runtime, either as the narrow function-open candidate or as a
    /// fully supported profile.
    /// </summary>
    public bool IsV2AbMergeRuntimeRoute =>
        (Eligibility == CompiledCompositionEligibility.V2RuntimeExecutable ||
         IsV2AbFunctionOpenCandidate) &&
        V2Details.Provenance.Context is ResolvedMapV2CompilationContext &&
        CompositionKind == CompositionKind.Merge &&
        V2Details.OutputNamingRequirement.RendererKind == CompiledOutputNameRendererKind.AbCodeV1;

    /// <summary>Paired trusted profile-bundle provenance and compiled requirements.</summary>
    public V2CompiledCompositionDetails V2Details { get; }

    /// <summary>Closed validation requirements retained by the compiler authority.</summary>
    public IReadOnlyList<CompiledValidationRequirement> ValidationRequirements =>
        V2Details.Provenance.ValidationRequirements;

    /// <summary>Canonical lowercase SHA-256 over the complete compiled policy and plan.</summary>
    public string CompilationFingerprint { get; }

    /// <summary>Reviewed capability definition chained into this canonical compilation; null before catalog binding.</summary>
    public string? CapabilityFingerprint { get; }

    /// <summary>
    /// Canonical lowercase SHA-256 over profile-declared external processors and
    /// host-side scalar relocation operations; null when neither is present.
    /// </summary>
    public string? IntegrityFingerprint { get; }

    /// <summary>Returns the immutable canonical artifact whose compilation identity references one reviewed capability.</summary>
    public CompiledComposition BindCapabilityFingerprint(
        string capabilityFingerprint)
    {
        string acceptedFingerprint = CanonicalSha256.Require(
            capabilityFingerprint,
            nameof(capabilityFingerprint));

        return CapabilityFingerprint is not null
            ? StringComparer.Ordinal.Equals(
                    CapabilityFingerprint,
                    acceptedFingerprint)
                ? this
                : throw new InvalidOperationException(
                    "A compiled composition cannot be rebound to another capability definition.")
            : new CompiledComposition(this, acceptedFingerprint);
    }

    /// <summary>Creates a complete but non-executable profile-bundle-v2 plan artifact.</summary>
    internal static CompiledComposition CreateV2(
        CompositionPlan plan,
        V2CompiledCompositionDetails details,
        CompiledIcNumberPolicy icNumberPolicy)
    {
        return new CompiledComposition(plan, details, icNumberPolicy, CompiledCompositionEligibility.V2PlanCompiled);
    }

    /// <summary>Creates a trusted profile-bundle-v2 artifact admitted to the current closed runtime subset.</summary>
    internal static CompiledComposition CreateV2RuntimeExecutable(
        CompositionPlan plan,
        V2CompiledCompositionDetails details,
        CompiledIcNumberPolicy icNumberPolicy)
    {
        return new CompiledComposition(plan, details, icNumberPolicy, CompiledCompositionEligibility.V2RuntimeExecutable);
    }
}

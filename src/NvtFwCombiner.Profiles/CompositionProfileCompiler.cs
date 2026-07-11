using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Compiles typed profile definitions and request mapping overlays into domain composition plans.</summary>
public static partial class CompositionProfileCompiler
{
    private const string CtrlRamReplaceExperienceId = IcWorkflowIds.CtrlRamReplace;
    private const string GeneralReplaceExperienceId = IcWorkflowIds.GeneralReplace;
    private const string StandardMergeExperienceId = IcWorkflowIds.StandardMerge;
    private const string CtrlRamClassificationTag = "tp-ctrlram";
    private const string TpClassificationTag = "tp";
    private const string TpClassificationTagPrefix = "tp-";

    /// <summary>Compiles a profile and optional explicit mappings into a validated plan.</summary>
    public static ProfileCompileResult Compile(
        CompositionProfileDefinition profile,
        IReadOnlyList<ExplicitMapping> explicitMappings,
        IReadOnlyList<AddressSpace>? requestAddressSpaces = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(explicitMappings);
        requestAddressSpaces ??= [];

        List<CompositionIssue> issues = ValidateProfileHeader(profile, explicitMappings, requestAddressSpaces);
        issues.AddRange(ValidateProfileOperations(profile));
        issues.AddRange(ValidateExplicitMappings(profile, explicitMappings));
        if (issues.Count > 0)
        {
            return ProfileCompileResult.Failed(issues);
        }

        try
        {
            CompositionOperation[] operations = [
                .. profile.Operations,
                .. explicitMappings.Select(mapping => CompileExplicitMapping(profile, mapping)),
            ];
            AddressSpace[] addressSpaces = [
                .. profile.AddressSpaces,
                .. requestAddressSpaces,
            ];
            var identity = new LegacyCompiledCompositionIdentity(
                profile.ProfileId,
                profile.ProfileVersion,
                profile.IcId,
                profile.ModeId,
                profile.ExperienceId,
                profile.CompositionKind);
            var plan = new CompositionPlan(profile.Initialization, addressSpaces, operations);
            var compiledComposition = CompiledComposition.CreateLegacy(
                plan,
                identity,
                profile.DefaultOutputFileName,
                CompileIcNumberPolicy(profile.IcNumberInputMode));
            return ProfileCompileResult.Succeeded(compiledComposition);
        }
        catch (ArgumentException exception)
        {
            return ProfileCompileResult.Failed([
                new CompositionIssue("profile.plan.invalid", exception.Message),
            ]);
        }
    }

    private static CompiledIcNumberPolicy CompileIcNumberPolicy(IcNumberInputMode? inputMode)
    {
        return inputMode switch
        {
            null => CompiledIcNumberPolicy.NotApplicable,
            IcNumberInputMode.SingleSelector => CompiledIcNumberPolicy.SingleSelector,
            IcNumberInputMode.CascadeSelector => CompiledIcNumberPolicy.CascadeSelector,
            IcNumberInputMode.NumericSelector => CompiledIcNumberPolicy.NumericSelector,
            _ => throw new InvalidOperationException("Profile IC-number mode was not validated."),
        };
    }
}

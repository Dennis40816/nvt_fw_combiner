using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    internal static CompiledComposition CompileAbRuntimeReferenceReplace(V2RuntimeReferenceBankReplacePlan prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        CompiledReferenceBank[] banks = [.. prepared.Banks.Select(static bank => new CompiledReferenceBank(
            bank.BankInstanceId, bank.WorkspaceId, bank.OutputRange, bank.Reference.Identity, bank.LocalComposition))];
        var context = new RuntimeReferenceBankReplaceV2CompilationContext(prepared.AbLayout, prepared.Reference.Identity,
            prepared.Definition, prepared.Plan, banks);
        BankReferenceReplaceDefinition definition = context.Definition;
        V2CompiledCompositionDetails local = banks[0].LocalComposition.V2Details;
        V2CompilationProvenance layoutSource = prepared.AbLayout.V2Details.Provenance;
        var promotion = new CompiledProfilePromotion(CompiledProfilePromotionStage.ExecutableCandidate,
        [
            new CompiledProfilePromotionBlocker("ab-replace-golden", CompiledProfilePromotionBlockerKind.Golden,
                "Independent AB Replace output certification remains required.", []),
            new CompiledProfilePromotionBlocker("ab-replace-owner", CompiledProfilePromotionBlockerKind.HumanReview,
                "Firmware-owner certification of this exact AB Replace release candidate remains required.", []),
        ]);
        var provenance = new V2CompilationProvenance(layoutSource.Bundle, layoutSource.ProfileEntry, context,
            promotion, layoutSource.ProfileEvidenceRefs,
            banks.SelectMany(bank => bank.LocalComposition.V2Details.Provenance.ValidationRequirements
                .Select(validation => new CompiledBankScopedValidation(bank, validation))), []);
        var inputs = new CompiledInputContract(local.InputContract.Slots.Select(slot =>
            slot.ArtifactClass == CompiledInputArtifactClass.ReferenceImage
                ? slot.ResolveCompositeReferenceCapacity(prepared.Reference.LengthBytes) : slot), local.InputContract.SpaceBindings);
        return CompiledComposition.CreateV2(prepared.Plan, new V2CompiledCompositionDetails(
            definition.DefinitionId, definition.Version, ExperienceIds.CtrlRamReplace, CompositionKind.Replace,
            provenance, inputs, prepared.AbLayout.V2Details.RegionAccessContract, local.OutputNamingRequirement,
            local.IcNumberInputMode));
    }
}

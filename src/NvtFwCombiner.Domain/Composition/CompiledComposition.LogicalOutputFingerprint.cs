using System.Security.Cryptography;
using System.Text;
using static NvtFwCombiner.Domain.Firmware.FirmwareFingerprintWriter;

namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompiledComposition
{
    private const string LogicalOutputV2FingerprintFormat = "nfc.compiled-composition.profile-v2-logical-output.v1";

    private static string CalculateLogicalOutputV2CompilationFingerprint(
        CompiledComposition composition,
        LogicalOutputV2CompilationContext context)
    {
        V2CompiledCompositionDetails details = composition.V2Details ?? throw new InvalidOperationException(
            "Profile-bundle-v2 artifacts require paired v2 details.");
        V2CompilationProvenance provenance = details.Provenance;
        CompiledOutputNamingRequirement output = details.OutputNamingRequirement;
        var builder = new StringBuilder();
        AppendField(builder, "format", LogicalOutputV2FingerprintFormat);
        AppendField(builder, "authority.kind", "profile-bundle-v2");
        AppendField(
            builder,
            "authority.model-version",
            ((ProfileBundleV2CompilationAuthority)composition.Authority).ModelVersion);
        AppendField(builder, "profile.id", composition.ProfileId);
        AppendField(builder, "profile.version", composition.ProfileVersion);
        AppendField(builder, "profile.ic", composition.IcId);
        AppendField(builder, "profile.mode", composition.ModeId);
        AppendField(builder, "profile.experience", composition.ExperienceId);
        AppendEnum(builder, "profile.composition-kind", composition.CompositionKind);
        AppendEnum(builder, "run-policy.ic-number", composition.IcNumberPolicy);
        AppendEnum(builder, "eligibility", composition.Eligibility);
        AppendField(builder, "bundle.id", provenance.Bundle.BundleId);
        AppendField(builder, "bundle.version", provenance.Bundle.BundleVersion);
        AppendField(builder, "bundle.content-hash", provenance.Bundle.ContentHash);
        AppendField(builder, "bundle.trust-anchor-binding-id", provenance.Bundle.TrustAnchorBindingId);
        AppendField(builder, "profile-entry.id", provenance.ProfileEntry.EntryId);
        AppendField(builder, "profile-entry.content-hash", provenance.ProfileEntry.ContentHash);
        AppendField(builder, "logical.family.id", context.FamilyId);
        AppendField(builder, "logical.family.version", context.FamilyVersion);
        AppendField(builder, "logical.family.content-hash", context.FamilyContentHash);
        AppendField(builder, "logical.member", context.MemberId);
        AppendField(builder, "logical.mode", context.ModeId);
        AppendEnum(builder, "promotion.stage", provenance.Promotion.Stage);
        AppendInteger(builder, "promotion.blocker.count", provenance.Promotion.Blockers.Count);
        for (int index = 0; index < provenance.Promotion.Blockers.Count; index++)
        {
            CompiledProfilePromotionBlocker blocker = provenance.Promotion.Blockers[index];
            string prefix = FormattableString.Invariant($"promotion.blocker.{index}");
            AppendField(builder, $"{prefix}.id", blocker.BlockerId);
            AppendEnum(builder, $"{prefix}.kind", blocker.Kind);
            AppendField(builder, $"{prefix}.reason", blocker.Reason);
            AppendStringList(builder, $"{prefix}.evidence", blocker.EvidenceRefs);
        }

        AppendStringList(builder, "profile.evidence", provenance.ProfileEvidenceRefs);
        AppendValidationRequirements(builder, provenance.ValidationRequirements);
        AppendCapabilityAdmissions(builder, provenance.RequiredCapabilities);
        AppendInputContract(builder, details.InputContract);
        AppendRegionAccessContract(builder, details.RegionAccessContract);
        AppendField(builder, "output.template", output.FileNameTemplate);
        AppendInteger(builder, "output.allow-override", output.AllowOverride ? 1 : 0);
        AppendEnum(builder, "output.invalid-character-policy", output.InvalidCharacterPolicy);
        AppendStringList(builder, "output.required-token", output.RequiredTokenIds);
        AppendPlan(builder, composition.Plan);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();
    }
}

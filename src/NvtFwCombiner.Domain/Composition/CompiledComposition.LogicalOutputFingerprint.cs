using System.Text;
using static NvtFwCombiner.Domain.Firmware.FirmwareFingerprintWriter;

namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompiledComposition
{
    private const string LogicalOutputV2FingerprintFormat = "nfc.compiled-composition.profile-v2-logical-output.v1";
    private const string CapabilityBoundLogicalOutputV2FingerprintFormat =
        "nfc.compiled-composition.profile-v2-logical-output.v2";

    private static string CalculateLogicalOutputV2CompilationFingerprint(
        CompiledComposition composition,
        LogicalOutputV2CompilationContext context)
    {
        V2CompiledCompositionDetails details = composition.V2Details ?? throw new InvalidOperationException(
            "Profile-bundle-v2 artifacts require paired v2 details.");
        V2CompilationProvenance provenance = details.Provenance;
        var builder = new StringBuilder();
        AppendField(
            builder,
            "format",
            composition.CapabilityFingerprint is null
                ? LogicalOutputV2FingerprintFormat
                : CapabilityBoundLogicalOutputV2FingerprintFormat);
        AppendCapabilityFingerprint(builder, composition);
        AppendV2ProfileIdentity(builder, composition);
        AppendV2ProfileAdmission(builder, composition, provenance);
        AppendField(builder, "logical.family.id", context.FamilyId);
        AppendField(builder, "logical.family.version", context.FamilyVersion);
        AppendField(builder, "logical.family.content-hash", context.FamilyContentHash);
        AppendField(builder, "logical.member", context.MemberId);
        AppendField(builder, "logical.mode", context.ModeId);
        return CompleteV2Fingerprint(builder, composition, details);
    }
}

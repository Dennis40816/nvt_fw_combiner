using System.Text;
using static NvtFwCombiner.Domain.Firmware.FirmwareFingerprintWriter;

namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompiledComposition
{
    private static void AppendBankReferenceContext(StringBuilder builder, RuntimeReferenceBankReplaceV2CompilationContext context)
    {
        AppendField(builder, "bank-replace.definition", context.Definition.ContentHash);
        AppendField(builder, "bank-replace.layout", context.LayoutComposition.CompilationFingerprint);
        AppendField(builder, "bank-replace.reference.id", context.Reference.ArtifactId);
        AppendField(builder, "bank-replace.reference.sha256", context.Reference.Sha256);
        AppendInteger(builder, "bank-replace.reference.length", context.Reference.LengthBytes);
        AppendInteger(builder, "bank-replace.bank.count", context.Banks.Count);
        for (int index = 0; index < context.Banks.Count; index++)
        {
            CompiledReferenceBank bank = context.Banks[index];
            string prefix = FormattableString.Invariant($"bank-replace.bank.{index}");
            AppendField(builder, prefix + ".id", bank.BankId);
            AppendField(builder, prefix + ".workspace", bank.WorkspaceId);
            AppendRange(builder, prefix + ".output", bank.OutputRange);
            AppendField(builder, prefix + ".reference.id", bank.Reference.ArtifactId);
            AppendField(builder, prefix + ".reference.sha256", bank.Reference.Sha256);
            AppendInteger(builder, prefix + ".reference.length", bank.Reference.LengthBytes);
            AppendField(builder, prefix + ".parent", bank.LocalComposition.CompilationFingerprint);
        }
    }
}

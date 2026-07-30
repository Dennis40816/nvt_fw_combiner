using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompiledCompositionTests
{
    /// <summary>Typed rule sources and binding identities participate in compiled identity.</summary>
    [Fact]
    public void TypedOutputNamingAuthorityChangesCompilationFingerprint()
    {
        CompiledComposition first = CreateV2(
            outputNaming: NormalFlashCodeOutput());
        CompiledComposition changedBinding = CreateV2(
            outputNaming: NormalFlashCodeOutput(
                dpBindingId: "other-dp-inspection"));
        CompiledComposition changedSpace = CreateV2(
            outputNaming: NormalFlashCodeOutput(
                dpSpaceId: "other-source"));
        CompiledComposition changedArtifact = CreateV2(
            outputNaming: TpFirmwareOutput());

        Assert.NotEqual(
            first.CompilationFingerprint,
            changedBinding.CompilationFingerprint);
        Assert.NotEqual(
            first.CompilationFingerprint,
            changedSpace.CompilationFingerprint);
        Assert.NotEqual(
            first.CompilationFingerprint,
            changedArtifact.CompilationFingerprint);
    }

    private static CompiledOutputNamingRequirement NormalFlashCodeOutput(
        string dpBindingId = "dp-inspection",
        string dpSpaceId = "source")
    {
        return new CompiledOutputNamingRequirement(
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            allowOverride: true,
            CompiledOutputInvalidCharacterPolicy.Reject,
            ["date", "dp-version", "ic", "tp-version"],
            CompiledOutputNamingRequirement.NormalFlashCodeV1RuleId,
            CompiledOutputArtifactType.FlashCode,
            [
                new CompiledOutputTokenRequirement(
                    "date",
                    CompiledOutputTokenSourceKind.RunDateUtc,
                    metadataBindingId: null,
                    CompiledOutputTokenMissingPolicy.Block,
                    placeholder: null),
                new CompiledOutputTokenRequirement(
                    "dp-version",
                    CompiledOutputTokenSourceKind.DpcmiVersion,
                    dpBindingId,
                    CompiledOutputTokenMissingPolicy.UsePlaceholder,
                    "xxxx",
                    dpSpaceId),
                new CompiledOutputTokenRequirement(
                    "ic",
                    CompiledOutputTokenSourceKind.CompiledIc,
                    metadataBindingId: null,
                    CompiledOutputTokenMissingPolicy.Block,
                    placeholder: null),
                new CompiledOutputTokenRequirement(
                    "tp-version",
                    CompiledOutputTokenSourceKind.FirmwareConfigTpVersion,
                    "tp-inspection",
                    CompiledOutputTokenMissingPolicy.UsePlaceholder,
                    "xxxx",
                    "source"),
            ]);
    }

    private static CompiledOutputNamingRequirement TpFirmwareOutput()
    {
        return new CompiledOutputNamingRequirement(
            CompiledOutputNamingRequirement.TpFirmwareV1Template,
            allowOverride: true,
            CompiledOutputInvalidCharacterPolicy.Reject,
            ["date", "ic", "tp-version"],
            CompiledOutputNamingRequirement.TpFirmwareV1RuleId,
            CompiledOutputArtifactType.TpFirmware,
            [
                new CompiledOutputTokenRequirement(
                    "date",
                    CompiledOutputTokenSourceKind.RunDateUtc,
                    metadataBindingId: null,
                    CompiledOutputTokenMissingPolicy.Block,
                    placeholder: null),
                new CompiledOutputTokenRequirement(
                    "ic",
                    CompiledOutputTokenSourceKind.CompiledIc,
                    metadataBindingId: null,
                    CompiledOutputTokenMissingPolicy.Block,
                    placeholder: null),
                new CompiledOutputTokenRequirement(
                    "tp-version",
                    CompiledOutputTokenSourceKind.FirmwareConfigTpVersion,
                    "tp-inspection",
                    CompiledOutputTokenMissingPolicy.UsePlaceholder,
                    "xxxx",
                    "source"),
            ]);
    }
}

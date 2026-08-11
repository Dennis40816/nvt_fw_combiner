using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionOutputNameResolverTests
{
    /// <summary>The exact accepted CtrlRAM draft overrides only the compiled TP-version token.</summary>
    [Fact]
    public void CtrlRamVersionDraftUsesTheCompiledNormalRenderer()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledOutputNamingRequirement output = NormalFlashCodeOutput();

        OutputNameResolution resolved = CompiledOutputNameResolver.ResolveNormal(
            "NT51950",
            output,
            output.FileNameTemplate,
            isExplicitOverride: false,
            fixture.AcceptedInspection,
            [fixture.InputSummary],
            RunTime,
            ctrlRamVersionEdit: new CtrlRamFirmwareVersionDraftState(0x80, 0x00));

        Assert.Equal("NT51950_FlashCode_D8205T8000_20260728.bin", resolved.FileName);
        OutputNamingTokenSummary tp = resolved.Summary!.Tokens.Single(static token =>
            token.TokenId == "tp-version");
        Assert.Equal("8000", tp.Value);
        Assert.Equal("accepted-ctrlram-version-draft", tp.ParserId);
        Assert.Empty(resolved.Issues);
    }
}

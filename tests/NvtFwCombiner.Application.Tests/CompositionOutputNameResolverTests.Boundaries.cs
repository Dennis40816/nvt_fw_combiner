using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionOutputNameResolverTests
{
    /// <summary>An absent accepted inspection uses only both profile-declared placeholders.</summary>
    [Fact]
    public void MissingAcceptedInspectionUsesBothCompiledPlaceholders()
    {
        OutputNameResolution resolved = CompiledOutputNameResolver.ResolveNormal(
            "NT51929",
            NormalFlashCodeOutput(),
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            isExplicitOverride: false,
            acceptedInspection: null,
            inputSummaries: [],
            RunTime);

        Assert.Equal(
            "NT51929_FlashCode_DxxxxTxxxx_20260728.bin",
            resolved.FileName);
        Assert.Equal(2, resolved.Issues.Count);
        Assert.All(
            resolved.Issues,
            issue => Assert.Equal("output-naming.metadata-unknown", issue.Code));
    }

    /// <summary>Normal resolution rejects a static contract at its focused boundary.</summary>
    [Fact]
    public void NormalResolutionRejectsStaticRenderer()
    {
        var output = new CompiledOutputNamingRequirement(
            "output.bin",
            allowOverride: true,
            CompiledOutputInvalidCharacterPolicy.Reject,
            requiredTokenIds: []);

        _ = Assert.Throws<ArgumentException>(() =>
            CompiledOutputNameResolver.ResolveNormal(
                "NT51929",
                output,
                "output.bin",
                isExplicitOverride: false,
                acceptedInspection: null,
                inputSummaries: [],
                RunTime));
    }
}

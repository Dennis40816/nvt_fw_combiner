using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests DTO normalization for v2 output naming policy.</summary>
public sealed class CompositionProfileV2OutputNormalizerTests
{
    /// <summary>Verifies both invalid-character policies preserve template and override intent.</summary>
    [Fact]
    public void OutputMapsEveryInvalidCharacterPolicy()
    {
        CompositionProfileOutput reject = CompositionProfileNormalizer.NormalizeOutput(Output("reject"));
        CompositionProfileOutput replace = CompositionProfileNormalizer.NormalizeOutput(
            Output("replace-underscore", allowOverride: true));

        Assert.Equal(CompositionProfileInvalidCharacterPolicy.Reject, reject.InvalidCharacterPolicy);
        Assert.Equal(CompositionProfileInvalidCharacterPolicy.ReplaceUnderscore, replace.InvalidCharacterPolicy);
        Assert.False(reject.AllowOverride);
        Assert.True(replace.AllowOverride);
        Assert.Equal("{original-name}_{version}.bin", replace.FileNameTemplate);
        Assert.Equal(["original-name", "version"], replace.RequiredTokenIds);
    }

    /// <summary>Verifies unknown policy tokens fail at their exact discriminator path.</summary>
    [Fact]
    public void OutputRejectsUnknownPolicyWithPath()
    {
        CompositionProfileNormalizationException exception = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeOutput(Output("future")));

        Assert.Equal("output.invalidCharacterPolicy", exception.Path);
    }

    /// <summary>Verifies missing token arrays retain their exact source path.</summary>
    [Fact]
    public void OutputRejectsMissingTokenArrayWithPath()
    {
        CompositionProfileNormalizationException exception = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeOutput(Output("reject") with { RequiredTokenIds = null! }));

        Assert.Equal("output.requiredTokenIds", exception.Path);
    }

    /// <summary>Verifies constructor-owned template and token invariants stay at the output aggregate path.</summary>
    [Fact]
    public void OutputRejectsInvalidAggregateValuesAtOutputPath()
    {
        CompositionProfileNormalizationException template = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeOutput(Output("reject") with { FileNameTemplate = "" }));
        CompositionProfileNormalizationException token = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeOutput(Output("reject") with { RequiredTokenIds = ["Version"] }));

        Assert.Equal("output", template.Path);
        Assert.Equal("output", token.Path);
        _ = Assert.IsType<ArgumentException>(template.InnerException, exactMatch: false);
        _ = Assert.IsType<ArgumentException>(token.InnerException, exactMatch: false);
    }

    private static CompositionProfileOutputDocument Output(string policy, bool allowOverride = false)
    {
        return new CompositionProfileOutputDocument(
            "{original-name}_{version}.bin",
            allowOverride,
            policy,
            ["version", "original-name"]);
    }
}

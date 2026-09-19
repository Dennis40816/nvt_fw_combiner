using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Tests shared platform identity and destination rules used by preview and commit.</summary>
public sealed class CompositionOutputBundlePlatformTests
{
    /// <summary>Windows components count UTF-16 units, including the filename extension.</summary>
    [Theory]
    [InlineData(255, false)]
    [InlineData(256, false)]
    [InlineData(255, true)]
    [InlineData(256, true)]
    public void SharedNameRulesRejectOnlyOversizedComponents(int length, bool unicode)
    {
        string name = new string(unicode ? '\u8cc7' : 'x', length - 4) + ".bin";
        Assert.Equal(length <= 255 ? null : CompositionOutputBundleValidationIssueCodes.PathTooLong,
            AtomicBundlePathRules.GetWindowsNameIssueCode(name));
    }

    /// <summary>Supplementary Unicode characters use two UTF-16 units and invalid drafts return typed issues.</summary>
    [Theory]
    [InlineData(125, false)]
    [InlineData(126, true)]
    public void SharedNameRulesCountSurrogatePairs(int pairs, bool rejected)
    {
        string name = string.Concat(Enumerable.Repeat("\U0001F600", pairs)) + ".bin";
        var validator = new FileSystemCompositionOutputBundleDestinationValidator();
        Assert.Equal(rejected, validator.ValidateName(name) is not null);
        if (rejected)
        {
            Assert.Contains("255 UTF-16", validator.ValidateName(name)!.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>Blank editor content is data, not an exception from the platform adapter.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void BlankNameDraftProducesTypedFailure(string name)
    {
        var validator = new FileSystemCompositionOutputBundleDestinationValidator();
        Assert.Equal(CompositionOutputBundleValidationIssueCodes.NameInvalid, validator.ValidateName(name)!.Code);
    }

    /// <summary>Reserved, trailing, and traversal names retain stable Application issue codes.</summary>
    [Theory]
    [InlineData("CON", CompositionOutputBundleValidationIssueCodes.NameReserved)]
    [InlineData("bundle.", CompositionOutputBundleValidationIssueCodes.NameInvalid)]
    [InlineData("bundle ", CompositionOutputBundleValidationIssueCodes.NameInvalid)]
    [InlineData("../bundle", CompositionOutputBundleValidationIssueCodes.NameInvalid)]
    public void SharedNameRulesReturnStableIssueCodes(string name, string expectedCode)
    {
        Assert.Equal(expectedCode, AtomicBundlePathRules.GetWindowsNameIssueCode(name));
    }
}

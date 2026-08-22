using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Tests shared platform identity and destination rules used by preview and commit.</summary>
public sealed class CompositionOutputBundlePlatformTests
{
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

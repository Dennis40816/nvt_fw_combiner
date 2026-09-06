using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionRunRequestV2Tests
{
    /// <summary>Static host overrides preserve the existing absent token/clock summary and automatic preparation.</summary>
    [Fact]
    public void BundleStaticOverridePreservesCanonicalPreparation()
    {
        CompiledComposition composition = CreateV2RuntimeExecutable(allowOutputOverride: true);
        OutputNameResolution automatic = OutputNameResolution.Static("v2-output.bin");
        OutputNameResolution effective = automatic.WithExplicitOverride(composition, "customer.bin");
        Assert.Equal("customer.bin", effective.FileName);
        Assert.Null(effective.Summary);
        Assert.Same(automatic.Issues, effective.Issues);
        Assert.Equal("v2-output.bin", automatic.FileName);
    }

    /// <summary>Prepared bundle overrides retain the same compiled policy as direct execution.</summary>
    [Fact]
    public void BundleOverrideCannotBypassCompiledOverrideProhibition()
    {
        CompiledComposition composition = CreateV2RuntimeExecutable(allowOutputOverride: false);
        OutputNameResolution automatic = OutputNameResolution.Static("v2-output.bin");
        _ = Assert.Throws<ArgumentException>(() => automatic.WithExplicitOverride(composition, "customer.bin"));
    }

    /// <summary>Prepared bundle names cannot bypass runtime literal filename admission.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("../outside.bin")]
    [InlineData("C:\\outside.bin")]
    [InlineData("bad?.bin")]
    public void BundleOverrideRejectsNonLiteralFileNames(string name)
    {
        CompiledComposition composition = CreateV2RuntimeExecutable(allowOutputOverride: true);
        OutputNameResolution automatic = OutputNameResolution.Static("v2-output.bin");
        _ = Assert.ThrowsAny<ArgumentException>(() => automatic.WithExplicitOverride(composition, name));
    }
}

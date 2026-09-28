using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Infrastructure.Diagnostics;

namespace NvtFwCombiner.GoldenRegression.Tests;

/// <summary>Golden output tests must run with the build's real accepted admission source.</summary>
public sealed class PrebuiltProfileCatalogAdmissionTests
{
    /// <summary>A successful JSON fallback must not conceal an unused default pack in this host.</summary>
    [Fact]
    public async Task GoldenHostUsesPrebuiltAdmission()
    {
        CapabilityCatalogReloadResult? terminal = null;
        await foreach (CanonicalCapabilityCatalogLoadUpdate update in GoldenTestHost.Services.CanonicalCatalogLoader.LoadAsync(TestContext.Current.CancellationToken))
        {
            terminal = update.Result ?? terminal;
        }
        Assert.True(terminal?.Succeeded);
        Assert.Equal("prebuilt", BuiltInProfileAdmissionStatus.Instance.Current?.SourceToken);
        Assert.Null(BuiltInProfileAdmissionStatus.Instance.Current?.RejectionCode);
    }
}

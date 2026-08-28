using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Owns one isolated Bootstrap graph for one concrete shell test group.</summary>
public sealed class ShellViewModelTestHostFixture
{
    internal CompositionHostServices Services { get; } = CompositionHostServices.Create(
        RetainedDpReplaceRegressionPolicy.Load);
}

/// <summary>Shared smoke-test support; each concrete group owns an isolated host fixture.</summary>
public abstract partial class ShellViewModelTestBase
{
    private protected ShellViewModelTestBase(ShellViewModelTestHostFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        TestHost = fixture.Services;
    }

    private protected CompositionHostServices TestHost { get; }

    private protected CanonicalCapabilityExperience TestProjection =>
        (CanonicalCapabilityExperience)TestHost.CompositionCapabilityExperience;
}

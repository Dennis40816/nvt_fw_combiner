using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Owns one isolated Bootstrap graph for one concrete shell test group.</summary>
public sealed class ShellViewModelTestHostFixture
{
    internal CompositionHostServices Services { get; } = CompositionHostServices.Create();
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
}

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Smoke coverage for shell view-model surfaces used by the Avalonia UI.</summary>
public sealed partial class ShellViewModelTests
{
    private static Bootstrap.CompositionHostServices TestHost { get; } =
        Bootstrap.CompositionHostServices.Create();
}

namespace NvtFwCombiner.Domain.Composition;

/// <summary>Closed authority carried by one compiled composition.</summary>
public abstract class CompositionCompilationAuthority
{
    private protected CompositionCompilationAuthority()
    {
    }
}

/// <summary>Authority produced by the pre-bundle typed profile compiler.</summary>
public sealed class LegacyProfileCompilationAuthority : CompositionCompilationAuthority
{
    internal LegacyProfileCompilationAuthority()
    {
    }

    /// <summary>Exact legacy profile model version represented by this authority.</summary>
    public string ModelVersion { get; } = "0.2";
}

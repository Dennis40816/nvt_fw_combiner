namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Classification of inherited managed-tree lifetime context.</summary>
public enum InheritedManagedProcessLifetimeOutcome
{
    /// <summary>No managed lifetime context was advertised.</summary>
    NotInherited,
    /// <summary>The exact inherited start lease and tree authority were captured.</summary>
    Captured,
    /// <summary>Managed context was advertised but incomplete, malformed, or unusable.</summary>
    InvalidInheritedContext,
}

/// <summary>Exact managed-process role bound to inherited lifetime authority.</summary>
public enum ManagedProcessLifetimeKind
{
    /// <summary>The version-scoped Desktop application tree.</summary>
    Application,
    /// <summary>The version-scoped inner Launcher tree.</summary>
    Launcher,
}

/// <summary>Typed inherited lifetime capture held until the managed process exits.</summary>
public interface IInheritedManagedProcessLifetimeCapture : IDisposable
{
    /// <summary>Gets the exact inherited-context classification.</summary>
    InheritedManagedProcessLifetimeOutcome Outcome { get; }
}

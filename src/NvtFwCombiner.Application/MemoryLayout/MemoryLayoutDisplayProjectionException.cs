namespace NvtFwCombiner.Application.MemoryLayout;

/// <summary>
/// Optional CtrlRAM display annotations cannot bind to canonical geometry.
/// This is not an input, capability identity, compilation, or execution failure.
/// </summary>
public sealed class MemoryLayoutDisplayProjectionException : ArgumentException
{
    /// <summary>Creates a display-only binding diagnostic without changing its parameter evidence.</summary>
    public MemoryLayoutDisplayProjectionException(string message, string paramName)
        : base(message, paramName)
    {
    }
}

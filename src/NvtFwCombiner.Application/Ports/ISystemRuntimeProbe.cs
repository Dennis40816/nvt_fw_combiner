using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Application.Ports;

/// <summary>Reads non-sensitive runtime facts for the current process.</summary>
public interface ISystemRuntimeProbe
{
    /// <summary>Returns one current-machine runtime snapshot without paths or environment variables.</summary>
    SystemRuntimeFacts Probe();
}

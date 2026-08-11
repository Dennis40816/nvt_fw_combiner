using System.Runtime.InteropServices;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.Diagnostics;

/// <summary>Reads the allowlisted non-sensitive runtime facts used by diagnostics.</summary>
public sealed class SystemRuntimeProbe : ISystemRuntimeProbe
{
    /// <inheritdoc />
    public SystemRuntimeFacts Probe()
    {
        return new SystemRuntimeFacts(
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString());
    }
}

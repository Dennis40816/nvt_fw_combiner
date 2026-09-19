using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Bootstrap;

public sealed partial class CompositionHostServices
{
    /// <summary>Handles only the versioned internal runtime trust probe before ordinary host startup.</summary>
    public static bool TryHandleRuntimeTrustProbe(string[] arguments, TextWriter output, out int exitCode)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);
        return RuntimeTrustProbeProcess.TryHandle(arguments, output, out exitCode);
    }
}

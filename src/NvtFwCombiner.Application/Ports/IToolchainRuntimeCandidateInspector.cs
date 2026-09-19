using NvtFwCombiner.Application.Configuration;

namespace NvtFwCombiner.Application.Ports;

/// <summary>Platform-owned executable evidence; probing never installs or selects a candidate.</summary>
public interface IToolchainRuntimeCandidateInspector
{
    /// <summary>Verifies one exact path and returns explicit complete verification evidence.</summary>
    ValueTask<ToolchainRuntimeCandidateInspection> InspectAsync(string path, CancellationToken cancellationToken);
    /// <summary>Reads the host-owned bundled identity; metadata alone does not assert user-candidate verification.</summary>
    ValueTask<ToolchainRuntimeCandidateInspection> InspectBundledAsync(CancellationToken cancellationToken);
    /// <summary>Returns a bounded read-only candidate list using the platform's declared discovery locations.</summary>
    ValueTask<IReadOnlyList<ToolchainRuntimeCandidateInspection>> DetectAsync(CancellationToken cancellationToken);
}

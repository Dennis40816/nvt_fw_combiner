namespace NvtFwCombiner.Application.Configuration;

/// <summary>Host-scoped owner of requested runtime selection and immutable configuration publication.</summary>
public interface IToolchainRuntimeConfigurationSession
{
    /// <summary>Current immutable publication; blocked state has no effective selection.</summary>
    ToolchainRuntimeConfigurationSnapshot Current { get; }
    /// <summary>Reloads stored intent and admits its exact identity without fallback.</summary>
    ValueTask<ToolchainRuntimeConfigurationOperationResult> ReloadAsync(CancellationToken cancellationToken);
    /// <summary>Verifies and persists the exact draft before publishing; failures preserve Current.</summary>
    ValueTask<ToolchainRuntimeConfigurationOperationResult> SaveAsync(ToolchainRuntimeSelection selection, CancellationToken cancellationToken);
    /// <summary>Inspects a candidate without selecting, persisting, or publishing it.</summary>
    ValueTask<ToolchainRuntimeCandidateInspection> InspectAsync(string path, CancellationToken cancellationToken);
    /// <summary>Reads bundled runtime metadata without applying user-candidate trust or changing selection.</summary>
    ValueTask<ToolchainRuntimeCandidateInspection> InspectBundledAsync(CancellationToken cancellationToken);
    /// <summary>Discovers bounded candidates without selecting, persisting, or publishing them.</summary>
    ValueTask<IReadOnlyList<ToolchainRuntimeCandidateInspection>> DetectAsync(CancellationToken cancellationToken);
}

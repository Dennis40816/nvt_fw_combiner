using System.Runtime.InteropServices;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>
/// Re-probes only processor/tool references already selected by one compiled
/// capability. It never infers or grants processor byte authority.
/// </summary>
public sealed class ExternalProcessorRuntimeDependencyInspector :
    IRuntimeDependencyReadinessProvider
{
    private readonly ExternalCombinerToolRegistry _registry;
    private readonly string _toolRoot;
    private readonly string _stagingRoot;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates one refreshable current-machine dependency inspector.</summary>
    public ExternalProcessorRuntimeDependencyInspector(
        ExternalCombinerToolRegistry registry,
        string toolRoot,
        string stagingRoot,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _registry = registry;
        _toolRoot = Path.GetFullPath(toolRoot);
        _stagingRoot = Path.GetFullPath(stagingRoot);
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public ValueTask<RuntimeDependencyReadinessSnapshot> RefreshAsync(
        RuntimeDependencyReadinessRequest request,
        long generation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(generation, 1);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Dependencies.Count == 0)
        {
            return ValueTask.FromResult(CreateSnapshot(request, generation, []));
        }

        CompositionIssue? stagingIssue = ProbeStaging();
        var resolver = new ExternalCombinerToolResolver(_registry, _toolRoot);
        RuntimeDependencyEntry[] entries =
        [
            .. request.Dependencies.Select(dependency =>
                Inspect(dependency, resolver, stagingIssue)),
        ];
        return ValueTask.FromResult(CreateSnapshot(request, generation, entries));
    }

    private RuntimeDependencyReadinessSnapshot CreateSnapshot(
        RuntimeDependencyReadinessRequest request,
        long generation,
        IEnumerable<RuntimeDependencyEntry> entries)
    {
        return new RuntimeDependencyReadinessSnapshot(
            request.RouteId,
            request.CapabilityFingerprint,
            request.CompilationFingerprint,
            request.ResolutionToken,
            request.AuthoringRevision,
            generation,
            _timeProvider.GetUtcNow().ToUniversalTime(),
            entries);
    }

    private static RuntimeDependencyEntry Inspect(
        ExternalProcessorDependencyReference dependency,
        ExternalCombinerToolResolver resolver,
        CompositionIssue? stagingIssue)
    {
        if (stagingIssue is not null)
        {
            return Blocked(dependency, stagingIssue);
        }

        try
        {
            bool resolved = resolver.TryResolve(
                dependency.ToolBindingId,
                out ExternalCombinerToolManifest? manifest,
                out _,
                out CompositionIssue? issue);
            return !resolved
                ? Blocked(dependency, issue!)
                : SupportsCurrentPlatform(manifest!.Platform)
                ? RuntimeDependencyEntry.Ready(
                    dependency.ProcessorId,
                    dependency.ToolBindingId)
                : RuntimeDependencyEntry.Blocked(
                    dependency.ProcessorId,
                    dependency.ToolBindingId,
                    "external-tool.platform.unsupported",
                    "The selected external processor does not support the current platform.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return RuntimeDependencyEntry.Blocked(
                dependency.ProcessorId,
                dependency.ToolBindingId,
                "external-tool.discovery.io-failed",
                $"External processor discovery failed ({exception.GetType().Name}).");
        }
    }

    private CompositionIssue? ProbeStaging()
    {
        string? probePath = null;
        try
        {
            _ = Directory.CreateDirectory(_stagingRoot);
            probePath = Path.Combine(
                _stagingRoot,
                $".readiness-{Guid.NewGuid():N}.tmp");
            using var probe = new FileStream(
                probePath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);
            probe.WriteByte(0);
            return null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return new CompositionIssue(
                "external-tool.staging.unavailable",
                $"External processor staging is unavailable ({exception.GetType().Name}).");
        }
        finally
        {
            if (probePath is not null)
            {
                try
                {
                    File.Delete(probePath);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    // DeleteOnClose is authoritative; a redundant cleanup failure
                    // must not hide the original readiness result.
                }
            }
        }
    }

    private static RuntimeDependencyEntry Blocked(
        ExternalProcessorDependencyReference dependency,
        CompositionIssue issue)
    {
        return RuntimeDependencyEntry.Blocked(
            dependency.ProcessorId,
            dependency.ToolBindingId,
            issue.Code,
            issue.Message);
    }

    private static bool SupportsCurrentPlatform(string platform)
    {
        return OperatingSystem.IsWindows() && platform switch
        {
            "win-x64" => RuntimeInformation.OSArchitecture == Architecture.X64,
            "win-arm64" => RuntimeInformation.OSArchitecture == Architecture.Arm64,
            _ => false,
        };
    }
}

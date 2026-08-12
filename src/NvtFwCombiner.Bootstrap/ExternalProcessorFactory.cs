using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Bootstrap;

internal static class ExternalProcessorFactory
{
    private static readonly ExternalProcessorLifetime ProcessLifetime = new(CreateUncached);

    internal static ExternalProcessorGenerationLease AcquireCurrent()
    {
        return ProcessLifetime.AcquireCurrent();
    }

    internal static bool IsCurrent(long generation)
    {
        return ProcessLifetime.IsCurrent(generation);
    }

    private static ExternalProcessorRuntimeEnvironment CreateUncached()
    {
        string toolRoot = FindExternalToolsRoot() ??
            Path.Combine(AppContext.BaseDirectory, "external-tools");
        List<ExternalCombinerToolManifest> manifests = Directory.Exists(toolRoot)
            ?
            [
                .. Directory.EnumerateFiles(
                        toolRoot,
                        "manifest.json",
                        SearchOption.AllDirectories)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .Select(LoadManifest),
            ]
            : [];
        var registry = new ExternalCombinerToolRegistry(manifests);
        string stagingRoot = Path.Combine(Path.GetTempPath(), "nvt-fw-combiner", "external-tools");
        var readinessProvider = new ExternalProcessorRuntimeDependencyInspector(
            registry,
            toolRoot,
            stagingRoot,
            TimeProvider.System);
        IExternalProcessor? processor = null;
        if (manifests.Count != 0)
        {
            _ = Directory.CreateDirectory(stagingRoot);
            var processRunner = new SystemExternalProcessRunner();
            var legacyPostbuildProcessor = new LegacyCombinerPostbuildProcessor(
                registry,
                toolRoot,
                stagingRoot,
                processRunner);
            var manifestProcessor = new ExternalCombinerProcessor(
                registry,
                toolRoot,
                stagingRoot,
                processRunner,
                ExternalCombinerInvocationCatalog.All);
            processor = new ExternalProcessorRouter(
                legacyPostbuildProcessor,
                manifestProcessor);
        }

        return new ExternalProcessorRuntimeEnvironment(processor, readinessProvider);
    }

    private static string? FindExternalToolsRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "external-tools");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static ExternalCombinerToolManifest LoadManifest(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        return new ExternalCombinerToolManifest(
            RequiredString(root, "schemaVersion", path),
            RequiredString(root, "toolBindingId", path),
            RequiredString(root, "toolId", path),
            RequiredString(root, "toolVersion", path),
            RequiredString(root, "displayName", path),
            RequiredString(root, "platform", path),
            RequiredString(root, "executableName", path),
            RequiredString(root, "sha256", path),
            RequiredString(root, "adapterId", path),
            RequiredString(root, "inputMode", path),
            RequiredStringArray(root, "argumentTemplate", path),
            RequiredString(root, "workingDirectoryPolicy", path),
            RequiredInt32(root, "timeoutSeconds", path),
            RequiredStringArray(root, "allowedExtraOutputFiles", path));
    }

    private static string RequiredString(JsonElement root, string propertyName, string path)
    {
        return root.TryGetProperty(propertyName, out JsonElement property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString()!
                : throw new InvalidDataException($"External tool manifest '{path}' is missing string property '{propertyName}'.");
    }

    private static int RequiredInt32(JsonElement root, string propertyName, string path)
    {
        return root.TryGetProperty(propertyName, out JsonElement property) &&
            property.TryGetInt32(out int value)
                ? value
                : throw new InvalidDataException($"External tool manifest '{path}' is missing integer property '{propertyName}'.");
    }

    private static List<string> RequiredStringArray(JsonElement root, string propertyName, string path)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"External tool manifest '{path}' is missing string array property '{propertyName}'.");
        }

        List<string> values = [];
        foreach (JsonElement item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"External tool manifest '{path}' has a non-string '{propertyName}' entry.");
            }

            values.Add(item.GetString()!);
        }

        return values;
    }
}

internal sealed record ExternalProcessorRuntimeEnvironment(
    IExternalProcessor? Processor,
    IRuntimeDependencyReadinessProvider ReadinessProvider);

internal sealed record ExternalProcessorGenerationLease(
    long Generation,
    IExternalProcessor? Processor,
    IRuntimeDependencyReadinessProvider ReadinessProvider);

internal sealed class ExternalProcessorLifetime
{
    private readonly Lock _gate = new();
    private readonly Func<ExternalProcessorRuntimeEnvironment> _environmentFactory;
    private Lazy<ExternalProcessorRuntimeEnvironment> _environment;
    private long _generation = 1;

    internal ExternalProcessorLifetime(
        Func<ExternalProcessorRuntimeEnvironment> environmentFactory)
    {
        ArgumentNullException.ThrowIfNull(environmentFactory);
        _environmentFactory = environmentFactory;
        _environment = CreateGeneration();
    }

    internal long Generation
    {
        get
        {
            lock (_gate)
            {
                return _generation;
            }
        }
    }

    internal ExternalProcessorGenerationLease AcquireCurrent()
    {
        while (true)
        {
            Lazy<ExternalProcessorRuntimeEnvironment> environment;
            long generation;
            lock (_gate)
            {
                environment = _environment;
                generation = _generation;
            }

            ExternalProcessorRuntimeEnvironment value = environment.Value;
            lock (_gate)
            {
                if (ReferenceEquals(environment, _environment) &&
                    generation == _generation)
                {
                    return new ExternalProcessorGenerationLease(
                        generation,
                        value.Processor,
                        value.ReadinessProvider);
                }
            }
        }
    }

    internal void Refresh()
    {
        lock (_gate)
        {
            _generation = checked(_generation + 1);
            _environment = CreateGeneration();
        }
    }

    internal bool IsCurrent(long generation)
    {
        lock (_gate)
        {
            return generation == _generation;
        }
    }

    private Lazy<ExternalProcessorRuntimeEnvironment> CreateGeneration()
    {
        return new Lazy<ExternalProcessorRuntimeEnvironment>(
            _environmentFactory,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }
}

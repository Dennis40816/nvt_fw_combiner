using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Bootstrap;

internal static class ExternalProcessorFactory
{
    internal static IExternalProcessor? CreateOrNull()
    {
        string? toolRoot = FindExternalToolsRoot();
        if (toolRoot is null)
        {
            return null;
        }

        List<ExternalCombinerToolManifest> manifests = [
            .. Directory.EnumerateFiles(toolRoot, "manifest.json", SearchOption.AllDirectories)
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(LoadManifest),
        ];
        if (manifests.Count == 0)
        {
            return null;
        }

        var registry = new ExternalCombinerToolRegistry(manifests);
        string stagingRoot = Path.Combine(Path.GetTempPath(), "nvt-fw-combiner", "external-tools");
        _ = Directory.CreateDirectory(stagingRoot);
        return new LegacyCombinerPostbuildProcessor(
            registry,
            LegacyCombinerPostbuildCatalog.All,
            toolRoot,
            stagingRoot,
            new SystemExternalProcessRunner());
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

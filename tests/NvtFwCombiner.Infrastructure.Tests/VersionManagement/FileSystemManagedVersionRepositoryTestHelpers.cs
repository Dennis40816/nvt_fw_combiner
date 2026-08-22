using System.Text.Json.Nodes;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class FileSystemManagedVersionRepositoryTests
{
    private static async ValueTask<ManagedLauncherResult> RunWithProbeBehaviorAsync(
        string behavior,
        ManagedActivationCoordinator coordinator)
    {
        const string key = "NVT_READY_PROBE_BEHAVIOR";
        string? prior = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, behavior);
            return await coordinator.RunAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, prior);
        }
    }

    private static void MutateManifest(JsonObject manifest, string mutation)
    {
        JsonArray files = manifest["files"]!.AsArray();
        JsonObject readme = files.Select(node => node!.AsObject())
            .Single(file => file["path"]!.GetValue<string>() == "README.txt");
        switch (mutation)
        {
            case "product":
                manifest["product"] = "Other";
                break;
            case "version":
                manifest["version"] = "0.10.5";
                break;
            case "role":
                readme["role"] = "arbitrary";
                break;
            case "hash":
                readme["sha256"] = new string('0', 64);
                break;
            case "size":
                readme["size"] = 99;
                break;
            case "unknown-field":
                manifest["unexpected"] = true;
                break;
            case "missing-fixed":
            case "extra-file":
                break;
            default:
                throw new InvalidOperationException($"Unknown manifest mutation '{mutation}'.");
        }
    }
}

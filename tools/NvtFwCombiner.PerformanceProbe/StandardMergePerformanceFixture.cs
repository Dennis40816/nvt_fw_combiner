using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace NvtFwCombiner.PerformanceProbe;

internal sealed class StandardMergePerformanceFixture : IDisposable
{
    private readonly JsonDocument _manifest;
    private readonly string _fixtureRoot;
    private readonly JsonElement _case;

    private StandardMergePerformanceFixture(
        JsonDocument manifest,
        string fixtureRoot,
        JsonElement goldenCase,
        string ic)
    {
        _manifest = manifest;
        _fixtureRoot = fixtureRoot;
        _case = goldenCase;
        FullIcId = $"NT{ic}";
        ExpectedOutputSha256 = goldenCase
            .GetProperty("expectedOutput")
            .GetProperty("sha256")
            .GetString() ?? throw new InvalidOperationException("The Standard Merge golden has no output SHA-256.");
        InputSha256 = goldenCase
            .GetProperty("inputs")
            .EnumerateObject()
            .ToDictionary(
                static input => input.Name,
                static input => input.Value.GetProperty("sha256").GetString() ?? string.Empty,
                StringComparer.Ordinal);
    }

    internal string FullIcId { get; }

    internal string ExpectedOutputSha256 { get; }

    internal IReadOnlyDictionary<string, string> InputSha256 { get; }

    internal static StandardMergePerformanceFixture Load(string repositoryRoot, string ic)
    {
        string root = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        try
        {
            JsonElement goldenCase = manifest.RootElement
                .GetProperty("cases")
                .EnumerateArray()
                .Single(candidate => string.Equals(
                    candidate.GetProperty("ic").GetString(),
                    ic,
                    StringComparison.Ordinal));
            var fixture = new StandardMergePerformanceFixture(manifest, root, goldenCase, ic);
            fixture.ValidateFiles();
            return fixture;
        }
        catch
        {
            manifest.Dispose();
            throw;
        }
    }

    internal IReadOnlyDictionary<string, string> CopyInputsTo(string destinationRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty input in _case.GetProperty("inputs").EnumerateObject())
        {
            string source = ResolveManifestPath(input.Value);
            string destination = Path.Combine(destinationRoot, $"{input.Name}.bin");
            File.Copy(source, destination);
            result.Add(SlotIdForAddressSpace(input.Name), destination);
        }

        return result;
    }

    public void Dispose()
    {
        _manifest.Dispose();
    }

    private void ValidateFiles()
    {
        foreach (JsonProperty input in _case.GetProperty("inputs").EnumerateObject())
        {
            ValidateFile(input.Value, $"Standard Merge {input.Name}");
        }

        ValidateFile(_case.GetProperty("expectedOutput"), "Standard Merge expected output");
    }

    private void ValidateFile(JsonElement entry, string label)
    {
        string path = ResolveManifestPath(entry);
        long expectedSize = entry.GetProperty("size").GetInt64();
        string expectedHash = entry.GetProperty("sha256").GetString() ?? string.Empty;
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != expectedSize)
        {
            throw new InvalidOperationException($"{label} is missing or has an unexpected size.");
        }

        using FileStream stream = info.OpenRead();
        string actualHash = Convert.ToHexStringLower(SHA256.HashData(stream));
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{label} SHA-256 does not match its manifest.");
        }
    }

    private string ResolveManifestPath(JsonElement entry)
    {
        string relative = entry.GetProperty("path").GetString() ??
            throw new InvalidOperationException("A Standard Merge manifest entry has no path.");
        string fullPath = Path.GetFullPath(Path.Combine(_fixtureRoot, relative));
        return fullPath.StartsWith(
            _fixtureRoot + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : throw new InvalidOperationException("A Standard Merge manifest path escapes its fixture root.");
    }

    private static string SlotIdForAddressSpace(string addressSpaceId)
    {
        return addressSpaceId switch
        {
            "dp-input" => "merge-dp",
            "tp-input" => "merge-tp",
            "ld-input" => "merge-ld",
            _ => throw new InvalidOperationException($"Unknown Standard Merge input '{addressSpaceId}'."),
        };
    }
}

internal static class RepositoryLocator
{
    internal static string FindRoot()
    {
        string? current = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "NvtFwCombiner.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException("Could not locate the NVT FW Combiner repository root.");
    }
}

internal sealed class TemporaryProbeWorkspace : IDisposable
{
    private TemporaryProbeWorkspace(string root)
    {
        Root = root;
    }

    internal string Root { get; }

    internal static TemporaryProbeWorkspace Create()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"nvt-fw-combiner-performance-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(root);
        return new TemporaryProbeWorkspace(root);
    }

    public void Dispose()
    {
        string fullRoot = Path.GetFullPath(Root);
        string fullTemp = Path.GetFullPath(Path.GetTempPath());
        if (!fullRoot.StartsWith(fullTemp, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(fullRoot).StartsWith("nvt-fw-combiner-performance-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to remove an unexpected performance-probe directory.");
        }

        if (Directory.Exists(fullRoot))
        {
            Directory.Delete(fullRoot, recursive: true);
        }
    }
}

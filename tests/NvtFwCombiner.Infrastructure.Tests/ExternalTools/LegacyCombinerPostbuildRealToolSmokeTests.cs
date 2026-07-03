using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Windows-only smoke tests for the committed legacy Combiner.exe binding.</summary>
public sealed class LegacyCombinerPostbuildRealToolSmokeTests
{
    /// <summary>Verifies the real Combiner.exe can run a golden-backed NT51927 CRC-only command through the host adapter.</summary>
    [Fact]
    public async Task RealToolRunsNt51927GoldenCrcOnlyWithoutUnexpectedChanges()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        string toolRoot = Path.Combine(repositoryRoot, "external-tools");
        string stagingRoot = Path.Combine(Path.GetTempPath(), $"nfc-real-combiner-smoke-{Guid.NewGuid():N}");

        try
        {
            byte[] goldenBytes = File.ReadAllBytes(FindGoldenExpectedOutput(goldenRoot, "51927"));
            ExternalCombinerToolManifest manifest = LoadManifest(
                Path.Combine(toolRoot, "legacy-combiner", "1.13.0", "manifest.json"));
            Assert.Equal(
                manifest.Sha256,
                Sha256(Path.Combine(toolRoot, manifest.ToolId, manifest.ToolVersion, manifest.ExecutableName)));

            var crcOnlyCommand = new LegacyCombinerPostbuildCommand(
                "nt51927-real-tool-crc-smoke",
                LegacyCombinerCommandFamily.CrcOnlyMode,
                "NT51927BASED_GEN_CRC_MODE",
                "CRC32",
                []);
            var smokeProfile = new LegacyCombinerPostbuildProfile(
                "nfc.nt51927.real-tool-crc-smoke-v1",
                "NT51927",
                "legacy-combiner-1.13.0",
                "nt51927_fw.bin",
                [crcOnlyCommand],
                [crcOnlyCommand],
                "Windows smoke test for the committed Combiner 1.13.0 binding.");
            var registry = new ExternalCombinerToolRegistry([manifest]);
            var processor = new LegacyCombinerPostbuildProcessor(
                registry,
                [smokeProfile],
                toolRoot,
                stagingRoot,
                new SystemExternalProcessRunner());
            var request = new ExternalProcessorRequest(
                "real-tool-nt51927-crc-smoke",
                smokeProfile.ProcessorId,
                "legacy-combiner-1.13.0",
                goldenBytes,
                [],
                new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

            ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

            Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.Empty(result.ChangedRanges);
            Assert.Equal(goldenBytes, result.OutputBytes.ToArray());
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    private static string FindGoldenExpectedOutput(string goldenRoot, string ic)
    {
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(testCase => testCase.GetProperty("ic").GetString() == ic);
        string relativePath = goldenCase.GetProperty("expectedOutput").GetProperty("path").GetString()!;
        return Path.Combine(goldenRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static ExternalCombinerToolManifest LoadManifest(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        return new ExternalCombinerToolManifest(
            RequiredString(root, "schemaVersion"),
            RequiredString(root, "toolBindingId"),
            RequiredString(root, "toolId"),
            RequiredString(root, "toolVersion"),
            RequiredString(root, "displayName"),
            RequiredString(root, "platform"),
            RequiredString(root, "executableName"),
            RequiredString(root, "sha256"),
            RequiredString(root, "adapterId"),
            RequiredString(root, "inputMode"),
            [.. root.GetProperty("argumentTemplate").EnumerateArray().Select(item => item.GetString()!)],
            RequiredString(root, "workingDirectoryPolicy"),
            root.GetProperty("timeoutSeconds").GetInt32(),
            [.. root.GetProperty("allowedExtraOutputFiles").EnumerateArray().Select(item => item.GetString()!)]);
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        return element.GetProperty(propertyName).GetString()
            ?? throw new InvalidOperationException($"Manifest property '{propertyName}' is null.");
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SPEC.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}

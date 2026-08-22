using System.Text.Json;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Diagnostics;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Tests.Diagnostics;

/// <summary>Protects the local diagnostic export schema and privacy boundary.</summary>
public sealed class JsonSystemDiagnosticsExporterTests
{
    /// <summary>Export is versioned and contains neither the destination nor raw catalog issue messages.</summary>
    [Fact]
    public async Task ExportWritesPrivacyFilteredVersionedJson()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"nfc-diagnostics-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, "private-user-name-diagnostics.json");
        try
        {
            SystemInformationService service = new(
                "0.10.3-test",
                new StubCatalog(),
                new NoReload(),
                new ExternalProcessorEnvironmentLoader(static (_, _) =>
                    throw new NotSupportedException()),
                new StubRuntimeProbe(),
                new StubClock());

            await new JsonSystemDiagnosticsExporter().ExportAsync(
                service.CreateBundle(),
                destination,
                TestContext.Current.CancellationToken);

            string json = await File.ReadAllTextAsync(
                destination,
                TestContext.Current.CancellationToken);
            using var document = JsonDocument.Parse(json);
            Assert.Equal(
                SystemDiagnosticsBundle.CurrentSchemaVersion,
                document.RootElement.GetProperty("schemaVersion").GetString());
            Assert.Equal(
                "ColdStartBlocked",
                document.RootElement.GetProperty("current").GetProperty("catalogState").GetString());
            Assert.DoesNotContain(destination, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("C:/private/catalog.json", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("catalog.source.invalid", json, StringComparison.Ordinal);
            Assert.Contains("activities", json, StringComparison.Ordinal);
            Assert.DoesNotContain("transitions", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class StubCatalog : ICanonicalSupportMatrixQuery
    {
        public CanonicalSupportMatrixQueryResult Query()
        {
            return new CanonicalSupportMatrixQueryResult(
                CanonicalSupportMatrixCatalogState.ColdStartBlocked,
                matrix: null,
                [new CapabilityCatalogIssue(
                    "catalog.source.invalid",
                    "C:/private/catalog.json",
                    null)]);
        }
    }

    private sealed class NoReload : ICanonicalCapabilityCatalogReloader
    {
        public void Reload(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private sealed class StubRuntimeProbe : ISystemRuntimeProbe
    {
        public SystemRuntimeFacts Probe()
        {
            return new SystemRuntimeFacts(".NET test", "Windows test", "x64");
        }
    }

    private sealed class StubClock : ISystemClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    }
}

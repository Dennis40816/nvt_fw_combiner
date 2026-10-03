using System.Runtime.CompilerServices;
using System.Text.Json;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Diagnostics;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Closed probe protocol over typed catalog and observation ports.</summary>
public sealed class PrebuiltProfileCatalogProbeTests
{
    /// <summary>The real normal catalog path never loads configuration, external processors, or user files.</summary>
    [Fact]
    public void NormalCatalogProbeDoesNotTouchOtherHostServices()
    {
        using var workspace = TempWorkspace.Create("probe-side-effect-traps");
        var files = new TrapFiles();
        var toolchain = new TrapToolchain();
        int externalLoads = 0;
        int configurationLoads = 0;
        var environment = new ExternalProcessorEnvironmentLoader((_, _) =>
        {
            externalLoads++;
            throw new InvalidOperationException("External tools must not load.");
        });
        CompositionHostServices host = CompositionHostServices.Create(environment, null, workspace.PathFor("uncreated-state"),
            loadConfigurationFamily: () =>
            {
                configurationLoads++;
                throw new InvalidOperationException("Configuration must not load.");
            }, localFiles: files, toolchainConfiguration: toolchain);
        using var output = new StringWriter();
        Assert.True(CompositionHostServices.TryHandleProfileCatalogProbe(["--profile-catalog-probe-v1"], output,
            () => (host.CanonicalCatalogLoader, host.CanonicalSupportMatrixQuery), BuiltInProfileAdmissionStatus.Instance, out int exit));
        Assert.Equal(0, exit);
        using JsonDocument json = JsonDocument.Parse(output.ToString());
        Assert.True(json.RootElement.GetProperty("catalogLoaded").GetBoolean());
        Assert.Equal("prebuilt", json.RootElement.GetProperty("admissionSource").GetString());
        Assert.Null(json.RootElement.GetProperty("rejectionCode").GetString());
        Assert.Equal(0, externalLoads);
        Assert.Equal(0, configurationLoads);
        Assert.Equal(0, files.Calls);
        Assert.Equal(0, toolchain.Calls);
        Assert.Equal(ExternalProcessorEnvironmentState.NotLoaded, host.ExternalEnvironment.Current.State);
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>Unknown launches are untouched; malformed probe invocations never compose services.</summary>
    [Theory]
    [InlineData("", false, 0)]
    [InlineData("--other", false, 0)]
    [InlineData("--profile-catalog-probe-v1 extra", true, 2)]
    [InlineData("--profile-catalog-probe-v1 --profile-catalog-probe-v1", true, 2)]
    [InlineData("extra --profile-catalog-probe-v1", true, 2)]
    public void MalformedOrUnrelatedArgumentsNeverRunCatalog(string command, bool handled, int expectedExit)
    {
        ArgumentNullException.ThrowIfNull(command);
        using var output = new StringWriter();
        Assert.Equal(handled, CompositionHostServices.TryHandleProfileCatalogProbe(
            command.Split(' ', StringSplitOptions.RemoveEmptyEntries), output,
            () => throw new InvalidOperationException("unexpected host composition"), new Status(null), out int exit));
        Assert.Equal(expectedExit, exit);
        Assert.Equal(string.Empty, output.ToString());
    }

    /// <summary>Only a successful publication and a real resolved typed observation permit exit zero.</summary>
    [Theory]
    [InlineData(true, null, 1)]
    [InlineData(true, BuiltInProfileAdmissionSource.Prebuilt, 0)]
    [InlineData(true, BuiltInProfileAdmissionSource.Json, 0)]
    [InlineData(false, BuiltInProfileAdmissionSource.Json, 1)]
    [InlineData(false, null, 1)]
    public void ProtocolReportsExactPublicationAndObservation(bool succeeds, BuiltInProfileAdmissionSource? source, int expectedExit)
    {
        BuiltInProfileAdmission? fact = source is null ? null : new(source.Value,
            source == BuiltInProfileAdmissionSource.Json ? BuiltInProfileAdmissionRejectionReason.Missing : null);
        var catalog = new Catalog(succeeds);
        using var output = new StringWriter();
        Assert.True(CompositionHostServices.TryHandleProfileCatalogProbe(["--profile-catalog-probe-v1"], output,
            () => (catalog, catalog), new Status(fact), out int exit));
        Assert.Equal(expectedExit, exit);
        Assert.Equal(1, catalog.LoadCount);
        string line = Assert.Single(output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using JsonDocument json = JsonDocument.Parse(line);
        Assert.Equal(["schemaVersion", "admissionSource", "rejectionCode", "catalogLoaded"],
            json.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("nfc-profile-catalog-probe-v1", json.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(fact?.SourceToken, json.RootElement.GetProperty("admissionSource").GetString());
        Assert.Equal(fact?.RejectionCode, json.RootElement.GetProperty("rejectionCode").GetString());
        Assert.Equal(succeeds, json.RootElement.GetProperty("catalogLoaded").GetBoolean());
    }

    /// <summary>Host failures are path-free protocol failures; passive publication cannot be replaced.</summary>
    [Fact]
    public void HostFailureIsClosedAndObservationPublishesOnlyOnce()
    {
        var status = new BuiltInProfileAdmissionStatus();
        Assert.Null(status.Current);
        var first = new BuiltInProfileAdmission(BuiltInProfileAdmissionSource.Json, BuiltInProfileAdmissionRejectionReason.FileAccess);
        status.Publish(first);
        _ = Assert.Throws<InvalidOperationException>(() => status.Publish(new(BuiltInProfileAdmissionSource.Prebuilt)));
        Assert.Same(first, status.Current);
        using var output = new StringWriter();
        Assert.True(CompositionHostServices.TryHandleProfileCatalogProbe(["--profile-catalog-probe-v1"], output,
            () => throw new IOException("private-path"), status, out int exit));
        Assert.Equal(1, exit);
        Assert.DoesNotContain("private-path", output.ToString(), StringComparison.Ordinal);
        using JsonDocument json = JsonDocument.Parse(output.ToString());
        Assert.False(json.RootElement.GetProperty("catalogLoaded").GetBoolean());
    }

    private sealed class Status(BuiltInProfileAdmission? fact) : IBuiltInProfileAdmissionStatus
    {
        public BuiltInProfileAdmission? Current => fact;
    }

    private sealed class TrapFiles : ILocalFileStore
    {
        internal int Calls { get; private set; }
        private InvalidOperationException Unexpected()
        {
            Calls++;
            return new("User file access is forbidden.");
        }
        public ValueTask<T> ReadAsync<T>(string path, long maximumBytes,
            Func<Stream, CancellationToken, ValueTask<T>> project, CancellationToken cancellationToken)
        {
            throw Unexpected();
        }
        public ValueTask<string> ReadTextAsync(string path, long maximumBytes, CancellationToken cancellationToken,
            Action<LocalFileReadProgress>? progress = null)
        {
            throw Unexpected();
        }
        public ValueTask<string> ReadTextAsync(Func<CancellationToken, ValueTask<Stream>> openReadAsync,
            long maximumBytes, CancellationToken cancellationToken)
        {
            throw Unexpected();
        }
        public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            throw Unexpected();
        }

        public ValueTask<LocalFileDestinationInfo> InspectDestinationAsync(string path, CancellationToken cancellationToken)
        {
            throw Unexpected();
        }

        public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes,
            LocalFileWriteOptions options, CancellationToken cancellationToken)
        {
            throw Unexpected();
        }

        public ValueTask<bool> RefersToSameFileAsync(string first, string second, CancellationToken cancellationToken)
        {
            throw Unexpected();
        }

        public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes,
            LocalFileWriteMode mode, CancellationToken cancellationToken)
        {
            throw Unexpected();
        }
    }

    private sealed class TrapToolchain : IToolchainRuntimeConfigurationSession
    {
        internal int Calls { get; private set; }
        private InvalidOperationException Unexpected()
        {
            Calls++;
            return new("Toolchain access is forbidden.");
        }
        public ToolchainRuntimeConfigurationSnapshot Current => throw Unexpected();
        public ValueTask<ToolchainRuntimeConfigurationOperationResult> ReloadAsync(CancellationToken cancellationToken)
        {
            throw Unexpected();
        }
        public ValueTask<ToolchainRuntimeConfigurationOperationResult> SaveAsync(ToolchainRuntimeSelection selection, CancellationToken cancellationToken)
        {
            throw Unexpected();
        }
        public ValueTask<ToolchainRuntimeCandidateInspection> InspectAsync(string path, CancellationToken cancellationToken)
        {
            throw Unexpected();
        }
        public ValueTask<ToolchainRuntimeCandidateInspection> InspectBundledAsync(CancellationToken cancellationToken)
        {
            throw Unexpected();
        }
        public ValueTask<IReadOnlyList<ToolchainRuntimeCandidateInspection>> DetectAsync(CancellationToken cancellationToken)
        {
            throw Unexpected();
        }
    }

    private sealed class Catalog(bool succeeds) : ICanonicalCapabilityCatalogLoader, ICanonicalSupportMatrixQuery
    {
        internal int LoadCount { get; private set; }
        public async IAsyncEnumerable<CanonicalCapabilityCatalogLoadUpdate> LoadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCount++;
            await Task.Yield();
            yield return new(null, new(succeeds, false, null, []));
        }
        public CanonicalSupportMatrixQueryResult Query()
        {
            return new(succeeds ? CanonicalSupportMatrixCatalogState.Current : CanonicalSupportMatrixCatalogState.ColdStartBlocked,
                succeeds ? new("test", "1", new string('a', 64), new ResolutionToken("test"), []) : null, []);
        }
    }
}

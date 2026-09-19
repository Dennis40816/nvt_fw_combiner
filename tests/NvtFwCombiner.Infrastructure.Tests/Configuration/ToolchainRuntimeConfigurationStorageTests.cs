using System.Text;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.Configuration;
using NvtFwCombiner.Infrastructure.Configuration;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Configuration;

/// <summary>Strict runtime configuration transport over real stable reads and atomic writes.</summary>
public sealed class ToolchainRuntimeConfigurationStorageTests
{
    private const string Valid = """{"schemaVersion":1,"source":"bundled","path":null,"sha256":null}""";

    /// <summary>Missing files remain distinct and atomic writes round-trip every field.</summary>
    [Fact]
    public async Task MissingAndAtomicRoundTrip()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        var storage = new ToolchainRuntimeConfigurationStorage(new LocalFileStore(), workspace.PathFor("config/runtime.json"));
        Assert.Null(await storage.ReadAsync(TestContext.Current.CancellationToken));
        var document = new ToolchainRuntimeConfigurationDocument(1, "user", @"C:\工具\runtime.exe", new('a', 64));

        await storage.WriteAsync(document, TestContext.Current.CancellationToken);

        Assert.Equal(document, await storage.ReadAsync(TestContext.Current.CancellationToken));
        _ = Assert.Single(Directory.GetFiles(workspace.PathFor("config")));
    }

    /// <summary>Unknown properties, duplicate keys, malformed framing and missing required fields fail closed.</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":1,\"source\":\"bundled\",\"source\":\"user\"}")]
    [InlineData("{\"schemaVersion\":1,\"source\":\"bundled\",\"extra\":true}")]
    [InlineData("{\"schemaVersion\":1,\"source\":\"bundled\",}")]
    [InlineData("{/*comment*/\"schemaVersion\":1,\"source\":\"bundled\"}")]
    [InlineData("[[[[[[[[[0]]]]]]]]]")]
    [InlineData("{\"schemaVersion\":1,\"source\":\"bundled\"} {}")]
    [InlineData("{\"schemaVersion\":1,\"source\":\"\\uD800\"}")]
    public async Task StrictDocumentFailuresPreserveBytes(string json)
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string path = workspace.Write("runtime.json", Encoding.UTF8.GetBytes(json));
        var storage = new ToolchainRuntimeConfigurationStorage(new LocalFileStore(), path);

        _ = await Assert.ThrowsAsync<ToolchainRuntimeConfigurationFormatException>(() => storage.ReadAsync(TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(json, File.ReadAllText(path));
    }

    /// <summary>The transport owns size/encoding, while source and hash shape stay Application decisions.</summary>
    [Fact]
    public async Task SemanticValuesRoundTripButOversizeAndInvalidUnicodeFail()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string path = workspace.Write("runtime.json", Encoding.UTF8.GetBytes(Valid));
        var storage = new ToolchainRuntimeConfigurationStorage(new LocalFileStore(), path);
        var future = new ToolchainRuntimeConfigurationDocument(2, "unknown", "relative", "bad-hash");
        await storage.WriteAsync(future, TestContext.Current.CancellationToken);
        Assert.Equal(future, await storage.ReadAsync(TestContext.Current.CancellationToken));
        byte[] saved = File.ReadAllBytes(path);
        _ = await Assert.ThrowsAsync<ToolchainRuntimeConfigurationFormatException>(() => storage.WriteAsync(
            future with { Path = "\uD800" }, TestContext.Current.CancellationToken).AsTask());
        Assert.Equal(saved, File.ReadAllBytes(path));
        _ = await Assert.ThrowsAsync<ToolchainRuntimeConfigurationFormatException>(() => storage.WriteAsync(
            future with { Path = new('x', 65537) }, TestContext.Current.CancellationToken).AsTask());
        Assert.Equal(saved, File.ReadAllBytes(path));
        _ = workspace.Write("runtime.json", Encoding.UTF8.GetBytes(Valid.PadRight(65537)));
        _ = await Assert.ThrowsAsync<LocalFileTooLargeException>(() => storage.ReadAsync(TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Invalid UTF-8 cannot be replaced silently while decoding.</summary>
    [Fact]
    public async Task InvalidUtf8FailsClosed()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string path = workspace.Write("runtime.json", [0xFF]);
        var storage = new ToolchainRuntimeConfigurationStorage(new LocalFileStore(), path);
        _ = await Assert.ThrowsAsync<ToolchainRuntimeConfigurationFormatException>(() => storage.ReadAsync(TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>A failed real atomic replacement preserves the original document.</summary>
    [Fact]
    public async Task LockedDestinationPreservesOriginal()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using TempWorkspace workspace = TempWorkspace.Create();
        string path = workspace.Write("runtime.json", Encoding.UTF8.GetBytes(Valid));
        var storage = new ToolchainRuntimeConfigurationStorage(new LocalFileStore(), path);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => storage.WriteAsync(
                new(1, "user", "candidate", new('a', 64)), TestContext.Current.CancellationToken).AsTask());
        }
        Assert.Equal(Valid, File.ReadAllText(path));
        _ = Assert.Single(Directory.GetFiles(workspace.Root));
    }
}

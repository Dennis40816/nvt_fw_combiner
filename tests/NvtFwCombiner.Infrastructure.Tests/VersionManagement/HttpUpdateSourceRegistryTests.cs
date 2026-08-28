using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies bounded fail-closed HTTPS Registry retrieval.</summary>
public sealed class HttpUpdateSourceRegistryTests
{
    private const string RegistryUri = "https://example.test/update-source-registry.json";

    /// <summary>HTTPS bytes pass through the same strict document admission as filesystem bytes.</summary>
    [Fact]
    public async Task ValidHttpsDocumentIsParsedAndHasExactContentDigest()
    {
        string source = OperatingSystem.IsWindows() ? @"G:\updates" : "/updates";
        byte[] bytes = RegistryBytes(Path.Combine(source, "update-catalog.json"));
        using HttpClient client = CreateClient((request, _) => Response(
            request,
            HttpStatusCode.OK,
            bytes,
            "application/json"));

        UpdateSourceRegistryLoadResult result = await new HttpUpdateSourceRegistry(
            RegistryUri,
            client,
            TimeSpan.FromSeconds(2)).LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Snapshot!.RegistryRevision);
        Assert.Equal(source, Assert.Single(result.Snapshot.Entries).SourceRoot);
        Assert.Matches("^[0-9a-f]{64}$", result.Snapshot.ContentDigest);
    }

    /// <summary>A SharePoint sign-in page is never accepted as a Registry document.</summary>
    [Fact]
    public async Task SharePointLoginHtmlRequiresAuthenticationEvenWithHttpSuccess()
    {
        const string sharePoint =
            "https://tenant.sharepoint.com/:u:/g/personal/user/example?e=token";
        using HttpClient client = CreateClient((request, _) => Response(
            request,
            HttpStatusCode.OK,
            "<!doctype html><title>Sign in</title>"u8.ToArray(),
            "text/html"));

        UpdateSourceRegistryLoadResult result = await new HttpUpdateSourceRegistry(
            sharePoint,
            client,
            TimeSpan.FromSeconds(2)).LoadAsync(TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateSourceRegistryLoadIssue.AuthenticationRequired, result.Issue);
    }

    /// <summary>HTML sniffing remains typed after an authentication-host redirect, BOM, or bad media type.</summary>
    [Fact]
    public async Task RedirectedBomHtmlRequiresAuthenticationWithoutTrustingContentType()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. "  <html>Sign in</html>"u8.ToArray()];
        int calls = 0;
        using HttpClient client = CreateClient((request, _) => ++calls == 1
            ? Redirect(request, "https://login.microsoftonline.com/login")
            : Response(request, HttpStatusCode.OK, bytes, "application/octet-stream"));

        UpdateSourceRegistryLoadResult result = await new HttpUpdateSourceRegistry(
            RegistryUri,
            client,
            TimeSpan.FromSeconds(2)).LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, calls);
        Assert.Equal(UpdateSourceRegistryLoadIssue.AuthenticationRequired, result.Issue);
    }

    /// <summary>Non-HTML markup is invalid Registry content, not a false sign-in diagnosis.</summary>
    [Theory]
    [InlineData("<?xml version=\"1.0\"?><registry />")]
    [InlineData("<svg></svg>")]
    public async Task NonHtmlMarkupIsInvalidManifest(string markup)
    {
        using HttpClient client = CreateClient((request, _) => Response(
            request,
            HttpStatusCode.OK,
            Encoding.UTF8.GetBytes(markup),
            "application/octet-stream"));

        UpdateSourceRegistryLoadResult result = await new HttpUpdateSourceRegistry(
            RegistryUri,
            client,
            TimeSpan.FromSeconds(2)).LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateSourceRegistryLoadIssue.InvalidManifest, result.Issue);
    }

    /// <summary>Only five HTTPS redirects are admitted and every hop is executed by the tested reader.</summary>
    [Theory]
    [InlineData(5, true, 6)]
    [InlineData(6, false, 6)]
    public async Task RedirectCountIsBounded(int redirectCount, bool succeeds, int expectedCalls)
    {
        int calls = 0;
        string source = OperatingSystem.IsWindows() ? @"G:\updates" : "/updates";
        byte[] document = RegistryBytes(Path.Combine(source, "update-catalog.json"));
        using HttpClient client = CreateClient((request, _) => ++calls <= redirectCount
            ? Redirect(request, $"https://example.test/hop-{calls}")
            : Response(request, HttpStatusCode.OK, document, "application/json"));

        UpdateSourceRegistryLoadResult result = await new HttpUpdateSourceRegistry(
            RegistryUri,
            client,
            TimeSpan.FromSeconds(2)).LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expectedCalls, calls);
        Assert.Equal(succeeds, result.IsSuccess);
        if (!succeeds)
        {
            Assert.Equal(UpdateSourceRegistryLoadIssue.RegistryUnavailable, result.Issue);
        }
    }

    /// <summary>An HTTPS redirect cannot downgrade transport before another request is sent.</summary>
    [Fact]
    public async Task RedirectDowngradeFailsBeforeSendingTheUnsafeHop()
    {
        int calls = 0;
        using HttpClient client = CreateClient((request, _) =>
        {
            calls++;
            return Redirect(request, "http://example.test/registry.json");
        });

        UpdateSourceRegistryLoadResult result = await new HttpUpdateSourceRegistry(
            RegistryUri,
            client,
            TimeSpan.FromSeconds(2)).LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, calls);
        Assert.Equal(UpdateSourceRegistryLoadIssue.UnsafeLocator, result.Issue);
    }

    private static byte[] RegistryBytes(string catalogPath)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            registryId = "nvt-fw-combiner-production",
            registryRevision = 7,
            publishedAtUtc = "2026-08-27T00:00:00Z",
            catalogPublication = new
            {
                latestVersion = "1.0.1",
                catalogSchemaVersion = 1,
                catalogSha256 = new string('a', 64),
            },
            entries = new[] { new { status = "latest", catalogPath } },
        });
    }

    /// <summary>Remote authorization status is a typed authentication failure.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task AuthorizationStatusRequiresAuthentication(HttpStatusCode status)
    {
        using HttpClient client = CreateClient((request, _) => Response(
            request,
            status,
            [],
            "application/json"));

        UpdateSourceRegistryLoadResult result = await new HttpUpdateSourceRegistry(
            RegistryUri,
            client,
            TimeSpan.FromSeconds(2)).LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateSourceRegistryLoadIssue.AuthenticationRequired, result.Issue);
    }

    /// <summary>Payload size is bounded even when the server omits Content-Length.</summary>
    [Fact]
    public async Task ChunkedPayloadOverLimitFailsBeforeJsonAdmission()
    {
        byte[] bytes = new byte[UpdateSourceRegistryDocumentParser.MaximumRegistryBytes + 1];
        using HttpClient client = CreateClient((request, _) =>
        {
            HttpResponseMessage response = Response(
                request,
                HttpStatusCode.OK,
                bytes,
                "application/json");
            response.Content.Headers.ContentLength = null;
            return response;
        });

        UpdateSourceRegistryLoadResult result = await new HttpUpdateSourceRegistry(
            RegistryUri,
            client,
            TimeSpan.FromSeconds(2)).LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateSourceRegistryLoadIssue.RegistryTooLarge, result.Issue);
    }

    /// <summary>An oversized declared body is rejected without opening its content stream.</summary>
    [Fact]
    public async Task DeclaredPayloadOverLimitFailsBeforeJsonAdmission()
    {
        using HttpClient client = CreateClient((request, _) =>
        {
            HttpResponseMessage response = Response(
                request,
                HttpStatusCode.OK,
                [],
                "application/json");
            response.Content.Headers.ContentLength =
                UpdateSourceRegistryDocumentParser.MaximumRegistryBytes + 1;
            return response;
        });

        UpdateSourceRegistryLoadResult result = await new HttpUpdateSourceRegistry(
            RegistryUri,
            client,
            TimeSpan.FromSeconds(2)).LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateSourceRegistryLoadIssue.RegistryTooLarge, result.Issue);
    }

    /// <summary>The Registry request has one bounded timeout distinct from caller cancellation.</summary>
    [Fact]
    public async Task TransportTimeoutIsTypedWithoutEscapingCancellation()
    {
        using HttpClient client = CreateClient(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        });

        UpdateSourceRegistryLoadResult result = await new HttpUpdateSourceRegistry(
            RegistryUri,
            client,
            TimeSpan.FromMilliseconds(20)).LoadAsync(CancellationToken.None);

        Assert.Equal(UpdateSourceRegistryLoadIssue.RegistryTimedOut, result.Issue);
    }

    /// <summary>Caller cancellation is never rewritten as a transport timeout.</summary>
    [Fact]
    public async Task CallerCancellationPropagates()
    {
        using HttpClient client = CreateClient((request, _) => Response(
            request,
            HttpStatusCode.OK,
            "{}"u8.ToArray(),
            "application/json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new HttpUpdateSourceRegistry(
                RegistryUri,
                client,
                TimeSpan.FromSeconds(2)).LoadAsync(cancellation.Token).AsTask());
    }

    /// <summary>Only HTTPS locators are probed by the remote transport.</summary>
    [Theory]
    [InlineData("http://example.test/registry.json")]
    [InlineData("ftp://example.test/registry.json")]
    [InlineData("https://user:secret@example.test/registry.json")]
    public async Task UnsafeRemoteLocatorFailsWithoutSending(string locator)
    {
        int calls = 0;
        using HttpClient client = CreateClient((request, _) =>
        {
            calls++;
            return Response(request, HttpStatusCode.OK, "{}"u8.ToArray(), "application/json");
        });

        UpdateSourceRegistryLoadResult result = await new HttpUpdateSourceRegistry(
            locator,
            client,
            TimeSpan.FromSeconds(2)).LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateSourceRegistryLoadIssue.UnsafeLocator, result.Issue);
        Assert.Equal(0, calls);
    }

    /// <summary>The transport factory keeps filesystem and HTTPS authority separate.</summary>
    [Theory]
    [InlineData(RegistryUri, typeof(HttpUpdateSourceRegistry))]
    [InlineData(@"G:\fixed\registry.json", typeof(FileSystemUpdateSourceRegistry))]
    public void AdapterFactorySelectsOnlyTheDeclaredTransport(string locator, Type expectedType)
    {
        Assert.IsType(expectedType, UpdateSourceRegistryAdapterFactory.Create(locator));
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The returned HttpClient owns and disposes the test handler.")]
    private static HttpClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
    {
        return new HttpClient(new StubHandler((request, token) =>
            Task.FromResult(responder(request, token))))
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The returned HttpClient owns and disposes the test handler.")]
    private static HttpClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        return new HttpClient(new StubHandler(responder))
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    private static HttpResponseMessage Response(
        HttpRequestMessage request,
        HttpStatusCode status,
        byte[] bytes,
        string contentType)
    {
        return new HttpResponseMessage(status)
        {
            RequestMessage = request,
            Content = new ByteArrayContent(bytes)
            {
                Headers = { ContentType = new MediaTypeHeaderValue(contentType) },
            },
        };
    }

    private static HttpResponseMessage Redirect(
        HttpRequestMessage request,
        string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            RequestMessage = request,
        };
        response.Headers.Location = new Uri(location, UriKind.Absolute);
        return response;
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return responder(request, cancellationToken);
        }
    }
}

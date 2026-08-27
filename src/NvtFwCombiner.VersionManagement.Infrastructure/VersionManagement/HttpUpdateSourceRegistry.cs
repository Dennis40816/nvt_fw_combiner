using System.Net;
using System.Net.Http.Headers;
using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Bounded HTTPS transport for one externally hosted Registry document.</summary>
public sealed class HttpUpdateSourceRegistry : IUpdateSourceRegistry
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(45);
    private const int MaximumRedirects = 5;

    private static readonly HttpClient SharedClient = CreateSharedClient();
    private readonly string? _locator;
    private readonly HttpClient _client;
    private readonly TimeSpan _timeout;

    /// <summary>Creates one reader for an injected HTTPS Registry locator.</summary>
    public HttpUpdateSourceRegistry(string? locator)
        : this(locator, SharedClient, DefaultTimeout)
    {
    }

    internal HttpUpdateSourceRegistry(
        string? locator,
        HttpClient client,
        TimeSpan timeout)
    {
        _locator = locator;
        _client = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        _timeout = timeout;
    }

    /// <inheritdoc />
    public async ValueTask<UpdateSourceRegistryLoadResult> LoadAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(_locator))
        {
            return Failure(UpdateSourceRegistryLoadIssue.NotConfigured);
        }
        if (!TryNormalizeHttpsLocator(_locator, out Uri locator))
        {
            return Failure(UpdateSourceRegistryLoadIssue.UnsafeLocator);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try
        {
            (HttpResponseMessage? sentResponse, UpdateSourceRegistryLoadIssue redirectIssue) =
                await SendWithRedirectsAsync(locator, timeout.Token).ConfigureAwait(false);
            if (sentResponse is null)
            {
                return Failure(redirectIssue);
            }
            using HttpResponseMessage response = sentResponse;
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Failure(UpdateSourceRegistryLoadIssue.RegistryMissing);
            }
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return Failure(UpdateSourceRegistryLoadIssue.AuthenticationRequired);
            }
            if (!response.IsSuccessStatusCode)
            {
                return Failure(UpdateSourceRegistryLoadIssue.RegistryUnavailable);
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(
                    response.Content.Headers.ContentType?.MediaType,
                    "text/html"))
            {
                return Failure(UpdateSourceRegistryLoadIssue.AuthenticationRequired);
            }
            if (response.Content.Headers.ContentLength is >
                UpdateSourceRegistryDocumentParser.MaximumRegistryBytes)
            {
                return Failure(UpdateSourceRegistryLoadIssue.RegistryTooLarge);
            }

            byte[] bytes = await ReadBoundedAsync(response.Content, timeout.Token)
                .ConfigureAwait(false);
            return StartsWithHtmlDocument(bytes)
                ? Failure(UpdateSourceRegistryLoadIssue.AuthenticationRequired)
                : UpdateSourceRegistryDocumentParser.Parse(bytes);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(UpdateSourceRegistryLoadIssue.RegistryTimedOut);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return Failure(UpdateSourceRegistryLoadIssue.RegistryUnavailable);
        }
        catch (IOException)
        {
            return Failure(UpdateSourceRegistryLoadIssue.RegistryUnavailable);
        }
    }

    private async Task<(HttpResponseMessage? Response, UpdateSourceRegistryLoadIssue Issue)>
        SendWithRedirectsAsync(
            Uri initialLocator,
            CancellationToken cancellationToken)
    {
        Uri locator = initialLocator;
        for (int redirectCount = 0; ; redirectCount++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, locator);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage response = await _client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!IsRedirect(response.StatusCode))
            {
                return (response, UpdateSourceRegistryLoadIssue.None);
            }
            if (redirectCount >= MaximumRedirects ||
                response.Headers.Location is not { } location ||
                !Uri.TryCreate(locator, location, out Uri? redirected) ||
                !TryNormalizeHttpsLocator(redirected.AbsoluteUri, out Uri safeRedirect))
            {
                response.Dispose();
                return (null, redirectCount >= MaximumRedirects
                    ? UpdateSourceRegistryLoadIssue.RegistryUnavailable
                    : UpdateSourceRegistryLoadIssue.UnsafeLocator);
            }

            response.Dispose();
            locator = safeRedirect;
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using Stream stream = await content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        int limit = UpdateSourceRegistryDocumentParser.MaximumRegistryBytes;
        byte[] buffer = new byte[limit + 1];
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(
                    buffer.AsMemory(total, buffer.Length - total),
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            total += read;
        }
        return total > limit
            ? new byte[limit + 1]
            : buffer.AsSpan(0, total).ToArray();
    }

    private static bool StartsWithHtmlDocument(ReadOnlySpan<byte> bytes)
    {
        int index = 0;
        if (bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF)
        {
            index = 3;
        }
        while (index < bytes.Length && bytes[index] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            index++;
        }
        ReadOnlySpan<byte> content = bytes[index..];
        return StartsWithAsciiIgnoreCase(content, "<!doctype html"u8) ||
            StartsWithAsciiIgnoreCase(content, "<html"u8);
    }

    private static bool StartsWithAsciiIgnoreCase(
        ReadOnlySpan<byte> value,
        ReadOnlySpan<byte> prefix)
    {
        if (value.Length < prefix.Length)
        {
            return false;
        }
        for (int index = 0; index < prefix.Length; index++)
        {
            byte actual = value[index];
            byte expected = prefix[index];
            if (actual is >= (byte)'A' and <= (byte)'Z')
            {
                actual = (byte)(actual + ((byte)'a' - (byte)'A'));
            }
            if (expected is >= (byte)'A' and <= (byte)'Z')
            {
                expected = (byte)(expected + ((byte)'a' - (byte)'A'));
            }
            if (actual != expected)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.MovedPermanently or
            HttpStatusCode.Redirect or
            HttpStatusCode.SeeOther or
            HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;
    }

    private static bool TryNormalizeHttpsLocator(string value, out Uri locator)
    {
        locator = null!;
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed) &&
            StringComparer.OrdinalIgnoreCase.Equals(parsed.Scheme, Uri.UriSchemeHttps) &&
            string.IsNullOrEmpty(parsed.UserInfo) &&
            string.IsNullOrEmpty(parsed.Fragment) &&
            parsed.HostNameType != UriHostNameType.Unknown &&
            (locator = parsed) is not null;
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The process-wide HttpClient owns this handler for the complete process lifetime.")]
    private static HttpClient CreateSharedClient()
    {
        return new HttpClient(
            new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = false,
            },
            disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    private static UpdateSourceRegistryLoadResult Failure(
        UpdateSourceRegistryLoadIssue issue)
    {
        return new(null, issue);
    }
}

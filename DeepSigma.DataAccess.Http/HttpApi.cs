using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeepSigma.Core.Utilities;
using DeepSigma.DataAccess.CsvUtilities.Reading;
using DeepSigma.DataAccess.CsvUtilities.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepSigma.DataAccess.Http;

/// <summary>
/// Instance API for interacting with web APIs: fetching JSON and CSV payloads,
/// deserializing JSON responses, and surfacing rate-limit / error payloads as exceptions.
/// </summary>
/// <remarks>
/// Takes an <see cref="HttpClient"/> via constructor injection. Register with
/// <c>services.AddDeepSigmaHttp()</c> to get an <see cref="IHttpClientFactory"/>-managed
/// client wired up for you.
/// Per-call timeouts are enforced via a linked <see cref="CancellationTokenSource"/>,
/// so the injected <see cref="HttpClient"/>'s own <c>Timeout</c> property is ignored.
/// </remarks>
public class HttpApi
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpApi> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="HttpApi"/> with the supplied <see cref="HttpClient"/>.
    /// </summary>
    public HttpApi(HttpClient httpClient, ILogger<HttpApi>? logger = null)
    {
        _http = httpClient;
        _logger = logger ?? NullLogger<HttpApi>.Instance;
    }

    /// <summary>
    /// Fetches JSON data from the URL and deserializes it into <typeparamref name="T"/>.
    /// </summary>
    public async Task<T?> GetDataFromUrlAsync<T>(string url, int timeoutInSeconds = 15, Action<string?>? apiResultLoggingMethod = null, CancellationToken cancellationToken = default)
    {
        string? json = await GetJsonResponseAsync(url, timeoutInSeconds, cancellationToken);

        apiResultLoggingMethod?.Invoke(json);

        if (string.IsNullOrWhiteSpace(json)) { return default; }
        return LoadFromJson<T>(json);
    }

    /// <summary>
    /// Fetches CSV data from the URL and deserializes it into a list of <typeparamref name="T"/>.
    /// </summary>
    public async Task<List<T>> GetDataFromCsvAsync<T>(string url, int timeoutInSeconds = 15, Action<string?>? apiResultLoggingMethod = null, CancellationToken cancellationToken = default) where T : class
    {
        string? csv = await GetCsvDataAsync(url, timeoutInSeconds, cancellationToken);

        apiResultLoggingMethod?.Invoke(csv);

        if (string.IsNullOrWhiteSpace(csv)) { return []; }
        return LoadFromCsv<T>(csv) ?? [];
    }

    /// <summary>
    /// Fetches raw JSON from the URL as a string.
    /// </summary>
    public Task<string?> GetJsonResponseAsync(string urlEndpoint, int timeoutInSeconds = 15, CancellationToken cancellationToken = default)
        => FetchStringAsync(urlEndpoint, "JSON", timeoutInSeconds, validator: null, cancellationToken);

    /// <summary>
    /// Fetches CSV data from the URL as a string, validating the Content-Type.
    /// </summary>
    public Task<string?> GetCsvDataAsync(string urlEndpoint, int timeoutSeconds = 15, CancellationToken cancellationToken = default)
        => FetchStringAsync(urlEndpoint, "CSV", timeoutSeconds, validator: RequireCsvContentType, cancellationToken);

    private static void RequireCsvContentType(HttpResponseMessage response, string body)
    {
        if (!response.Content.Headers.ContentType?.MediaType?.Contains("csv", StringComparison.OrdinalIgnoreCase) ?? true)
        {
            throw new InvalidOperationException($"Non-CSV response: {body}");
        }
    }

    /// <summary>
    /// Shared fetch-as-string scaffold: linked-CTS, per-call timeout, response-headers-read mode,
    /// status-success enforcement, optional content-type validation.
    /// </summary>
    private async Task<string?> FetchStringAsync(
        string urlEndpoint,
        string kind,
        int timeoutInSeconds,
        Action<HttpResponseMessage, string>? validator,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("HTTP GET ({Kind}) {Url}, timeout {Timeout}s", kind, urlEndpoint, timeoutInSeconds);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutInSeconds));

        using HttpResponseMessage response = await _http
            .GetAsync(urlEndpoint, HttpCompletionOption.ResponseHeadersRead, cts.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
        validator?.Invoke(response, body);
        return body;
    }

    /// <summary>
    /// Deserializes a JSON string into <typeparamref name="T"/>, surfacing API rate-limit / error payloads as exceptions.
    /// Pure helper — does not require an <see cref="HttpClient"/>.
    /// </summary>
    public static T? LoadFromJson<T>(string jsonText)
    {
        if (string.IsNullOrWhiteSpace(jsonText)) { return default; }

        JsonSerializerOptions opts = new()
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            PropertyNameCaseInsensitive = true,
        };

        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;

        if (root.TryGetProperty("Note", out var note))
        {
            throw new InvalidOperationException($"API note: {note.GetString()}");
        }

        if (root.TryGetProperty("Error Message", out var err))
        {
            throw new InvalidOperationException($"API error: {err.GetString()}");
        }

        return JsonSerializer.Deserialize<T>(jsonText, opts);
    }

    /// <summary>
    /// Parses CSV text into a list of <typeparamref name="T"/>. Pure helper — does not require an <see cref="HttpClient"/>.
    /// </summary>
    public static List<T>? LoadFromCsv<T>(string csvText) where T : class
    {
        CsvImportResult<T> results = CsvReader.ReadFromStringSafe<T>(csvText);
        return results.IsSuccess ? results.Records : null;
    }

    /// <summary>
    /// Fetches XML data from the URL and deserializes it into <typeparamref name="T"/>.
    /// </summary>
    public async Task<T?> GetDataFromXmlUrlAsync<T>(string url, int timeoutInSeconds = 15, Action<string?>? apiResultLoggingMethod = null, CancellationToken cancellationToken = default)
    {
        string? xml = await GetXmlResponseAsync(url, timeoutInSeconds, cancellationToken).ConfigureAwait(false);

        apiResultLoggingMethod?.Invoke(xml);

        if (string.IsNullOrWhiteSpace(xml)) { return default; }
        return LoadFromXml<T>(xml);
    }

    /// <summary>
    /// Fetches raw XML from the URL as a string. Does not validate Content-Type — XML responses
    /// arrive with a wide variety of media types (<c>application/xml</c>, <c>text/xml</c>,
    /// <c>application/atom+xml</c>, <c>application/rss+xml</c>, etc.), and strict validation
    /// causes more friction than it prevents.
    /// </summary>
    public Task<string?> GetXmlResponseAsync(string urlEndpoint, int timeoutInSeconds = 15, CancellationToken cancellationToken = default)
        => FetchStringAsync(urlEndpoint, "XML", timeoutInSeconds, validator: null, cancellationToken);

    /// <summary>
    /// Streams elements matching <paramref name="elementName"/> from a large XML response, yielding
    /// each deserialized as <typeparamref name="T"/>. Memory usage stays bounded regardless of payload size.
    /// Intended for OAI-PMH harvests, RSS feeds, S3 inventory, and similar large XML streams.
    /// </summary>
    public async IAsyncEnumerable<T> StreamXmlElementsAsync<T>(
        string urlEndpoint,
        string elementName,
        string? namespaceUri = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("HTTP GET (XML stream) {Url}, element <{Element}>", urlEndpoint, elementName);

        using var response = await _http
            .GetAsync(urlEndpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        await foreach (T item in XmlReaderExtensions.ReadElementsAsync<T>(stream, elementName, namespaceUri, cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Streams a response body into <paramref name="destination"/>. Suitable for binary downloads
    /// or any large payload where buffering the whole body in memory is undesirable.
    /// </summary>
    public async Task DownloadToStreamAsync(string url, Stream destination, int bufferSize = 81_920, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        _logger.LogDebug("HTTP GET (download) {Url}", url);

        using var response = await _http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await source.CopyToAsync(destination, bufferSize, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Convenience wrapper over <see cref="DownloadToStreamAsync"/> that writes to a file path.
    /// Creates the destination's directory if it does not exist.
    /// </summary>
    public async Task DownloadToFileAsync(string url, string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        string? directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory)) { Directory.CreateDirectory(directory); }

        await using FileStream file = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await DownloadToStreamAsync(url, file, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deserializes an XML string into <typeparamref name="T"/>. Pure helper — does not require an <see cref="HttpClient"/>.
    /// </summary>
    public static T? LoadFromXml<T>(string xmlText) => XMLUtilities.FromString<T>(xmlText);
}

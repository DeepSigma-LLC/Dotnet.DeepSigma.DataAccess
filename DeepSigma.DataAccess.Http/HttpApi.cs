using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
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
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Default JSON options used for serialization/deserialization: case-insensitive property names
    /// and tolerant number parsing. PascalCase property names go on the wire as-is.
    /// </summary>
    public static JsonSerializerOptions DefaultJsonOptions { get; } = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Preset for talking to APIs that use <c>snake_case</c> field names (e.g. Python / FastAPI services).
    /// PascalCase C# property names are serialized as <c>snake_case</c> on the wire and accepted
    /// case-insensitively on read.
    /// </summary>
    public static JsonSerializerOptions SnakeCaseJsonOptions { get; } = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Initializes a new instance of <see cref="HttpApi"/>. Pass <see cref="SnakeCaseJsonOptions"/>
    /// (or any custom <see cref="JsonSerializerOptions"/>) to override <see cref="DefaultJsonOptions"/>.
    /// </summary>
    public HttpApi(HttpClient httpClient, ILogger<HttpApi>? logger = null, JsonSerializerOptions? jsonOptions = null)
    {
        _http = httpClient;
        _logger = logger ?? NullLogger<HttpApi>.Instance;
        _jsonOptions = jsonOptions ?? DefaultJsonOptions;
    }

    /// <summary>
    /// Fetches JSON data from the URL and deserializes it into <typeparamref name="T"/>.
    /// </summary>
    public async Task<T?> GetDataFromUrlAsync<T>(string url, int timeoutInSeconds = 15, Action<string?>? apiResultLoggingMethod = null, CancellationToken cancellationToken = default)
    {
        string? json = await GetJsonResponseAsync(url, timeoutInSeconds, cancellationToken);

        apiResultLoggingMethod?.Invoke(json);

        if (string.IsNullOrWhiteSpace(json)) { return default; }
        return LoadFromJson<T>(json, _jsonOptions);
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
        => SendForStringAsync(new HttpRequestMessage(HttpMethod.Get, urlEndpoint), "GET JSON", timeoutInSeconds, validator: null, cancellationToken);

    /// <summary>
    /// Fetches CSV data from the URL as a string, validating the Content-Type.
    /// </summary>
    public Task<string?> GetCsvDataAsync(string urlEndpoint, int timeoutSeconds = 15, CancellationToken cancellationToken = default)
        => SendForStringAsync(new HttpRequestMessage(HttpMethod.Get, urlEndpoint), "GET CSV", timeoutSeconds, validator: RequireCsvContentType, cancellationToken);

    private static void RequireCsvContentType(HttpResponseMessage response, string body)
    {
        if (!response.Content.Headers.ContentType?.MediaType?.Contains("csv", StringComparison.OrdinalIgnoreCase) ?? true)
        {
            throw new InvalidOperationException($"Non-CSV response: {body}");
        }
    }

    /// <summary>
    /// Shared send-as-string scaffold: linked-CTS, per-call timeout, response-headers-read mode,
    /// status-success enforcement, optional content-type validation. Disposes the request.
    /// </summary>
    private async Task<string?> SendForStringAsync(
        HttpRequestMessage request,
        string kind,
        int timeoutInSeconds,
        Action<HttpResponseMessage, string>? validator,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("HTTP {Kind} {Url}, timeout {Timeout}s", kind, request.RequestUri, timeoutInSeconds);

        using (request)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutInSeconds));

            using HttpResponseMessage response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            validator?.Invoke(response, body);
            return body;
        }
    }

    /// <summary>
    /// Shared send-to-stream scaffold for binary or large payloads. No per-call timeout —
    /// streaming durations are caller-bounded via <paramref name="cancellationToken"/>. Disposes the request.
    /// </summary>
    private async Task SendToStreamCoreAsync(
        HttpRequestMessage request,
        string kind,
        Stream destination,
        int bufferSize,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("HTTP {Kind} {Url}", kind, request.RequestUri);

        using (request)
        {
            using HttpResponseMessage response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await source.CopyToAsync(destination, bufferSize, cancellationToken).ConfigureAwait(false);
        }
    }

    private StringContent JsonContent<T>(T body)
    {
        string json = JsonSerializer.Serialize(body, _jsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private HttpRequestMessage BuildJsonRequest<T>(HttpMethod method, string url, T body)
        => new(method, url) { Content = JsonContent(body) };

    /// <summary>
    /// Deserializes a JSON string into <typeparamref name="T"/>, surfacing API rate-limit / error payloads as exceptions.
    /// Pure helper — does not require an <see cref="HttpClient"/>.
    /// </summary>
    public static T? LoadFromJson<T>(string jsonText, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(jsonText)) { return default; }

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

        return JsonSerializer.Deserialize<T>(jsonText, options ?? DefaultJsonOptions);
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
        => SendForStringAsync(new HttpRequestMessage(HttpMethod.Get, urlEndpoint), "GET XML", timeoutInSeconds, validator: null, cancellationToken);

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
    public Task DownloadToStreamAsync(string url, Stream destination, int bufferSize = 81_920, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return SendToStreamCoreAsync(new HttpRequestMessage(HttpMethod.Get, url), "GET download", destination, bufferSize, cancellationToken);
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

    /// <summary>
    /// POSTs <paramref name="body"/> as JSON and deserializes the JSON response into <typeparamref name="TResponse"/>.
    /// </summary>
    public async Task<TResponse?> PostJsonAsync<TRequest, TResponse>(
        string url,
        TRequest body,
        int timeoutInSeconds = 15,
        Action<string?>? apiResultLoggingMethod = null,
        CancellationToken cancellationToken = default)
    {
        string? json = await SendForStringAsync(BuildJsonRequest(HttpMethod.Post, url, body), "POST JSON", timeoutInSeconds, validator: null, cancellationToken).ConfigureAwait(false);
        apiResultLoggingMethod?.Invoke(json);
        if (string.IsNullOrWhiteSpace(json)) { return default; }
        return LoadFromJson<TResponse>(json, _jsonOptions);
    }

    /// <summary>
    /// POSTs <paramref name="body"/> as JSON; returns the raw response body string.
    /// </summary>
    public Task<string?> PostJsonAsync<TRequest>(
        string url,
        TRequest body,
        int timeoutInSeconds = 15,
        CancellationToken cancellationToken = default)
        => SendForStringAsync(BuildJsonRequest(HttpMethod.Post, url, body), "POST JSON", timeoutInSeconds, validator: null, cancellationToken);

    /// <summary>
    /// POSTs <paramref name="fields"/> as <c>application/x-www-form-urlencoded</c> and deserializes the JSON response.
    /// Common for OAuth token endpoints.
    /// </summary>
    public async Task<TResponse?> PostFormAsync<TResponse>(
        string url,
        IEnumerable<KeyValuePair<string, string>> fields,
        int timeoutInSeconds = 15,
        Action<string?>? apiResultLoggingMethod = null,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(fields) };
        string? json = await SendForStringAsync(request, "POST form", timeoutInSeconds, validator: null, cancellationToken).ConfigureAwait(false);
        apiResultLoggingMethod?.Invoke(json);
        if (string.IsNullOrWhiteSpace(json)) { return default; }
        return LoadFromJson<TResponse>(json, _jsonOptions);
    }

    /// <summary>
    /// PUTs <paramref name="body"/> as JSON and deserializes the JSON response into <typeparamref name="TResponse"/>.
    /// </summary>
    public async Task<TResponse?> PutJsonAsync<TRequest, TResponse>(
        string url,
        TRequest body,
        int timeoutInSeconds = 15,
        Action<string?>? apiResultLoggingMethod = null,
        CancellationToken cancellationToken = default)
    {
        string? json = await SendForStringAsync(BuildJsonRequest(HttpMethod.Put, url, body), "PUT JSON", timeoutInSeconds, validator: null, cancellationToken).ConfigureAwait(false);
        apiResultLoggingMethod?.Invoke(json);
        if (string.IsNullOrWhiteSpace(json)) { return default; }
        return LoadFromJson<TResponse>(json, _jsonOptions);
    }

    /// <summary>
    /// PATCHes <paramref name="body"/> as JSON and deserializes the JSON response into <typeparamref name="TResponse"/>.
    /// </summary>
    public async Task<TResponse?> PatchJsonAsync<TRequest, TResponse>(
        string url,
        TRequest body,
        int timeoutInSeconds = 15,
        Action<string?>? apiResultLoggingMethod = null,
        CancellationToken cancellationToken = default)
    {
        string? json = await SendForStringAsync(BuildJsonRequest(HttpMethod.Patch, url, body), "PATCH JSON", timeoutInSeconds, validator: null, cancellationToken).ConfigureAwait(false);
        apiResultLoggingMethod?.Invoke(json);
        if (string.IsNullOrWhiteSpace(json)) { return default; }
        return LoadFromJson<TResponse>(json, _jsonOptions);
    }

    /// <summary>
    /// DELETEs <paramref name="url"/>; returns the response body string (often empty). Throws on non-2xx.
    /// </summary>
    public async Task<string?> DeleteAsync(
        string url,
        int timeoutInSeconds = 15,
        Action<string?>? apiResultLoggingMethod = null,
        CancellationToken cancellationToken = default)
    {
        string? body = await SendForStringAsync(new HttpRequestMessage(HttpMethod.Delete, url), "DELETE", timeoutInSeconds, validator: null, cancellationToken).ConfigureAwait(false);
        apiResultLoggingMethod?.Invoke(body);
        return body;
    }

    /// <summary>
    /// POSTs <paramref name="body"/> as JSON and streams the response body into <paramref name="destination"/>.
    /// Suitable for binary responses or large payloads where buffering is undesirable.
    /// </summary>
    public Task PostJsonToStreamAsync<TRequest>(
        string url,
        TRequest body,
        Stream destination,
        int bufferSize = 81_920,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return SendToStreamCoreAsync(BuildJsonRequest(HttpMethod.Post, url, body), "POST JSON stream", destination, bufferSize, cancellationToken);
    }

    /// <summary>
    /// PUTs <paramref name="body"/> as JSON and streams the response body into <paramref name="destination"/>.
    /// </summary>
    public Task PutJsonToStreamAsync<TRequest>(
        string url,
        TRequest body,
        Stream destination,
        int bufferSize = 81_920,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return SendToStreamCoreAsync(BuildJsonRequest(HttpMethod.Put, url, body), "PUT JSON stream", destination, bufferSize, cancellationToken);
    }

    /// <summary>
    /// Full-control send for cases the typed helpers don't cover (custom headers, multipart, non-JSON bodies).
    /// Caller owns <paramref name="request"/>; this adds the linked-CTS timeout, status check, optional validator,
    /// and returns the response body string. The request is disposed.
    /// </summary>
    public Task<string?> SendAsync(
        HttpRequestMessage request,
        int timeoutInSeconds = 15,
        Action<HttpResponseMessage, string>? validator = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendForStringAsync(request, $"SEND {request.Method.Method}", timeoutInSeconds, validator, cancellationToken);
    }

    /// <summary>
    /// Full-control send that streams the response body into <paramref name="destination"/>. The request is disposed.
    /// </summary>
    public Task SendToStreamAsync(
        HttpRequestMessage request,
        Stream destination,
        int bufferSize = 81_920,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);
        return SendToStreamCoreAsync(request, $"SEND {request.Method.Method} stream", destination, bufferSize, cancellationToken);
    }
}

namespace DeepSigma.DataAccess.Web;

/// <summary>
/// Abstraction over <see cref="HttpApi"/>: typed read/write helpers for web APIs
/// (JSON/CSV/XML), streaming downloads and large-XML streaming, and a full-control
/// send escape hatch. Depend on this interface rather than the concrete class so
/// consumers can be unit-tested with a mock. Register via <c>services.AddDeepSigmaHttp()</c>.
/// </summary>
public interface IHttpApi
{
    /// <summary>Fetches JSON from <paramref name="url"/> and deserializes it into <typeparamref name="T"/>.</summary>
    Task<T?> GetDataFromUrlAsync<T>(string url, int timeoutInSeconds = 15, Action<string?>? apiResultLoggingMethod = null, CancellationToken cancellationToken = default);

    /// <summary>Fetches CSV from <paramref name="url"/> and deserializes it into a list of <typeparamref name="T"/>.</summary>
    Task<List<T>> GetDataFromCsvAsync<T>(string url, int timeoutInSeconds = 15, Action<string?>? apiResultLoggingMethod = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Fetches raw JSON from <paramref name="urlEndpoint"/> as a string.</summary>
    Task<string?> GetJsonResponseAsync(string urlEndpoint, int timeoutInSeconds = 15, CancellationToken cancellationToken = default);

    /// <summary>Fetches CSV from <paramref name="urlEndpoint"/> as a string, validating the Content-Type.</summary>
    Task<string?> GetCsvDataAsync(string urlEndpoint, int timeoutSeconds = 15, CancellationToken cancellationToken = default);

    /// <summary>Fetches XML from <paramref name="url"/> and deserializes it into <typeparamref name="T"/>.</summary>
    Task<T?> GetDataFromXmlUrlAsync<T>(string url, int timeoutInSeconds = 15, Action<string?>? apiResultLoggingMethod = null, CancellationToken cancellationToken = default);

    /// <summary>Fetches raw XML from <paramref name="urlEndpoint"/> as a string.</summary>
    Task<string?> GetXmlResponseAsync(string urlEndpoint, int timeoutInSeconds = 15, CancellationToken cancellationToken = default);

    /// <summary>Streams elements named <paramref name="elementName"/> from a large XML response, each deserialized as <typeparamref name="T"/>. Memory stays bounded.</summary>
    IAsyncEnumerable<T> StreamXmlElementsAsync<T>(string urlEndpoint, string elementName, string? namespaceUri = null, CancellationToken cancellationToken = default);

    /// <summary>Streams a response body into <paramref name="destination"/>. Suitable for binary or large payloads.</summary>
    Task DownloadToStreamAsync(string url, Stream destination, int bufferSize = 81_920, CancellationToken cancellationToken = default);

    /// <summary>Streams a response body into a file at <paramref name="destinationPath"/>, creating the directory if needed.</summary>
    Task DownloadToFileAsync(string url, string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>POSTs <paramref name="body"/> as JSON and deserializes the JSON response into <typeparamref name="TResponse"/>.</summary>
    Task<TResponse?> PostJsonAsync<TRequest, TResponse>(string url, TRequest body, int timeoutInSeconds = 15, Action<string?>? apiResultLoggingMethod = null, CancellationToken cancellationToken = default);

    /// <summary>POSTs <paramref name="body"/> as JSON; returns the raw response body string.</summary>
    Task<string?> PostJsonAsync<TRequest>(string url, TRequest body, int timeoutInSeconds = 15, CancellationToken cancellationToken = default);

    /// <summary>POSTs <paramref name="fields"/> as <c>application/x-www-form-urlencoded</c> and deserializes the JSON response.</summary>
    Task<TResponse?> PostFormAsync<TResponse>(string url, IEnumerable<KeyValuePair<string, string>> fields, int timeoutInSeconds = 15, Action<string?>? apiResultLoggingMethod = null, CancellationToken cancellationToken = default);

    /// <summary>PUTs <paramref name="body"/> as JSON and deserializes the JSON response into <typeparamref name="TResponse"/>.</summary>
    Task<TResponse?> PutJsonAsync<TRequest, TResponse>(string url, TRequest body, int timeoutInSeconds = 15, Action<string?>? apiResultLoggingMethod = null, CancellationToken cancellationToken = default);

    /// <summary>PATCHes <paramref name="body"/> as JSON and deserializes the JSON response into <typeparamref name="TResponse"/>.</summary>
    Task<TResponse?> PatchJsonAsync<TRequest, TResponse>(string url, TRequest body, int timeoutInSeconds = 15, Action<string?>? apiResultLoggingMethod = null, CancellationToken cancellationToken = default);

    /// <summary>DELETEs <paramref name="url"/>; returns the response body string (often empty). Throws on non-2xx.</summary>
    Task<string?> DeleteAsync(string url, int timeoutInSeconds = 15, Action<string?>? apiResultLoggingMethod = null, CancellationToken cancellationToken = default);

    /// <summary>POSTs <paramref name="body"/> as JSON and streams the response body into <paramref name="destination"/>.</summary>
    Task PostJsonToStreamAsync<TRequest>(string url, TRequest body, Stream destination, int bufferSize = 81_920, CancellationToken cancellationToken = default);

    /// <summary>PUTs <paramref name="body"/> as JSON and streams the response body into <paramref name="destination"/>.</summary>
    Task PutJsonToStreamAsync<TRequest>(string url, TRequest body, Stream destination, int bufferSize = 81_920, CancellationToken cancellationToken = default);

    /// <summary>Full-control send for a caller-built request; applies the per-call timeout and status check and returns the body string. The request is disposed.</summary>
    Task<string?> SendAsync(HttpRequestMessage request, int timeoutInSeconds = 15, Action<HttpResponseMessage, string>? validator = null, CancellationToken cancellationToken = default);

    /// <summary>Full-control send that streams the response body into <paramref name="destination"/>. The request is disposed.</summary>
    Task SendToStreamAsync(HttpRequestMessage request, Stream destination, int bufferSize = 81_920, CancellationToken cancellationToken = default);
}

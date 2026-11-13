
using DeepSigma.Core.Utilities;
using DeepSigma.DataAccess.CsvUtilities.Results;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeepSigma.DataAccess.CsvUtilities.Reading;

namespace DeepSigma.DataAccess.Web;

/// <summary>
///  
/// </summary>
public static class Serializer
{

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
    /// Parses CSV text into a list of <typeparamref name="T"/>. Pure helper — does not require an <see cref="HttpClient"/>.
    /// </summary>
    public static List<T>? LoadFromCsv<T>(string csvText) where T : class
    {
        CsvImportResult<T> results = CsvReader.ReadFromStringSafe<T>(csvText);
        return results.IsSuccess ? results.Records : null;
    }

    /// <summary>
    /// Deserializes an XML string into <typeparamref name="T"/>. Pure helper — does not require an <see cref="HttpClient"/>.
    /// </summary>
    public static T? LoadFromXml<T>(string xmlText) => XMLUtilities.FromString<T>(xmlText);

    /// <summary>
    /// Deserializes a JSON string into <typeparamref name="T"/>. When <paramref name="guard"/> is
    /// supplied, it inspects the response root first and may throw to surface API-level errors
    /// (e.g. <see cref="JsonResponseGuards.AlphaVantage"/> for HTTP-200 rate-limit payloads).
    /// Pure helper — does not require an <see cref="HttpClient"/>.
    /// </summary>
    public static T? LoadFromJson<T>(string jsonText, JsonSerializerOptions? options = null, JsonResponseGuard? guard = null)
    {
        if (string.IsNullOrWhiteSpace(jsonText)) { return default; }

        if (guard is not null)
        {
            using JsonDocument doc = JsonDocument.Parse(jsonText);
            guard(doc.RootElement);
        }

        return JsonSerializer.Deserialize<T>(jsonText, options ?? DefaultJsonOptions);
    }

}

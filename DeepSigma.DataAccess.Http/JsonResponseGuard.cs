using System.Text.Json;

namespace DeepSigma.DataAccess.Web;

/// <summary>
/// Inspects the root element of a JSON response before it is deserialized and throws to surface
/// API-level errors that arrive with an HTTP 200 (e.g. rate-limit payloads). Supply one to
/// <see cref="Serializer.LoadFromJson{T}(string, JsonSerializerOptions?, JsonResponseGuard?)"/> or to
/// the <see cref="HttpApi"/> constructor; when none is supplied, no such check runs.
/// </summary>
public delegate void JsonResponseGuard(JsonElement root);

/// <summary>Ready-made <see cref="JsonResponseGuard"/>s for common provider conventions.</summary>
public static class JsonResponseGuards
{
    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when the root object carries a <c>Note</c> or
    /// <c>Error Message</c> property — the HTTP-200 throttle/error convention used by Alpha Vantage
    /// and similar financial-data providers. A no-op for non-object roots (arrays, scalars).
    /// </summary>
    public static JsonResponseGuard AlphaVantage { get; } = static root =>
    {
        if (root.ValueKind != JsonValueKind.Object) { return; }

        if (root.TryGetProperty("Note", out JsonElement note))
        {
            throw new InvalidOperationException($"API note: {note.GetString()}");
        }
        if (root.TryGetProperty("Error Message", out JsonElement error))
        {
            throw new InvalidOperationException($"API error: {error.GetString()}");
        }
    };
}

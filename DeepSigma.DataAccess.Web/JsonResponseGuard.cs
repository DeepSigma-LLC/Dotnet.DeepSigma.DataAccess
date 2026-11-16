using System.Text.Json;

namespace DeepSigma.DataAccess.Web;

/// <summary>
/// Inspects the root element of a JSON response before it is deserialized and throws to surface
/// API-level errors that arrive with an HTTP 200 (e.g. rate-limit payloads). Supply one to
/// <see cref="Serializer.LoadFromJson{T}(string, JsonSerializerOptions?, JsonResponseGuard?)"/> or to
/// the <see cref="HttpApi"/> constructor; when none is supplied, no such check runs. A provider's own convention belongs
/// in that provider's package (the AlphaVantage package has <c>Note</c> / <c>Error Message</c> / <c>Information</c>).
/// </summary>
public delegate void JsonResponseGuard(JsonElement root);

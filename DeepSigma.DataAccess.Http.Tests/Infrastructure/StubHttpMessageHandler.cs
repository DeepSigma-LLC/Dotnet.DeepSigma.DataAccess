using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace DeepSigma.DataAccess.Http.Tests.Infrastructure;

/// <summary>
/// Test handler that returns a configurable response without making any real HTTP calls.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Responder { get; set; }
    public List<HttpRequestMessage> Requests { get; } = new();

    public static StubHttpMessageHandler WithJsonBody(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new()
        {
            Responder = (_, _) =>
            {
                var response = new HttpResponseMessage(status)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
                return Task.FromResult(response);
            },
        };

    public static StubHttpMessageHandler WithCsvBody(string csv, HttpStatusCode status = HttpStatusCode.OK, string mediaType = "text/csv")
        => new()
        {
            Responder = (_, _) =>
            {
                var content = new StringContent(csv, Encoding.UTF8);
                content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
                var response = new HttpResponseMessage(status) { Content = content };
                return Task.FromResult(response);
            },
        };

    public static StubHttpMessageHandler WithStatus(HttpStatusCode status)
        => new() { Responder = (_, _) => Task.FromResult(new HttpResponseMessage(status)) };

    public static StubHttpMessageHandler WithDelay(TimeSpan delay)
        => new()
        {
            Responder = async (_, ct) =>
            {
                await Task.Delay(delay, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            },
        };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (Responder is null)
        {
            throw new InvalidOperationException("StubHttpMessageHandler.Responder is not configured.");
        }
        return Responder(request, cancellationToken);
    }
}

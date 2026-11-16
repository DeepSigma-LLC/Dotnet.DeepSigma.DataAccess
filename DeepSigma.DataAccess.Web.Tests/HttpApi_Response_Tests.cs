using System.Net;
using System.Net.Http.Headers;
using System.Text;
using DeepSigma.DataAccess.Web.Tests.Infrastructure;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace DeepSigma.DataAccess.Web.Tests;

/// <summary><see cref="HttpApi.SendForResponseAsync"/>: the response read whole, whatever its status.</summary>
public class HttpApi_Response_Tests
{
    [Fact]
    public async Task A_refusal_comes_back_with_its_status_body_headers_and_wait()
    {
        var handler = new StubHttpMessageHandler
        {
            Responder = (_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("Too many requests, slow down.", Encoding.UTF8, "text/plain"),
                };
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(42));
                response.Headers.Add("X-RateLimit-Remaining", "0");
                return Task.FromResult(response);
            },
        };
        var http = new HttpApi(new HttpClient(handler), clock: new FakeTimeProvider());

        var answer = await http.SendForResponseAsync(new HttpRequestMessage(HttpMethod.Post, "https://example.com/v3/mapping"),
                                                     cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.TooManyRequests, false, "Too many requests, slow down."), (answer.StatusCode, answer.IsSuccess, answer.Body));
        Assert.Equal(TimeSpan.FromSeconds(42), answer.RetryAfter);
        Assert.Equal("0", answer.Header("x-ratelimit-remaining"));                                // any case
        Assert.StartsWith("text/plain", answer.Header("Content-Type"));                           // the content's headers too
    }

    [Fact]
    public async Task A_success_is_read_the_same_way()
    {
        var http = new HttpApi(new HttpClient(StubHttpMessageHandler.WithJsonBody("""{"ok":true}""")));
        var answer = await http.SendForResponseAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.com/"),
                                                     cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.OK, true, """{"ok":true}""", (TimeSpan?)null), (answer.StatusCode, answer.IsSuccess, answer.Body, answer.RetryAfter));
        Assert.Null(answer.Header("X-Missing"));
    }

    [Fact]
    public async Task The_per_call_timeout_still_applies()
    {
        var http = new HttpApi(new HttpClient(StubHttpMessageHandler.WithDelay(TimeSpan.FromSeconds(30))));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            http.SendForResponseAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.com/"), timeoutInSeconds: 1,
                                      cancellationToken: TestContext.Current.CancellationToken));
    }
}

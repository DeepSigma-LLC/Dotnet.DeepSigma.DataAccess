using System.Net;
using DeepSigma.DataAccess.Http.Tests.Infrastructure;
using Xunit;

namespace DeepSigma.DataAccess.Http.Tests;

public class HttpApiTests
{
    private sealed record WeatherDto
    {
        public string City { get; init; } = "";
        public double TempF { get; init; }
    }

    [Fact]
    public async Task GetJsonResponseAsync_returns_body_on_success()
    {
        var handler = StubHttpMessageHandler.WithJsonBody("{\"city\":\"Seattle\",\"tempF\":55}");
        var http = new HttpApi(new HttpClient(handler));

        string? body = await http.GetJsonResponseAsync("https://example.com/weather");

        Assert.Equal("{\"city\":\"Seattle\",\"tempF\":55}", body);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetJsonResponseAsync_throws_on_non_success_status()
    {
        var handler = StubHttpMessageHandler.WithStatus(HttpStatusCode.InternalServerError);
        var http = new HttpApi(new HttpClient(handler));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => http.GetJsonResponseAsync("https://example.com/weather"));
    }

    [Fact]
    public async Task GetJsonResponseAsync_cancels_when_timeout_elapses()
    {
        var handler = StubHttpMessageHandler.WithDelay(TimeSpan.FromSeconds(5));
        var http = new HttpApi(new HttpClient(handler));

        // 1-second per-call timeout should fire well before the 5-second stub delay
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => http.GetJsonResponseAsync("https://example.com/slow", timeoutInSeconds: 1));
    }

    [Fact]
    public async Task GetJsonResponseAsync_honors_caller_cancellation_token()
    {
        var handler = StubHttpMessageHandler.WithDelay(TimeSpan.FromSeconds(5));
        var http = new HttpApi(new HttpClient(handler));
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => http.GetJsonResponseAsync("https://example.com/slow", timeoutInSeconds: 30, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task GetCsvDataAsync_throws_when_content_type_is_not_csv()
    {
        var handler = StubHttpMessageHandler.WithCsvBody("date,price\n2026-01-01,100", mediaType: "text/plain");
        var http = new HttpApi(new HttpClient(handler));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => http.GetCsvDataAsync("https://example.com/data.csv"));
    }

    [Fact]
    public async Task GetCsvDataAsync_returns_text_when_content_type_is_csv()
    {
        var handler = StubHttpMessageHandler.WithCsvBody("date,price\n2026-01-01,100");
        var http = new HttpApi(new HttpClient(handler));

        string? csv = await http.GetCsvDataAsync("https://example.com/data.csv");

        Assert.Contains("date,price", csv);
    }

    [Fact]
    public async Task GetDataFromUrlAsync_deserializes_json_into_T()
    {
        var handler = StubHttpMessageHandler.WithJsonBody("{\"city\":\"Seattle\",\"tempF\":55}");
        var http = new HttpApi(new HttpClient(handler));

        WeatherDto? dto = await http.GetDataFromUrlAsync<WeatherDto>("https://example.com/weather");

        Assert.NotNull(dto);
        Assert.Equal("Seattle", dto!.City);
        Assert.Equal(55, dto.TempF);
    }

    [Fact]
    public async Task GetDataFromUrlAsync_invokes_logging_callback_with_raw_body()
    {
        var handler = StubHttpMessageHandler.WithJsonBody("{\"city\":\"Seattle\",\"tempF\":55}");
        var http = new HttpApi(new HttpClient(handler));
        string? observedBody = null;

        await http.GetDataFromUrlAsync<WeatherDto>(
            "https://example.com/weather",
            apiResultLoggingMethod: body => observedBody = body);

        Assert.Equal("{\"city\":\"Seattle\",\"tempF\":55}", observedBody);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using DeepSigma.DataAccess.Http.Tests.Infrastructure;
using Xunit;

namespace DeepSigma.DataAccess.Http.Tests;

public class HttpApi_Write_Tests
{
    private sealed record CreateUserRequest
    {
        public string Name { get; init; } = "";
        public int Age { get; init; }
    }

    private sealed record CreateUserResponse
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
    }

    [Fact]
    public async Task PostJsonAsync_sends_json_body_and_deserializes_response()
    {
        string? capturedBody = null;
        string? capturedMediaType = null;
        HttpMethod? capturedMethod = null;
        var handler = new StubHttpMessageHandler
        {
            Responder = async (req, ct) =>
            {
                capturedMethod = req.Method;
                capturedMediaType = req.Content?.Headers.ContentType?.MediaType;
                capturedBody = req.Content is null ? null : await req.Content.ReadAsStringAsync(ct);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"id\":\"u-1\",\"name\":\"Ada\"}", Encoding.UTF8, "application/json"),
                };
            },
        };
        var http = new HttpApi(new HttpClient(handler));

        var response = await http.PostJsonAsync<CreateUserRequest, CreateUserResponse>(
            "https://example.com/users",
            new CreateUserRequest { Name = "Ada", Age = 37 },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Equal("u-1", response!.Id);
        Assert.Equal(HttpMethod.Post, capturedMethod);
        Assert.Equal("application/json", capturedMediaType);
        Assert.Contains("\"Name\":\"Ada\"", capturedBody);
        Assert.Contains("\"Age\":37", capturedBody);
    }

    [Fact]
    public async Task PostJsonAsync_invokes_logging_callback_with_raw_body()
    {
        var handler = StubHttpMessageHandler.WithJsonBody("{\"id\":\"u-1\",\"name\":\"Ada\"}");
        var http = new HttpApi(new HttpClient(handler));
        string? observed = null;

        await http.PostJsonAsync<CreateUserRequest, CreateUserResponse>(
            "https://example.com/users",
            new CreateUserRequest { Name = "Ada" },
            apiResultLoggingMethod: b => observed = b,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("{\"id\":\"u-1\",\"name\":\"Ada\"}", observed);
    }

    [Fact]
    public async Task PostJsonAsync_raw_overload_returns_response_body_string()
    {
        var handler = StubHttpMessageHandler.WithJsonBody("{\"ok\":true}");
        var http = new HttpApi(new HttpClient(handler));

        string? body = await http.PostJsonAsync(
            "https://example.com/echo",
            new CreateUserRequest { Name = "Ada" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("{\"ok\":true}", body);
    }

    [Fact]
    public async Task PostFormAsync_sends_url_encoded_body()
    {
        string? capturedBody = null;
        string? capturedMediaType = null;
        var handler = new StubHttpMessageHandler
        {
            Responder = async (req, ct) =>
            {
                capturedMediaType = req.Content?.Headers.ContentType?.MediaType;
                capturedBody = req.Content is null ? null : await req.Content.ReadAsStringAsync(ct);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"id\":\"u-1\",\"name\":\"Ada\"}", Encoding.UTF8, "application/json"),
                };
            },
        };
        var http = new HttpApi(new HttpClient(handler));

        var response = await http.PostFormAsync<CreateUserResponse>(
            "https://example.com/oauth/token",
            new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", "abc"),
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Equal("application/x-www-form-urlencoded", capturedMediaType);
        Assert.Contains("grant_type=client_credentials", capturedBody);
        Assert.Contains("client_id=abc", capturedBody);
    }

    [Fact]
    public async Task PutJsonAsync_sends_put_with_body_and_deserializes()
    {
        var handler = StubHttpMessageHandler.WithJsonBody("{\"id\":\"u-1\",\"name\":\"Ada\"}");
        var http = new HttpApi(new HttpClient(handler));

        var response = await http.PutJsonAsync<CreateUserRequest, CreateUserResponse>(
            "https://example.com/users/u-1",
            new CreateUserRequest { Name = "Ada" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Equal(HttpMethod.Put, handler.Requests.Single().Method);
    }

    [Fact]
    public async Task PatchJsonAsync_sends_patch_with_body_and_deserializes()
    {
        var handler = StubHttpMessageHandler.WithJsonBody("{\"id\":\"u-1\",\"name\":\"Ada\"}");
        var http = new HttpApi(new HttpClient(handler));

        var response = await http.PatchJsonAsync<CreateUserRequest, CreateUserResponse>(
            "https://example.com/users/u-1",
            new CreateUserRequest { Name = "Ada" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Equal(HttpMethod.Patch, handler.Requests.Single().Method);
    }

    [Fact]
    public async Task DeleteAsync_sends_delete_and_returns_body()
    {
        var handler = StubHttpMessageHandler.WithJsonBody("{\"deleted\":\"u-1\"}");
        var http = new HttpApi(new HttpClient(handler));
        string? observed = null;

        string? body = await http.DeleteAsync(
            "https://example.com/users/u-1",
            apiResultLoggingMethod: b => observed = b,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("{\"deleted\":\"u-1\"}", body);
        Assert.Equal(body, observed);
        Assert.Equal(HttpMethod.Delete, handler.Requests.Single().Method);
    }

    [Fact]
    public async Task DeleteAsync_throws_on_non_success_status()
    {
        var handler = StubHttpMessageHandler.WithStatus(HttpStatusCode.NotFound);
        var http = new HttpApi(new HttpClient(handler));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => http.DeleteAsync(
                "https://example.com/users/u-1",
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PostJsonAsync_cancels_when_timeout_elapses()
    {
        var handler = StubHttpMessageHandler.WithDelay(TimeSpan.FromSeconds(5));
        var http = new HttpApi(new HttpClient(handler));

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => http.PostJsonAsync<CreateUserRequest, CreateUserResponse>(
                "https://example.com/slow",
                new CreateUserRequest { Name = "Ada" },
                timeoutInSeconds: 1,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PostJsonToStreamAsync_copies_response_body_into_destination()
    {
        var handler = new StubHttpMessageHandler
        {
            Responder = (_, _) =>
            {
                var content = new ByteArrayContent(new byte[] { 1, 2, 3, 4, 5 });
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            },
        };
        var http = new HttpApi(new HttpClient(handler));
        using var ms = new MemoryStream();

        await http.PostJsonToStreamAsync(
            "https://example.com/render",
            new CreateUserRequest { Name = "Ada" },
            ms,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, ms.ToArray());
        Assert.Equal(HttpMethod.Post, handler.Requests.Single().Method);
    }

    [Fact]
    public async Task SendAsync_escape_hatch_returns_body_and_runs_validator()
    {
        var handler = StubHttpMessageHandler.WithJsonBody("hello");
        var http = new HttpApi(new HttpClient(handler));
        bool validatorRan = false;

        var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/custom")
        {
            Content = new StringContent("payload", Encoding.UTF8, "text/plain"),
        };
        request.Headers.Add("X-Custom", "yes");

        string? body = await http.SendAsync(
            request,
            validator: (resp, b) =>
            {
                validatorRan = true;
                Assert.Equal("hello", b);
                Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("hello", body);
        Assert.True(validatorRan);
        Assert.Equal("yes", handler.Requests.Single().Headers.GetValues("X-Custom").Single());
    }
}

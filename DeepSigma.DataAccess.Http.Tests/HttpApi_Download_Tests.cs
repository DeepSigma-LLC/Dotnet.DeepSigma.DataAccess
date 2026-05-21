using System.Net;
using System.Text;
using DeepSigma.DataAccess.Http.Tests.Infrastructure;
using Xunit;

namespace DeepSigma.DataAccess.Http.Tests;

public class HttpApi_Download_Tests
{
    private static StubHttpMessageHandler WithBinaryBody(byte[] bytes)
        => new()
        {
            Responder = (_, _) =>
            {
                var content = new ByteArrayContent(bytes);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            },
        };

    [Fact]
    public async Task DownloadToStreamAsync_CopiesAllBytes()
    {
        byte[] payload = Enumerable.Range(0, 4096).Select(i => (byte)(i % 256)).ToArray();
        var http = new HttpApi(new HttpClient(WithBinaryBody(payload)));
        using var destination = new MemoryStream();

        await http.DownloadToStreamAsync(
            "https://example.com/blob",
            destination,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(payload, destination.ToArray());
    }

    [Fact]
    public async Task DownloadToStreamAsync_HandlesEmptyResponse()
    {
        var http = new HttpApi(new HttpClient(WithBinaryBody(Array.Empty<byte>())));
        using var destination = new MemoryStream();

        await http.DownloadToStreamAsync(
            "https://example.com/blob",
            destination,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(destination.ToArray());
    }

    [Fact]
    public async Task DownloadToStreamAsync_ThrowsOnNullDestination()
    {
        var http = new HttpApi(new HttpClient(WithBinaryBody(new byte[] { 1, 2, 3 })));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            http.DownloadToStreamAsync(
                "https://example.com/blob",
                null!,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadToStreamAsync_ThrowsOnNonSuccessStatus()
    {
        var http = new HttpApi(new HttpClient(StubHttpMessageHandler.WithStatus(HttpStatusCode.NotFound)));
        using var destination = new MemoryStream();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            http.DownloadToStreamAsync(
                "https://example.com/missing",
                destination,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadToFileAsync_WritesFileAndCreatesDirectory()
    {
        byte[] payload = Encoding.UTF8.GetBytes("the quick brown fox jumps over the lazy dog");
        var http = new HttpApi(new HttpClient(WithBinaryBody(payload)));

        string tempDir = Path.Combine(Path.GetTempPath(), "deepsigma-http-test-" + Guid.NewGuid().ToString("N"));
        string nestedPath = Path.Combine(tempDir, "nested", "out.bin");

        try
        {
            await http.DownloadToFileAsync(
                "https://example.com/blob",
                nestedPath,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(File.Exists(nestedPath));
            Assert.Equal(payload, await File.ReadAllBytesAsync(nestedPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(tempDir)) { Directory.Delete(tempDir, recursive: true); }
        }
    }

    [Fact]
    public async Task DownloadToFileAsync_ThrowsOnEmptyPath()
    {
        var http = new HttpApi(new HttpClient(WithBinaryBody(new byte[] { 1, 2, 3 })));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            http.DownloadToFileAsync(
                "https://example.com/blob",
                "",
                cancellationToken: TestContext.Current.CancellationToken));
    }
}

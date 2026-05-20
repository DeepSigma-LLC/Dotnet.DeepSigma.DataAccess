using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeepSigma.DataAccess.AzureBlobStorage.Tests;

/// <summary>
/// DI smoke tests. BlobServiceClient/BlobContainerClient construction is lazy — it does not
/// connect until the first operation — so these tests work without a live storage account or Azurite.
/// </summary>
public class DependencyInjectionTests
{
    // Azurite default development connection string (well-known, no secrets — published in Azure docs).
    private const string DummyConnectionString =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    [Fact]
    public void AddDeepSigmaAzureBlobStorage_resolves_BlobStorageApi()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaAzureBlobStorage(DummyConnectionString, "documents");
        using var provider = services.BuildServiceProvider();

        var api = provider.GetRequiredService<BlobStorageApi>();

        Assert.NotNull(api);
    }

    [Fact]
    public void BlobStorageApi_is_registered_as_singleton()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaAzureBlobStorage(DummyConnectionString, "documents");
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<BlobStorageApi>();
        var second = provider.GetRequiredService<BlobStorageApi>();

        Assert.Same(first, second);
    }
}

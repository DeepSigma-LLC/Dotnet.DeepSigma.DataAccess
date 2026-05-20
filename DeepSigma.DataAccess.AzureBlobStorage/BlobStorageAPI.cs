using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepSigma.DataAccess.AzureBlobStorage;

/// <summary>
/// Provides methods to interact with Azure Blob Storage:
/// uploading, downloading, deleting, and listing blobs in a specified container.
/// </summary>
/// <remarks>
/// Holds a long-lived <see cref="BlobContainerClient"/> for the lifetime of the instance.
/// The Azure SDK clients are thread-safe and designed to be reused.
/// </remarks>
public class BlobStorageApi
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<BlobStorageApi> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="BlobStorageApi"/>.
    /// </summary>
    public BlobStorageApi(string connectionString, string blobContainerName, ILogger<BlobStorageApi>? logger = null)
    {
        BlobServiceClient serviceClient = new(connectionString);
        _containerClient = serviceClient.GetBlobContainerClient(blobContainerName);
        _logger = logger ?? NullLogger<BlobStorageApi>.Instance;
    }

    /// <summary>
    /// Uploads the file to the blob container. Creates the container if it does not exist.
    /// </summary>
    public async Task UploadToBlobAsync(string filePath, bool allowOverwrite = false, CancellationToken cancellationToken = default)
    {
        string file = Path.GetFileName(filePath);
        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        BlobClient blobClient = _containerClient.GetBlobClient(file);
        using FileStream uploadFileStream = File.OpenRead(filePath);
        await blobClient.UploadAsync(uploadFileStream, overwrite: allowOverwrite, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Downloads the blob from the container to the specified file path.
    /// </summary>
    public async Task DownloadFromBlobAsync(string blobFileName, string downloadFilePath, CancellationToken cancellationToken = default)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(blobFileName);
        BlobDownloadInfo download = await blobClient.DownloadAsync(cancellationToken);
        await using FileStream downloadFileStream = File.OpenWrite(downloadFilePath);
        await download.Content.CopyToAsync(downloadFileStream, cancellationToken);
    }

    /// <summary>
    /// Deletes the blob from the container.
    /// </summary>
    public async Task DeleteBlobFileAsync(string blobFileName, CancellationToken cancellationToken = default)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(blobFileName);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Lists the blobs in the container.
    /// </summary>
    public async Task<List<string>> ListAllItemsBlobsAsync(CancellationToken cancellationToken = default)
    {
        List<string> blobs = new();
        await foreach (BlobItem blobItem in _containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            blobs.Add(blobItem.Name);
        }
        return blobs;
    }
}

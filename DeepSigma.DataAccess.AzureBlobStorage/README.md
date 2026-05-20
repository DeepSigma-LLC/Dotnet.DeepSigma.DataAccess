# DeepSigma.DataAccess.AzureBlobStorage

Azure Blob Storage helpers: upload, download, delete, and list. Wraps `Azure.Storage.Blobs` for a single named container.

This package is **independent** of the relational stack.

## Installation

```bash
dotnet add package DeepSigma.DataAccess.AzureBlobStorage
```

## Dependencies

| Package | Purpose |
|---|---|
| `Azure.Storage.Blobs` | Official Azure Blob Storage SDK. |

## What it provides

### `BlobStorageAPI`

Constructed with a Storage connection string and a container name. Each instance is bound to a single container.

| Method | Behaviour |
|---|---|
| `UploadToBlob(filePath, allowOverwrite = false)` | Creates the container if missing, then uploads the local file as a blob named after the file's leaf name. |
| `DownloadFromBlob(blobFileName, downloadFilePath)` | Downloads `blobFileName` from the container and writes it to `downloadFilePath`. |
| `DeleteBlobFile(blobFileName)` | Deletes the named blob if it exists. |
| `ListAllItemsBlobs()` | Returns the names of every blob in the container. |

## Quick start

```csharp
using DeepSigma.DataAccess.AzureBlobStorage;

var blobs = new BlobStorageAPI(
    connection_string: "<azure-storage-connection-string>",
    blob_container_name: "documents");

// Upload (creates the container if needed)
await blobs.UploadToBlob("./report.pdf", allowOverwrite: true);

// List
List<string> names = await blobs.ListAllItemsBlobs();

// Download
await blobs.DownloadFromBlob("report.pdf", "./downloads/report.pdf");

// Delete
await blobs.DeleteBlobFile("report.pdf");
```

## Notes

- **Upload blob name.** The uploaded blob is named after `Path.GetFileName(filePath)` — directory structure is **not** preserved. If you need a virtual folder hierarchy (e.g. `2025/05/report.pdf`), pass the desired full key as the filename and arrange your source file accordingly, or extend the API.
- **Connection lifetime.** Each method creates a fresh `BlobServiceClient` and `BlobContainerClient`. These are cheap to construct but not reused across calls; for very high-throughput workloads, consider lifting them into a long-lived field.
- **Listing is eager.** `ListAllItemsBlobs` materialises every blob name in memory. For large containers, prefer paging via the underlying `containerClient.GetBlobsAsync()` directly.

## License

MIT

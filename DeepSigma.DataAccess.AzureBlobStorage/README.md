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

### `BlobStorageApi`

Constructed with a Storage connection string and a container name. Each instance is bound to a single container.

| Method | Behaviour |
|---|---|
| `UploadToBlob(filePath, allowOverwrite = false)` | Creates the container if missing, then uploads the local file as a blob named after the file's leaf name. |
| `DownloadFromBlob(blobFileName, downloadFilePath)` | Downloads `blobFileName` from the container and writes it to `downloadFilePath`. |
| `DeleteBlobFile(blobFileName)` | Deletes the named blob if it exists. |
| `ListAllItemsBlobs()` | Returns the names of every blob in the container. |

## Dependency-injection registration

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDeepSigmaAzureBlobStorage(
    connectionString: builder.Configuration.GetConnectionString("AzureStorage")!,
    containerName:    "documents");
```

Registers `BlobStorageApi` as a singleton bound to the named container. The underlying `BlobContainerClient` is reused across calls.

If your app needs multiple containers, register one `BlobStorageApi` per container using keyed services (`AddKeyedSingleton(...)`).

## Quick start

```csharp
using DeepSigma.DataAccess.AzureBlobStorage;

var blobs = new BlobStorageApi(
    connectionString: "<azure-storage-connection-string>",
    blobContainerName: "documents");

// Upload (creates the container if needed)
await blobs.UploadToBlob("./report.pdf", allowOverwrite: true);

// List
List<string> names = await blobs.ListAllItemsBlobs();

// Download
await blobs.DownloadFromBlob("report.pdf", "./downloads/report.pdf");

// Delete
await blobs.DeleteBlobFile("report.pdf");
```

## Health checks

Pairs with the `Microsoft.Extensions.Diagnostics.HealthChecks` system. The check verifies that the named container exists on the configured storage account.

```csharp
services.AddHealthChecks()
    .AddDeepSigmaAzureBlobStorage(connectionString, "documents", tags: new[] { "readiness" });

app.MapHealthChecks("/health");
```

Optional arguments (all named): `name` (default `"deepsigma_azureblob"`), `failureStatus` (default `Unhealthy`), `tags`, `timeout` (recommended: `TimeSpan.FromSeconds(5)`).

A missing container is reported as unhealthy (not just "the storage account is reachable") — because every other method on `BlobStorageApi` is bound to that container, and the rest of the API surface won't work without it.

## Notes

- **Upload blob name.** The uploaded blob is named after `Path.GetFileName(filePath)` — directory structure is **not** preserved. If you need a virtual folder hierarchy (e.g. `2025/05/report.pdf`), pass the desired full key as the filename and arrange your source file accordingly, or extend the API.
- **Connection lifetime.** `BlobStorageApi` holds a long-lived `BlobContainerClient` for the lifetime of the instance. The Azure SDK clients are thread-safe and designed to be reused.
- **Cancellation.** All methods accept an optional `CancellationToken`. It is forwarded to the underlying Azure SDK calls.
- **Listing is eager.** `ListAllItemsBlobs` materialises every blob name in memory. For large containers, prefer paging via the underlying `containerClient.GetBlobsAsync()` directly.

## License

MIT

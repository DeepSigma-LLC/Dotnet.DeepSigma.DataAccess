using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace DeepSigma.DataAccess.Database
{
    public class BlobStorageAPI
    {
        private string ConnectionString { get; set; }
        private string ContainerName { get; set; }
        public BlobStorageAPI(string connectionString, string blobContainerName)
        {
            this.ConnectionString = connectionString;
            this.ContainerName = blobContainerName;
        }

        /// <summary>
        /// Uploads the file to the blob container.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="allowOverwrite"></param>
        /// <returns></returns>
        public async Task UploadToBlob(string filePath, bool allowOverwrite = false)
        {
            string file = Path.GetFileName(filePath);
            BlobServiceClient blobServiceClient = new(ConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);

            await containerClient.CreateIfNotExistsAsync();
            BlobClient blobClient = containerClient.GetBlobClient(file);
            using FileStream uploadFileStream = File.OpenRead(filePath);
            await blobClient.UploadAsync(uploadFileStream, overwrite: allowOverwrite);
            uploadFileStream.Close();
        }

        /// <summary>
        /// Downloads the blob from the container to the specified file path.
        /// </summary>
        /// <param name="blobName"></param>
        /// <param name="downloadFilePath"></param>
        /// <returns></returns>
        public async Task DownloadFromBlob(string blobFileName, string downloadFilePath)
        {
            BlobServiceClient blobServiceClient = new(ConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobFileName);
            BlobDownloadInfo download = await blobClient.DownloadAsync();
            using (FileStream downloadFileStream = File.OpenWrite(downloadFilePath))
            {
                await download.Content.CopyToAsync(downloadFileStream);
            }
        }

        /// <summary>
        /// Deletes the blob from the container.
        /// </summary>
        /// <param name="blobName"></param>
        /// <returns></returns>
        public async Task DeleteBlobFile(string blobFileName)
        {
            BlobServiceClient blobServiceClient = new(ConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobFileName);
            await blobClient.DeleteIfExistsAsync();
        }

        /// <summary>
        /// Lists the blobs in the container.
        /// </summary>
        /// <returns></returns>
        public async Task<List<string>> ListAllItemsBlobs()
        {
            BlobServiceClient blobServiceClient = new(ConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
            List<string> blobs = new();
            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
            {
                blobs.Add(blobItem.Name);
            }
            return blobs;
        }

    }
}

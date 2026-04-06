using Azure.Storage.Blobs;

namespace BlogAPI
{
    public class BlobStorageService
    {
        private readonly BlobContainerClient? _container;

        public BlobStorageService(IConfiguration configuration)
        {
            // The connection string comes from Key Vault (stored there in Phase 2)
            var connectionString = configuration["StorageConnectionString"];
            if (!string.IsNullOrEmpty(connectionString))
            {
                var serviceClient = new BlobServiceClient(connectionString);
                _container = serviceClient.GetBlobContainerClient("uploads");
            }
        }

        /// Upload a file to the "uploads" container and return its URL
        public async Task<string> UploadFileAsync(Stream fileStream, string fileName)
        {
            var blobClient = _container.GetBlobClient(fileName);
            await blobClient.UploadAsync(fileStream, overwrite: true);
            return blobClient.Uri.ToString();
        }

        /// List all files in the "uploads" container
        public async Task<IEnumerable<string>> ListFilesAsync()
        {
            var files = new List<string>();
            await foreach (var blob in _container.GetBlobsAsync())
                files.Add(blob.Name);
            return files;
        }

        /// Delete a file by name
        public async Task DeleteFileAsync(string fileName)
        {
            var blobClient = _container.GetBlobClient(fileName);
            await blobClient.DeleteIfExistsAsync();
        }
    }
}

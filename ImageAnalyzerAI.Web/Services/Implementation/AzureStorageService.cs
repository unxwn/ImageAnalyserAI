namespace ImageAnalyzerAI.Web.Services.Implementation
{
    using Azure.Storage.Blobs;
    using Azure.Storage.Queues;
    using ImageAnalyzerAI.Web.Models;
    using ImageAnalyzerAI.Web.Services.Abstraction;
    using System.Text.Json;

    public class AzureStorageService : IStorageService
    {
        private readonly BlobContainerClient _imagesContainer;
        private readonly BlobContainerClient _metadataContainer;
        private readonly QueueClient _queueClient;

        public AzureStorageService(BlobServiceClient blobServiceClient, IConfiguration configuration)
        {
            string? connectionString = configuration.GetValue<string>("AzureStorage:ConnectionString") ?? 
                throw new InvalidOperationException("AzureStorage:ConnectionString must be configured.");

            var blobService = new BlobServiceClient(connectionString);

            _imagesContainer = blobService.GetBlobContainerClient(configuration.GetValue<string>("AzureStorage:ImagesContainer")
                ?? throw new InvalidOperationException("AzureStorage:ImagesContainer must be configured."));

            _metadataContainer = blobService.GetBlobContainerClient(configuration.GetValue<string>("AzureStorage:MetadataContainer") ??
                throw new InvalidOperationException("AzureStorage:MetadataContainer must be configured."));
                

            _imagesContainer.CreateIfNotExists();
            _metadataContainer.CreateIfNotExists();

            _queueClient = new QueueClient(connectionString, configuration.GetValue<string>("AzureStorage:QueueName") ??
                throw new InvalidOperationException("AzureStorage:QueueName must be configured."));
            _queueClient.CreateIfNotExists();
        }

        public async Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
        {
            var blobClient = _imagesContainer.GetBlobClient(fileName);
            stream.Position = 0;
            await blobClient.UploadAsync(stream, new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);
            return blobClient.Name;
        }

        public async Task EnqueueMessageAsync(string blobName, CancellationToken ct = default)
        {
            var message = JsonSerializer.Serialize(new { blobName });
            await _queueClient.SendMessageAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(message)), cancellationToken: ct);
        }

        public async Task<IEnumerable<ImageItem>> ListImagesAsync(string? search = null, CancellationToken ct = default)
        {
            var result = new List<ImageItem>();
            await foreach (var blob in _imagesContainer.GetBlobsAsync(cancellationToken: ct))
            {
                if (!string.IsNullOrEmpty(search))
                {
                    if (!blob.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                var blobClient = _imagesContainer.GetBlobClient(blob.Name);

                // build URL - using blobClient.Uri
                var url = blobClient.Uri.ToString();

                // try to read metadata json from metadata container
                string? description = null;
                bool analyzed = false;
                var metaBlobClient = _metadataContainer.GetBlobClient(blob.Name + ".json");
                if (await metaBlobClient.ExistsAsync(ct))
                {
                    var metaResponse = await metaBlobClient.DownloadContentAsync(ct);
                    try
                    {
                        var json = metaResponse.Value.Content.ToString();
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("description", out var desc))
                            description = desc.GetString();
                        analyzed = true;
                    }
                    catch { /* ignore parse errors */ }
                }

                result.Add(new ImageItem
                {
                    BlobName = blob.Name,
                    Url = url,
                    Analyzed = analyzed,
                    Description = description,
                    UploadedAt = blob.Properties.CreatedOn ?? DateTimeOffset.UtcNow
                });
            }

            return result.OrderByDescending(i => i.UploadedAt);
        }
    }

}

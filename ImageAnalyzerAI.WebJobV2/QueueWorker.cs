using Azure;
using Azure.AI.Vision.ImageAnalysis;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using System.Text;
using System.Text.Json;

namespace ImageAnalyzerAI.WebJobV2
{
    public class QueueWorker : BackgroundService
    {
        private readonly ILogger<QueueWorker> _logger;
        private readonly IConfiguration _config;

        public QueueWorker(ILogger<QueueWorker> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var connStr = _config["AzureStorage:ConnectionString"];
            var queueName = _config["AzureStorage:QueueName"] ?? "images-queue";

            var queueClient = new QueueClient(connStr, queueName);
            await queueClient.CreateIfNotExistsAsync();

            _logger.LogInformation("QueueWorker started, listening on {queue}", queueName);

            while (!stoppingToken.IsCancellationRequested)
            {
                var msg = await queueClient.ReceiveMessageAsync(TimeSpan.FromSeconds(30), stoppingToken);

                if (msg.Value != null)
                {
                    try
                    {
                        await ProcessMessageAsync(msg.Value.MessageText, stoppingToken);
                        await queueClient.DeleteMessageAsync(msg.Value.MessageId, msg.Value.PopReceipt, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process queue message: {msg}", msg.Value.MessageText);
                    }
                }
                else
                {
                    await Task.Delay(2000, stoppingToken);
                }
            }
        }

        private async Task ProcessMessageAsync(string base64Message, CancellationToken ct)
        {
            var storageConn = _config["AzureStorage:ConnectionString"];
            var imagesContainerName = _config["AzureStorage:ImagesContainer"] ?? "images";
            var metadataContainerName = _config["AzureStorage:MetadataContainer"] ?? "metadata";
            var visionEndpoint = new Uri(_config["AzureVision:Endpoint"]!);
            var visionKey = new AzureKeyCredential(_config["AzureVision:Key"]!);

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64Message));
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("blobName", out var blobEl))
            {
                _logger.LogWarning("Message does not contain blobName: {msg}", json);
                return;
            }

            var blobName = blobEl.GetString();
            _logger.LogInformation("Processing blob {blob}", blobName);

            var blobService = new BlobServiceClient(storageConn);
            var imagesContainer = blobService.GetBlobContainerClient(imagesContainerName);
            var metadataContainer = blobService.GetBlobContainerClient(metadataContainerName);

            await imagesContainer.CreateIfNotExistsAsync(cancellationToken: ct);
            await metadataContainer.CreateIfNotExistsAsync(cancellationToken: ct);

            var blobClient = imagesContainer.GetBlobClient(blobName);
            if (!await blobClient.ExistsAsync(ct))
            {
                _logger.LogWarning("Blob not found: {blob}", blobName);
                return;
            }

            var downloadResp = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
            using var stream = downloadResp.Value.Content;

            var visionClient = new ImageAnalysisClient(visionEndpoint, visionKey);

            var response = await visionClient.AnalyzeAsync(
                BinaryData.FromStream(stream),
                VisualFeatures.Caption | VisualFeatures.Tags | VisualFeatures.Objects | VisualFeatures.Read,
                cancellationToken: ct
);

            var result = response.Value;

            var description = result.Caption?.Text;

            var tags = result.Tags?.Values.Select(t => t.Name).ToArray();

            // Objects
            //var objects = result.Objects?.Values.Select(o => new { o.Name, o.Confidence }).ToArray();

            var objects = result.Objects?.Values.Select(o => new
            {
                Name = o.Tags.FirstOrDefault()?.Name,
                Confidence = o.Tags.FirstOrDefault()?.Confidence,
                BoundingBox = o.BoundingBox
            }).ToArray();

            string? text = null;
            if (result.Read != null)
            {
                text = string.Join(" ",
                    result.Read.Blocks.SelectMany(b => b.Lines).Select(l => l.Text));
            }

            var meta = new
            {
                blob = blobName,
                description,
                tags,
                objects,
                text
            };

            var metaJson = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });

            var metaBlobClient = metadataContainer.GetBlobClient(blobName + ".json");
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(metaJson));
            await metaBlobClient.UploadAsync(ms, overwrite: true, cancellationToken: ct);

            _logger.LogInformation("Saved metadata for {blob}", blobName);
        }
    }
}

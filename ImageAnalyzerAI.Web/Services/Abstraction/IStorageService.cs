using ImageAnalyzerAI.Web.Models;

namespace ImageAnalyzerAI.Web.Services.Abstraction
{
    public interface IStorageService
    {
        Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default);
        Task EnqueueMessageAsync(string blobName, CancellationToken ct = default);
        Task<IEnumerable<ImageItem>> ListImagesAsync(string? search = null, CancellationToken ct = default);
    }
}

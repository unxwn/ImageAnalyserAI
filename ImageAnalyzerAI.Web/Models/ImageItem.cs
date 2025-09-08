namespace ImageAnalyzerAI.Web.Models
{
    public class ImageItem
    {
        public string BlobName { get; set; } = default!;
        public string Url { get; set; } = default!;
        public bool Analyzed { get; set; }
        public string? Description { get; set; }
        public DateTimeOffset UploadedAt { get; set; }
    }
}

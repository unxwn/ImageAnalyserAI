using System.ComponentModel.DataAnnotations;

namespace ImageAnalyzerAI.Web.Models
{
    public class ImageUploadDto
    {
        [Required]
        public IFormFile File { get; set; } = default!;
    }
}

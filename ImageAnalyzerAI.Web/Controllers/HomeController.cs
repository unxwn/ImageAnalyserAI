using ImageAnalyzerAI.Web.Models;
using ImageAnalyzerAI.Web.Services.Abstraction;
using Microsoft.AspNetCore.Mvc;

namespace ImageAnalyzerAI.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IStorageService _storage;

        public HomeController(IStorageService storage)
        {
            _storage = storage;
        }

        public async Task<IActionResult> Index(string? q)
        {
            var items = await _storage.ListImagesAsync(q);
            ViewData["Query"] = q ?? string.Empty;
            return View(items);
        }

        public  IActionResult Privacy()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(ImageUploadDto dto)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "No file uploaded.";
                return RedirectToAction("Index");
            }

            var ext = Path.GetExtension(dto.File.FileName);
            var blobName = $"{Guid.NewGuid()}{ext}";
            using var ms = new MemoryStream();
            await dto.File.CopyToAsync(ms);
            ms.Position = 0;
            await _storage.UploadFileAsync(ms, blobName, dto.File.ContentType);
            await _storage.EnqueueMessageAsync(blobName);

            TempData["Message"] = "Image uploaded. Analysis will run shortly.";
            return RedirectToAction("Index");
        }
    }
}

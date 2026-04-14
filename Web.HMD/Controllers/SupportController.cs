using LedApp.Application.Abstractions;
using LedApp.Application.DTOs;
using LedApp.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Web.HMD.Models;

namespace Web.HMD.Controllers
{
    public class SupportController : Controller
    {
        private const string AdminSessionKey = "IsAdminLoggedIn";
        private readonly IPanelSupportService _panelSupportService;
        private readonly IPanelLibraryPathProvider _panelLibraryPathProvider;

        public SupportController(
            IPanelSupportService panelSupportService,
            IPanelLibraryPathProvider panelLibraryPathProvider)
        {
            _panelSupportService = panelSupportService;
            _panelLibraryPathProvider = panelLibraryPathProvider;
        }

        [HttpGet]
        public IActionResult Upload()
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Upload(string pValue, string chipsetValue, string decoderValue, IFormFile file)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            if (file == null || file.Length == 0)
            {
                TempData["UploadMessage"] = "Lutfen bir dosya secin.";
                return RedirectToAction(nameof(Upload));
            }

            byte[] bytes;
            await using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                bytes = memoryStream.ToArray();
            }

            var result = await _panelSupportService.UploadAsync(new PanelSupportUploadRequest
            {
                PValue = pValue,
                ChipsetValue = chipsetValue,
                DecoderValue = decoderValue,
                FileName = file.FileName,
                FileContent = bytes
            });

            TempData["UploadMessage"] = result.Message;
            return RedirectToAction(nameof(Upload));
        }

        [HttpGet]
        public IActionResult ScanUpload()
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ScanUpload(string pValue, string chipsetValue, string decoderValue, List<IFormFile> files)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            if (files == null || files.Count == 0)
            {
                TempData["ScanUploadMessage"] = "Lutfen en az bir dosya secin.";
                return RedirectToAction(nameof(ScanUpload));
            }

            var savedCount = 0;
            var skippedCount = 0;

            foreach (var file in files)
            {
                if (file == null || file.Length == 0)
                {
                    skippedCount++;
                    continue;
                }

                byte[] bytes;
                await using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    bytes = memoryStream.ToArray();
                }

                var result = await _panelSupportService.UploadAsync(new PanelSupportUploadRequest
                {
                    PValue = pValue,
                    ChipsetValue = chipsetValue,
                    DecoderValue = decoderValue,
                    FileName = file.FileName,
                    FileContent = bytes
                });

                if (result.IsSuccess)
                {
                    savedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            TempData["ScanUploadMessage"] = $"Tarama yukleme tamamlandi. Kaydedilen: {savedCount}, Atlanan: {skippedCount}";
            return RedirectToAction(nameof(ScanUpload));
        }

        [HttpPost]
        public async Task<IActionResult> ImportFromLibrary(string? rootPath)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            var libraryRoot = string.IsNullOrWhiteSpace(rootPath)
                ? _panelLibraryPathProvider.GetDefaultLibraryRootPath()
                : rootPath;

            if (string.IsNullOrWhiteSpace(libraryRoot) || !Directory.Exists(libraryRoot))
            {
                TempData["UploadMessage"] = "Panel kutuphanesi klasoru bulunamadi.";
                return RedirectToAction(nameof(Upload));
            }

            var importResult = await _panelSupportService.ImportFromLibraryAsync(libraryRoot);
            TempData["UploadMessage"] = $"Import tamamlandi. Yeni: {importResult.ImportedCount}, Guncellenen: {importResult.UpdatedCount}, Atlanan: {importResult.SkippedCount}";
            return RedirectToAction(nameof(Upload));
        }

        [HttpGet]
        public async Task<IActionResult> GetPanelFiles(string pValue, string chipsetValue, string decoderValue)
        {
            var panelFiles = await _panelSupportService.GetPanelFilesAsync(pValue, chipsetValue, decoderValue);
            var files = panelFiles.Select(x => new SupportFileResultViewModel
            {
                Id = x.Id,
                FileType = x.FileType,
                FileName = x.FileName,
                FileUrl = Url.Action(nameof(DownloadFile), "Support", new { id = x.Id }) ?? string.Empty
            }).ToList();

            return Json(files);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadFile(int id)
        {
            var item = await _panelSupportService.GetFileByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            return File(item.Content, "application/octet-stream", item.FileName);
        }

        private bool IsAdminLoggedIn()
        {
            return string.Equals(HttpContext.Session.GetString(AdminSessionKey), "true", StringComparison.Ordinal);
        }
    }
}

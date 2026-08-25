using LedApp.Application.Abstractions;
using LedApp.Application.DTOs;
using LedApp.Application.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using Web.HMD.Models;
using Web.HMD.Services;

namespace Web.HMD.Controllers
{
    public class SupportController : Controller
    {
        private const string AdminSessionKey = "IsAdminLoggedIn";
        private readonly IPanelSupportService _panelSupportService;
        private readonly IPanelLibraryPathProvider _panelLibraryPathProvider;
        private readonly IUpdateHexCatalogService _updateHexCatalogService;

        public SupportController(
            IPanelSupportService panelSupportService,
            IPanelLibraryPathProvider panelLibraryPathProvider,
            IUpdateHexCatalogService updateHexCatalogService)
        {
            _panelSupportService = panelSupportService;
            _panelLibraryPathProvider = panelLibraryPathProvider;
            _updateHexCatalogService = updateHexCatalogService;
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
        public async Task<IActionResult> ScanUpload()
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            ViewBag.UpdateMappings = await _updateHexCatalogService.GetMappingsAsync();
            ViewBag.UpdateHexFiles = await _updateHexCatalogService.GetHexFilesAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ScanUploadSingle(IFormFile file)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            if (file == null || file.Length == 0)
            {
                TempData["ScanUploadMessage"] = "Lutfen tekli yukleme icin bir dosya secin.";
                return RedirectToAction(nameof(ScanUpload));
            }

            var savedCount = 0;
            var skippedCount = 0;

            var isSaved = await SaveScanFileFromNameAsync(file);
            if (isSaved)
            {
                savedCount++;
            }
            else
            {
                skippedCount++;
            }

            TempData["ScanUploadMessage"] = $"Tekli yukleme tamamlandi. Kaydedilen: {savedCount}, Atlanan: {skippedCount}";
            return RedirectToAction(nameof(ScanUpload));
        }

        [HttpPost]
        public async Task<IActionResult> ScanUpload(List<IFormFile> files)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            if (files == null || files.Count == 0)
            {
                TempData["ScanUploadMessage"] = "Lutfen toplu yukleme icin en az bir dosya secin.";
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

                var isSaved = await SaveScanFileFromNameAsync(file);
                if (isSaved)
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
        public async Task<IActionResult> UploadUpdateHex(string versionLabel, IFormFile hexFile)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            var result = await _updateHexCatalogService.UploadHexAsync(versionLabel, hexFile);
            TempData["ScanUploadMessage"] = result.Message;
            return RedirectToAction(nameof(ScanUpload));
        }

        [HttpPost]
        public async Task<IActionResult> SaveUpdateHexMappings(string mappingTable)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            var result = await _updateHexCatalogService.ImportMappingsAsync(mappingTable);
            TempData["ScanUploadMessage"] = $"Tablo guncellendi. Eklenen/Guncellenen: {result.AddedOrUpdatedCount}, Atlanan: {result.SkippedCount}";
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
                FileUrl = Url.Action(nameof(DownloadFile), "Support", new { id = x.Id }) ?? string.Empty,
                FileRole = "scan"
            }).ToList();

            var updateMatch = await _updateHexCatalogService.FindMatchAsync(chipsetValue, decoderValue);
            if (updateMatch != null)
            {
                files.Add(new SupportFileResultViewModel
                {
                    Id = 0,
                    FileType = "hex",
                    FileName = updateMatch.FileName,
                    FileUrl = updateMatch.FileUrl,
                    FileRole = "update",
                    VersionLabel = updateMatch.VersionLabel
                });
            }

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

        private async Task<bool> SaveScanFileFromNameAsync(IFormFile file)
        {
            if (!TryParsePanelValuesFromFileName(file.FileName, out var pValue, out var chipsetValue, out var decoderValue))
            {
                return false;
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

            return result.IsSuccess;
        }

        private static bool TryParsePanelValuesFromFileName(string fileName, out string pValue, out string chipsetValue, out string decoderValue)
        {
            pValue = string.Empty;
            chipsetValue = string.Empty;
            decoderValue = string.Empty;

            var safeFileName = Path.GetFileNameWithoutExtension(fileName)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                return false;
            }

            var match = Regex.Match(
                safeFileName,
                @"(?<p>p\d+(?:[.,]\d+)?)\s*[-_+\s]+\s*(?<chip>[a-z0-9]+)\s*[-_+\s]+\s*(?<dec>[a-z0-9]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (!match.Success)
            {
                return false;
            }

            pValue = match.Groups["p"].Value.Replace(',', '.').ToLowerInvariant();
            chipsetValue = match.Groups["chip"].Value.ToLowerInvariant();
            decoderValue = match.Groups["dec"].Value.ToLowerInvariant();

            return !string.IsNullOrWhiteSpace(pValue) &&
                   !string.IsNullOrWhiteSpace(chipsetValue) &&
                   !string.IsNullOrWhiteSpace(decoderValue);
        }
    }
}

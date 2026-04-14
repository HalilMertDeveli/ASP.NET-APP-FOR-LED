using Entity.HMD.Context;
using Entity.HMD.Entity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.HMD.Models;

namespace Web.HMD.Controllers
{
    public class SupportController : Controller
    {
        private readonly LedContext _context;
        private readonly IWebHostEnvironment _environment;

        public SupportController(LedContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        public IActionResult Upload()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Upload(string panelType, string chipsetValue, string decoderValue, string pValue, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["UploadMessage"] = "Lutfen bir dosya secin.";
                return RedirectToAction(nameof(Upload));
            }

            var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "panel-files");
            Directory.CreateDirectory(uploadsRoot);

            var safeFileName = Path.GetFileName(file.FileName);
            var uniqueFileName = $"{Guid.NewGuid()}_{safeFileName}";
            var absolutePath = Path.Combine(uploadsRoot, uniqueFileName);

            await using (var stream = System.IO.File.Create(absolutePath))
            {
                await file.CopyToAsync(stream);
            }

            var dbItem = new PanelSupportFile
            {
                PanelType = panelType?.Trim() ?? string.Empty,
                ChipsetValue = chipsetValue?.Trim() ?? string.Empty,
                DecoderValue = decoderValue?.Trim() ?? string.Empty,
                PValue = pValue?.Trim() ?? string.Empty,
                FileName = safeFileName,
                FilePath = $"/uploads/panel-files/{uniqueFileName}"
            };

            _context.PanelSupportFiles.Add(dbItem);
            await _context.SaveChangesAsync();

            TempData["UploadMessage"] = "Dosya veritabanina kaydedildi.";
            return RedirectToAction(nameof(Upload));
        }

        [HttpGet]
        public async Task<IActionResult> GetPanelFiles(string panelType, string chipsetValue, string decoderValue, string pValue)
        {
            var query = _context.PanelSupportFiles.AsNoTracking().AsQueryable();

            query = query.Where(x =>
                x.PanelType == panelType &&
                x.ChipsetValue == chipsetValue &&
                x.DecoderValue == decoderValue &&
                x.PValue == pValue);

            var files = await query
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new SupportFileResultViewModel
                {
                    FileName = x.FileName,
                    FileUrl = x.FilePath
                })
                .ToListAsync();

            return Json(files);
        }
    }
}

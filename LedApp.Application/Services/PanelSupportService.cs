using Entity.HMD.Entity;
using LedApp.Application.DTOs;
using LedApp.Domain.Interfaces;

namespace LedApp.Application.Services
{
    public class PanelSupportService : IPanelSupportService
    {
        private readonly IGenericRepository<PanelSupportFile> _panelSupportRepository;

        public PanelSupportService(IGenericRepository<PanelSupportFile> panelSupportRepository)
        {
            _panelSupportRepository = panelSupportRepository;
        }

        public async Task<PanelSupportOperationResult> UploadAsync(PanelSupportUploadRequest request)
        {
            var safeFileName = Path.GetFileName(request.FileName);
            var extension = ResolveFileType(safeFileName, request.FileContent);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return new PanelSupportOperationResult
                {
                    IsSuccess = false,
                    Message = "Dosya tipi algilanamadi. Lutfen .rcvp veya .hex dosyasi yukleyin."
                };
            }
            safeFileName = EnsureFileNameHasExtension(safeFileName, extension);

            var normalizedP = Normalize(request.PValue);
            var normalizedChipset = Normalize(request.ChipsetValue);
            var normalizedDecoder = Normalize(request.DecoderValue);

            var existing = await _panelSupportRepository.FirstOrDefaultAsync(x =>
                x.PValue == normalizedP &&
                x.ChipsetValue == normalizedChipset &&
                x.DecoderValue == normalizedDecoder &&
                x.FileType == extension);

            if (existing == null)
            {
                await _panelSupportRepository.AddAsync(new PanelSupportFile
                {
                    PValue = normalizedP,
                    ChipsetValue = normalizedChipset,
                    DecoderValue = normalizedDecoder,
                    FileName = safeFileName,
                    FileType = extension,
                    FilePath = "manual-upload",
                    FileContent = request.FileContent
                });
            }
            else
            {
                existing.FileName = safeFileName;
                existing.FilePath = "manual-upload";
                existing.FileContent = request.FileContent;
                existing.UpdatedDate = DateTime.UtcNow;
                _panelSupportRepository.Update(existing);
            }

            await _panelSupportRepository.SaveChangesAsync();
            return new PanelSupportOperationResult { IsSuccess = true, Message = "Dosya veritabanina kaydedildi." };
        }

        public async Task<PanelSupportImportResult> ImportFromLibraryAsync(string libraryRoot)
        {
            var importedCount = 0;
            var updatedCount = 0;
            var skippedCount = 0;

            var pFolders = Directory.GetDirectories(libraryRoot);
            foreach (var pFolder in pFolders)
            {
                var pFolderName = Normalize(Path.GetFileName(pFolder));
                if (!pFolderName.StartsWith("p", StringComparison.Ordinal))
                {
                    continue;
                }

                var comboFolders = Directory.GetDirectories(pFolder);
                foreach (var comboFolder in comboFolders)
                {
                    var comboName = Normalize(Path.GetFileName(comboFolder));
                    if (!TryParsePanelDefinition(comboName, pFolderName, out var pValue, out var chipset, out var decoder))
                    {
                        skippedCount++;
                        continue;
                    }

                    var files = Directory.GetFiles(comboFolder)
                        .Where(path =>
                        {
                            var ext = Path.GetExtension(path).ToLowerInvariant();
                            // Accept extensionless files as well; file type is inferred later.
                            return ext == ".rcvp" || ext == ".hex" || string.IsNullOrWhiteSpace(ext);
                        });

                    foreach (var filePath in files)
                    {
                        var fileName = Path.GetFileName(filePath);
                        var content = await File.ReadAllBytesAsync(filePath);
                        var fileType = ResolveFileType(fileName, content);
                        if (string.IsNullOrWhiteSpace(fileType))
                        {
                            skippedCount++;
                            continue;
                        }
                        fileName = EnsureFileNameHasExtension(fileName, fileType);

                        var existing = await _panelSupportRepository.FirstOrDefaultAsync(x =>
                            x.PValue == pValue &&
                            x.ChipsetValue == chipset &&
                            x.DecoderValue == decoder &&
                            x.FileType == fileType);

                        if (existing == null)
                        {
                            await _panelSupportRepository.AddAsync(new PanelSupportFile
                            {
                                PValue = pValue,
                                ChipsetValue = chipset,
                                DecoderValue = decoder,
                                FileName = fileName,
                                FileType = fileType,
                                FilePath = filePath,
                                FileContent = content
                            });
                            importedCount++;
                        }
                        else
                        {
                            existing.FileName = fileName;
                            existing.FilePath = filePath;
                            existing.FileContent = content;
                            existing.UpdatedDate = DateTime.UtcNow;
                            _panelSupportRepository.Update(existing);
                            updatedCount++;
                        }
                    }
                }
            }

            await _panelSupportRepository.SaveChangesAsync();
            return new PanelSupportImportResult
            {
                ImportedCount = importedCount,
                UpdatedCount = updatedCount,
                SkippedCount = skippedCount
            };
        }

        public async Task<IReadOnlyList<PanelSupportFileDto>> GetPanelFilesAsync(string pValue, string chipsetValue, string decoderValue)
        {
            var normalizedP = Normalize(pValue);
            var normalizedChipset = Normalize(chipsetValue);
            var normalizedDecoder = Normalize(decoderValue);

            var matchedFiles = await _panelSupportRepository.FindAsync(x =>
                x.PValue == normalizedP &&
                x.ChipsetValue == normalizedChipset &&
                x.DecoderValue == normalizedDecoder);

            return matchedFiles
                .OrderBy(x => x.FileType)
                .Select(x => new PanelSupportFileDto
                {
                    Id = x.Id,
                    FileType = x.FileType,
                    FileName = x.FileName
                })
                .ToList();
        }

        public async Task<PanelSupportDownloadDto?> GetFileByIdAsync(int id)
        {
            var item = await _panelSupportRepository.GetByIdAsync(id);
            if (item == null || item.FileContent.Length == 0)
            {
                return null;
            }

            return new PanelSupportDownloadDto
            {
                FileName = item.FileName,
                FileType = item.FileType,
                Content = item.FileContent
            };
        }

        private static string Normalize(string? value)
        {
            return value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private static string ResolveFileType(string fileName, byte[] content)
        {
            var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
            if (extension == "rcvp" || extension == "hex")
            {
                return extension;
            }

            // Intel HEX files are text lines usually starting with ':'.
            if (content != null && content.Length > 0 && content[0] == (byte)':')
            {
                return "hex";
            }

            // If extension is missing/unknown and content is binary-like, use rcvp fallback.
            if (content != null && content.Length > 0)
            {
                return "rcvp";
            }

            return string.Empty;
        }

        private static string EnsureFileNameHasExtension(string fileName, string extension)
        {
            if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
            {
                return $"{fileName}.{extension}";
            }

            return fileName;
        }

        private static bool TryParsePanelDefinition(
            string comboName,
            string pFolderName,
            out string pValue,
            out string chipset,
            out string decoder)
        {
            pValue = string.Empty;
            chipset = string.Empty;
            decoder = string.Empty;

            // Supports both old format (p2.5+1065s+2012) and new format (p2.5-1065s-2012).
            var parts = comboName
                .Split(['+', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
                .Select(Normalize)
                .ToArray();

            if (parts.Length == 3)
            {
                pValue = parts[0];
                chipset = parts[1];
                decoder = parts[2];
                return !string.IsNullOrWhiteSpace(pValue) &&
                       !string.IsNullOrWhiteSpace(chipset) &&
                       !string.IsNullOrWhiteSpace(decoder);
            }

            if (parts.Length == 2 && pFolderName.StartsWith("p", StringComparison.Ordinal))
            {
                pValue = pFolderName;
                chipset = parts[0];
                decoder = parts[1];
                return true;
            }

            return false;
        }
    }
}

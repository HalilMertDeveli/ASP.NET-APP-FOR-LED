using System.Text.RegularExpressions;
using Entity.HMD.Context;
using Entity.HMD.Entity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Web.HMD.Services
{
    public class UpdateHexCatalogService : IUpdateHexCatalogService
    {
        private readonly LedContext _context;
        private readonly string _publicHexDirectory;

        public UpdateHexCatalogService(IWebHostEnvironment env, LedContext context)
        {
            _context = context;
            _publicHexDirectory = Path.Combine(env.WebRootPath, "update-hex");
        }

        public async Task<UpdateHexUploadResult> UploadHexAsync(string versionLabel, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return new UpdateHexUploadResult { IsSuccess = false, Message = "Lutfen bir .hex dosyasi secin." };
            }

            var normalizedVersion = NormalizeVersionLabel(versionLabel);
            if (string.IsNullOrWhiteSpace(normalizedVersion))
            {
                normalizedVersion = NormalizeVersionLabel(Path.GetFileNameWithoutExtension(file.FileName));
            }

            if (string.IsNullOrWhiteSpace(normalizedVersion))
            {
                return new UpdateHexUploadResult { IsSuccess = false, Message = "Versiyon etiketi bos olamaz. Orn: 11.28" };
            }

            if (!IsHexFile(file.FileName))
            {
                return new UpdateHexUploadResult { IsSuccess = false, Message = "Sadece .hex uzantili dosya kabul edilir." };
            }

            Directory.CreateDirectory(_publicHexDirectory);
            var safeName = $"{normalizedVersion}.hex";
            var absolutePath = Path.Combine(_publicHexDirectory, safeName);
            await using (var stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = $"/update-hex/{safeName}";
            var existing = await _context.UpdateHexFiles.FirstOrDefaultAsync(x => x.VersionLabel == normalizedVersion);
            if (existing == null)
            {
                _context.UpdateHexFiles.Add(new UpdateHexFile
                {
                    VersionLabel = normalizedVersion,
                    FileName = safeName,
                    RelativePath = relativePath,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                existing.FileName = safeName;
                existing.RelativePath = relativePath;
                existing.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return new UpdateHexUploadResult
            {
                IsSuccess = true,
                Message = $"Guncelleme HEX dosyasi kaydedildi. Versiyon: {normalizedVersion}"
            };
        }

        public async Task<UpdateHexMappingImportResult> ImportMappingsAsync(string tableText)
        {
            var lines = (tableText ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (lines.Count == 0)
            {
                return new UpdateHexMappingImportResult();
            }

            var addedOrUpdated = 0;
            var skipped = 0;

            foreach (var line in lines)
            {
                if (!TryParseMappingLine(line, out var lookupKeys, out var versionLabel))
                {
                    skipped++;
                    continue;
                }

                foreach (var lookupKey in lookupKeys)
                {
                    var existing = await _context.UpdateHexMappings.FirstOrDefaultAsync(x => x.LookupKey == lookupKey);
                    if (existing == null)
                    {
                        _context.UpdateHexMappings.Add(new UpdateHexMapping
                        {
                            LookupKey = lookupKey,
                            VersionLabel = versionLabel
                        });
                    }
                    else
                    {
                        existing.VersionLabel = versionLabel;
                    }

                    addedOrUpdated++;
                }
            }

            await _context.SaveChangesAsync();
            return new UpdateHexMappingImportResult
            {
                AddedOrUpdatedCount = addedOrUpdated,
                SkippedCount = skipped
            };
        }

        public async Task<UpdateHexMatchResult?> FindMatchAsync(string chipsetValue, string decoderValue)
        {
            var normalizedChipset = NormalizeLookup(chipsetValue);
            UpdateHexMapping? mapping = null;
            if (!string.IsNullOrWhiteSpace(normalizedChipset))
            {
                // Business rule: update HEX is selected only by chipset.
                mapping = await FindBestChipsetMappingAsync(normalizedChipset);
            }

            if (mapping == null)
            {
                return null;
            }

            var versionLabel = mapping.VersionLabel;
            var hexFile = await _context.UpdateHexFiles.FirstOrDefaultAsync(x => x.VersionLabel == versionLabel);
            if (hexFile == null)
            {
                return null;
            }

            return new UpdateHexMatchResult
            {
                LookupKey = mapping.LookupKey,
                VersionLabel = versionLabel,
                FileName = hexFile.FileName,
                FileUrl = hexFile.RelativePath
            };
        }

        private async Task<UpdateHexMapping?> FindBestChipsetMappingAsync(string chipset)
        {
            var mappings = await _context.UpdateHexMappings
                .AsNoTracking()
                .ToListAsync();

            if (mappings.Count == 0)
            {
                return null;
            }

            var direct = mappings.FirstOrDefault(x =>
                string.Equals(NormalizeLookup(x.LookupKey), chipset, StringComparison.Ordinal));
            if (direct != null)
            {
                return direct;
            }

            var chipsetAlnum = NormalizeLookupForCompare(chipset);
            var chipsetWithoutPrefix = RemoveLeadingLetters(chipsetAlnum);

            var prefixAware = mappings.FirstOrDefault(x =>
            {
                var keyAlnum = NormalizeLookupForCompare(x.LookupKey);
                if (string.Equals(keyAlnum, chipsetAlnum, StringComparison.Ordinal))
                {
                    return true;
                }

                var keyWithoutPrefix = RemoveLeadingLetters(keyAlnum);
                return !string.IsNullOrWhiteSpace(keyWithoutPrefix) &&
                       !string.IsNullOrWhiteSpace(chipsetWithoutPrefix) &&
                       string.Equals(keyWithoutPrefix, chipsetWithoutPrefix, StringComparison.Ordinal);
            });

            return prefixAware;
        }

        public async Task<IReadOnlyList<UpdateHexMappingEntry>> GetMappingsAsync()
        {
            return await _context.UpdateHexMappings
                .OrderBy(x => x.LookupKey)
                .Select(x => new UpdateHexMappingEntry
                {
                    LookupKey = x.LookupKey,
                    VersionLabel = x.VersionLabel
                })
                .ToListAsync();
        }

        public async Task<IReadOnlyList<UpdateHexFileEntry>> GetHexFilesAsync()
        {
            return await _context.UpdateHexFiles
                .OrderByDescending(x => x.UpdatedAtUtc)
                .Select(x => new UpdateHexFileEntry
                {
                    VersionLabel = x.VersionLabel,
                    FileName = x.FileName,
                    RelativePath = x.RelativePath,
                    UpdatedAtUtc = x.UpdatedAtUtc
                })
                .ToListAsync();
        }

        private static bool TryParseMappingLine(string line, out IReadOnlyList<string> lookupKeys, out string versionLabel)
        {
            lookupKeys = Array.Empty<string>();
            versionLabel = string.Empty;

            var separators = new[] { ';', ',', '|', '=', '\t' };
            string[] parts;
            if (line.Contains("->", StringComparison.Ordinal))
            {
                parts = line.Split("->", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                parts = line.Split(separators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            }

            if (parts.Length < 2)
            {
                return false;
            }

            // Supports:
            // 1) key,version
            // 2) version,key1/key2/key3
            // 3) Excel row paste: version<TAB>cell1<TAB>cell2...
            // 3) key1/key2 -> version
            var firstVersion = NormalizeVersionLabel(parts[0]);
            var lastVersion = NormalizeVersionLabel(parts[^1]);

            IEnumerable<string> rawKeyBlocks;
            if (!string.IsNullOrWhiteSpace(firstVersion))
            {
                versionLabel = firstVersion;
                rawKeyBlocks = parts.Skip(1);
            }
            else if (!string.IsNullOrWhiteSpace(lastVersion))
            {
                versionLabel = lastVersion;
                rawKeyBlocks = parts.Take(parts.Length - 1);
            }
            else
            {
                // Fallback for key,value style with no clear version edge.
                var secondVersion = NormalizeVersionLabel(parts[1]);
                if (string.IsNullOrWhiteSpace(secondVersion))
                {
                    return false;
                }
                versionLabel = secondVersion;
                rawKeyBlocks = new[] { parts[0] };
            }

            if (string.IsNullOrWhiteSpace(versionLabel))
            {
                return false;
            }

            var keys = rawKeyBlocks
                .SelectMany(block => block.Split(new[] { '/', '\\', '+', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(x => NormalizeLookup(x.Trim()))
                .Where(x => !string.IsNullOrWhiteSpace(x) && !IsPureVersionToken(x))
                .Distinct()
                .ToList();

            if (keys.Count == 0)
            {
                return false;
            }

            lookupKeys = keys;
            return true;
        }

        private static bool IsPureVersionToken(string token)
        {
            return !string.IsNullOrWhiteSpace(NormalizeVersionLabel(token)) &&
                   Regex.IsMatch(token, @"^\d+(?:[.,]\d+)?$", RegexOptions.CultureInvariant);
        }

        private static string NormalizeLookup(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string NormalizeLookupForCompare(string value)
        {
            var normalized = NormalizeLookup(value);
            return Regex.Replace(normalized, @"[^a-z0-9]", string.Empty);
        }

        private static string RemoveLeadingLetters(string value)
        {
            return Regex.Replace(value ?? string.Empty, @"^[a-z]+", string.Empty);
        }

        private static string NormalizeVersionLabel(string value)
        {
            var normalized = (value ?? string.Empty).Trim().Replace(',', '.');
            normalized = Regex.Replace(normalized, @"\s+", string.Empty);
            normalized = Regex.Replace(normalized, @"[^0-9.]", string.Empty);
            return normalized;
        }

        private static bool IsHexFile(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".hex";
        }
    }
}

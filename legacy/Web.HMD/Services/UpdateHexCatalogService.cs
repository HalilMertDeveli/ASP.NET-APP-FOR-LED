using System.Text.RegularExpressions;
using Entity.HMD.Context;
using Entity.HMD.Entity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Web.HMD.Services
{
    public class UpdateHexCatalogService : IUpdateHexCatalogService
    {
        private readonly LedContext _context;
        private readonly string _publicHexDirectory;
        private readonly string _chipsetMappingJsonPath;

        public UpdateHexCatalogService(IWebHostEnvironment env, LedContext context)
        {
            _context = context;
            _publicHexDirectory = Path.Combine(env.WebRootPath, "update-hex");
            _chipsetMappingJsonPath = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "LedApp.Application", "Utils", "chipset_mapping_v2.json"));
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
            var rawText = (tableText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return new UpdateHexMappingImportResult();
            }

            if (LooksLikeJson(rawText))
            {
                return await ImportMappingsFromJsonAsync(rawText);
            }

            var lines = rawText
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

        private async Task<UpdateHexMappingImportResult> ImportMappingsFromJsonAsync(string jsonText)
        {
            var normalizedEntries = new List<(string versionLabel, List<string> lookupKeys)>();
            var skipped = 0;

            try
            {
                using var doc = JsonDocument.Parse(jsonText);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        if (!TryExtractJsonMappingEntry(item, out var versionLabel, out var lookupKeys))
                        {
                            skipped++;
                            continue;
                        }

                        normalizedEntries.Add((versionLabel, lookupKeys));
                    }
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    // Supports key-value style:
                    // {
                    //   "11.28": ["1065s", "16169s"],
                    //   "13.80.hex": "5124/5166"
                    // }
                    foreach (var property in root.EnumerateObject())
                    {
                        var versionLabel = NormalizeVersionLabel(property.Name);
                        if (string.IsNullOrWhiteSpace(versionLabel))
                        {
                            skipped++;
                            continue;
                        }

                        var keys = ParseLookupKeysFromJsonElement(property.Value);
                        if (keys.Count == 0)
                        {
                            skipped++;
                            continue;
                        }

                        normalizedEntries.Add((versionLabel, keys));
                    }
                }
                else
                {
                    return new UpdateHexMappingImportResult { SkippedCount = 1 };
                }
            }
            catch (JsonException)
            {
                return new UpdateHexMappingImportResult { SkippedCount = 1 };
            }

            var addedOrUpdated = 0;
            foreach (var entry in normalizedEntries)
            {
                foreach (var lookupKey in entry.lookupKeys)
                {
                    var existing = await _context.UpdateHexMappings.FirstOrDefaultAsync(x => x.LookupKey == lookupKey);
                    if (existing == null)
                    {
                        _context.UpdateHexMappings.Add(new UpdateHexMapping
                        {
                            LookupKey = lookupKey,
                            VersionLabel = entry.versionLabel
                        });
                    }
                    else
                    {
                        existing.VersionLabel = entry.versionLabel;
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

            // Priority rule for chatbot: read chipset->hex mapping from application JSON file.
            if (!string.IsNullOrWhiteSpace(normalizedChipset))
            {
                var jsonVersion = await FindVersionFromChipsetJsonAsync(normalizedChipset);
                if (!string.IsNullOrWhiteSpace(jsonVersion))
                {
                    var jsonResult = await BuildMatchResultAsync(normalizedChipset, jsonVersion);
                    if (jsonResult != null)
                    {
                        return jsonResult;
                    }
                }
            }

            // Backward compatibility: if JSON has no match, fallback to DB mapping.
            UpdateHexMapping? mapping = null;
            if (!string.IsNullOrWhiteSpace(normalizedChipset))
            {
                mapping = await FindBestChipsetMappingAsync(normalizedChipset);
            }

            if (mapping == null || string.IsNullOrWhiteSpace(mapping.VersionLabel))
            {
                return null;
            }

            return await BuildMatchResultAsync(mapping.LookupKey, mapping.VersionLabel);
        }

        private async Task<UpdateHexMatchResult?> BuildMatchResultAsync(string lookupKey, string versionLabel)
        {
            var normalizedVersion = NormalizeVersionLabel(versionLabel);
            if (string.IsNullOrWhiteSpace(normalizedVersion))
            {
                return null;
            }

            var hexFile = await _context.UpdateHexFiles.FirstOrDefaultAsync(x => x.VersionLabel == normalizedVersion);
            if (hexFile == null)
            {
                var fallbackFileName = $"{normalizedVersion}.hex";
                var fallbackAbsolutePath = Path.Combine(_publicHexDirectory, fallbackFileName);
                if (!File.Exists(fallbackAbsolutePath))
                {
                    return null;
                }

                return new UpdateHexMatchResult
                {
                    LookupKey = lookupKey,
                    VersionLabel = normalizedVersion,
                    FileName = fallbackFileName,
                    FileUrl = $"/update-hex/{fallbackFileName}"
                };
            }

            return new UpdateHexMatchResult
            {
                LookupKey = lookupKey,
                VersionLabel = normalizedVersion,
                FileName = hexFile.FileName,
                FileUrl = hexFile.RelativePath
            };
        }

        private async Task<string?> FindVersionFromChipsetJsonAsync(string chipset)
        {
            if (string.IsNullOrWhiteSpace(chipset) || !File.Exists(_chipsetMappingJsonPath))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(_chipsetMappingJsonPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var chipsetAlnum = NormalizeLookupForCompare(chipset);
                var chipsetWithoutPrefix = RemoveLeadingLetters(chipsetAlnum);

                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (!TryGetPropertyIgnoreCase(property.Value, "mapped", out var mappedElement) ||
                        mappedElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var mapped = mappedElement.GetString() ?? string.Empty;
                    var mappedTokens = mapped
                        .Split(new[] { '/', '\\', '+', ' ', ',', ';', '|', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();

                    var hasChipsetMatch = mappedTokens.Any(token =>
                    {
                        var tokenAlnum = NormalizeLookupForCompare(token);
                        if (string.Equals(tokenAlnum, chipsetAlnum, StringComparison.Ordinal))
                        {
                            return true;
                        }

                        var tokenWithoutPrefix = RemoveLeadingLetters(tokenAlnum);
                        return !string.IsNullOrWhiteSpace(tokenWithoutPrefix) &&
                               !string.IsNullOrWhiteSpace(chipsetWithoutPrefix) &&
                               string.Equals(tokenWithoutPrefix, chipsetWithoutPrefix, StringComparison.Ordinal);
                    });

                    if (!hasChipsetMatch)
                    {
                        continue;
                    }

                    if (!TryGetPropertyIgnoreCase(property.Value, "hex_value", out var hexValueElement) ||
                        hexValueElement.ValueKind != JsonValueKind.String)
                    {
                        return null;
                    }

                    var version = NormalizeVersionLabel(hexValueElement.GetString() ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(version))
                    {
                        return version;
                    }
                }
            }
            catch (IOException)
            {
                return null;
            }
            catch (JsonException)
            {
                return null;
            }

            return null;
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

        private static bool TryExtractJsonMappingEntry(JsonElement item, out string versionLabel, out List<string> lookupKeys)
        {
            versionLabel = string.Empty;
            lookupKeys = new List<string>();

            if (item.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var versionCandidates = new[]
            {
                GetStringProperty(item, "versionLabel"),
                GetStringProperty(item, "version"),
                GetStringProperty(item, "hex"),
                GetStringProperty(item, "hexFile"),
                GetStringProperty(item, "hexDegeri"),
                GetStringProperty(item, "HEX Degeri"),
                GetStringProperty(item, "hex_degeri")
            };

            versionLabel = versionCandidates
                .Select(NormalizeVersionLabel)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

            var keyElements = new List<JsonElement>();
            TryAddProperty(item, "lookupKeys", keyElements);
            TryAddProperty(item, "lookupKey", keyElements);
            TryAddProperty(item, "chipset", keyElements);
            TryAddProperty(item, "chipsets", keyElements);
            TryAddProperty(item, "decoder", keyElements);
            TryAddProperty(item, "decoders", keyElements);
            TryAddProperty(item, "donusturulmusDeger", keyElements);
            TryAddProperty(item, "Dönüştürülmüş Değer", keyElements);
            TryAddProperty(item, "donusturulmus_deger", keyElements);

            foreach (var keyElement in keyElements)
            {
                lookupKeys.AddRange(ParseLookupKeysFromJsonElement(keyElement));
            }

            lookupKeys = lookupKeys
                .Select(NormalizeLookup)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            return !string.IsNullOrWhiteSpace(versionLabel) && lookupKeys.Count > 0;
        }

        private static List<string> ParseLookupKeysFromJsonElement(JsonElement element)
        {
            var values = new List<string>();

            switch (element.ValueKind)
            {
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        values.AddRange(ParseLookupKeysFromJsonElement(item));
                    }
                    break;

                case JsonValueKind.String:
                    var text = element.GetString() ?? string.Empty;
                    values.AddRange(
                        text.Split(new[] { '/', '\\', '+', ' ', ',', ';', '|', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Trim())
                    );
                    break;
            }

            return values;
        }

        private static string GetStringProperty(JsonElement item, string propertyName)
        {
            if (!TryGetPropertyIgnoreCase(item, propertyName, out var element))
            {
                return string.Empty;
            }

            return element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : string.Empty;
        }

        private static void TryAddProperty(JsonElement item, string propertyName, List<JsonElement> target)
        {
            if (TryGetPropertyIgnoreCase(item, propertyName, out var element))
            {
                target.Add(element);
            }
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement item, string propertyName, out JsonElement element)
        {
            foreach (var property in item.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    element = property.Value;
                    return true;
                }
            }

            element = default;
            return false;
        }

        private static bool LooksLikeJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.TrimStart();
            return trimmed.StartsWith("{", StringComparison.Ordinal) ||
                   trimmed.StartsWith("[", StringComparison.Ordinal);
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
            if (normalized.EndsWith(".hex", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[..^4];
            }
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

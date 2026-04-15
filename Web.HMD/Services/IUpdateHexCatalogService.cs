using Microsoft.AspNetCore.Http;

namespace Web.HMD.Services
{
    public interface IUpdateHexCatalogService
    {
        Task<UpdateHexUploadResult> UploadHexAsync(string versionLabel, IFormFile file);
        Task<UpdateHexMappingImportResult> ImportMappingsAsync(string tableText);
        Task<UpdateHexMatchResult?> FindMatchAsync(string chipsetValue, string decoderValue);
        Task<IReadOnlyList<UpdateHexMappingEntry>> GetMappingsAsync();
        Task<IReadOnlyList<UpdateHexFileEntry>> GetHexFilesAsync();
    }

    public class UpdateHexUploadResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class UpdateHexMappingImportResult
    {
        public int AddedOrUpdatedCount { get; set; }
        public int SkippedCount { get; set; }
    }

    public class UpdateHexMatchResult
    {
        public string LookupKey { get; set; } = string.Empty;
        public string VersionLabel { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
    }

    public class UpdateHexMappingEntry
    {
        public string LookupKey { get; set; } = string.Empty;
        public string VersionLabel { get; set; } = string.Empty;
    }

    public class UpdateHexFileEntry
    {
        public string VersionLabel { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; }
    }
}

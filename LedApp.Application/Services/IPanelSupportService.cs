using LedApp.Application.DTOs;

namespace LedApp.Application.Services
{
    public interface IPanelSupportService
    {
        Task<PanelSupportOperationResult> UploadAsync(PanelSupportUploadRequest request);
        Task<PanelSupportImportResult> ImportFromLibraryAsync(string libraryRoot);
        Task<IReadOnlyList<PanelSupportFileDto>> GetPanelFilesAsync(string pValue, string chipsetValue, string decoderValue);
        Task<PanelSupportDownloadDto?> GetFileByIdAsync(int id);
    }
}

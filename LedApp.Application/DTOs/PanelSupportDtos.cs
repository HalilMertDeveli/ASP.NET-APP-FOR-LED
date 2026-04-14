namespace LedApp.Application.DTOs
{
    public class PanelSupportUploadRequest
    {
        public string PValue { get; set; } = string.Empty;
        public string ChipsetValue { get; set; } = string.Empty;
        public string DecoderValue { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public byte[] FileContent { get; set; } = System.Array.Empty<byte>();
    }

    public class PanelSupportOperationResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class PanelSupportImportResult
    {
        public int ImportedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedCount { get; set; }
    }

    public class PanelSupportFileDto
    {
        public int Id { get; set; }
        public string FileType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    public class PanelSupportDownloadDto
    {
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public byte[] Content { get; set; } = System.Array.Empty<byte>();
    }
}

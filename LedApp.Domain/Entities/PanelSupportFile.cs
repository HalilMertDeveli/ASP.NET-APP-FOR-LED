using System;

namespace Entity.HMD.Entity
{
    public class PanelSupportFile
    {
        public int Id { get; set; }
        public string PanelType { get; set; } = string.Empty; // Optional: SMD / COB
        public string ChipsetValue { get; set; } = string.Empty;
        public string DecoderValue { get; set; } = string.Empty;
        public string PValue { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty; // Source path in local library
        public string FileType { get; set; } = string.Empty; // rcvp / hex
        public byte[] FileContent { get; set; } = Array.Empty<byte>();
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
    }
}

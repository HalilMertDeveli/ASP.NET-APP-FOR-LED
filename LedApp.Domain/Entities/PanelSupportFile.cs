using System;

namespace Entity.HMD.Entity
{
    public class PanelSupportFile
    {
        public int Id { get; set; }
        public string PanelType { get; set; } = string.Empty; // SMD / COB
        public string ChipsetValue { get; set; } = string.Empty;
        public string DecoderValue { get; set; } = string.Empty;
        public string PValue { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}

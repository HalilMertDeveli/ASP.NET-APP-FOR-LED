using System;

namespace Entity.HMD.Entity
{
    public class UpdateHexFile
    {
        public int Id { get; set; }
        public string VersionLabel { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

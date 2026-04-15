namespace Web.HMD.Models
{
    public class SupportFileResultViewModel
    {
        public int Id { get; set; }
        public string FileType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string FileRole { get; set; } = "scan";
        public string VersionLabel { get; set; } = string.Empty;
    }
}

namespace Web.HMD.Models
{
    public class PanelTypeViewModel
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PixelPitch { get; set; } = string.Empty;
        public string RecommendedUse { get; set; } = string.Empty;
        public string MinViewingDistance { get; set; } = string.Empty;
        public string RefreshRate { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string RouteAction { get; set; } = string.Empty;
    }
}

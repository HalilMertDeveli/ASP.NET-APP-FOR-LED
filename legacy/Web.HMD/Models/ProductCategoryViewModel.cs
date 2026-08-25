namespace Web.HMD.Models
{
    public class ProductCategoryViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public List<string> KeyFeatures { get; set; } = new();
    }
}

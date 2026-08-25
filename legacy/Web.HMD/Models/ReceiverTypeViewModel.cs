namespace Web.HMD.Models
{
    public class ReceiverTypeViewModel
    {
        public string Code { get; set; } = string.Empty;
        public string RouteAction { get; set; } = string.Empty;
        public string Interface { get; set; } = string.Empty;
        public int Ports { get; set; }
        public string CalibrationAccuracy { get; set; } = string.Empty;
        public string PwmCapacity { get; set; } = string.Empty;
        public string NormalCapacity { get; set; } = string.Empty;
        public string LsCapacity { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}

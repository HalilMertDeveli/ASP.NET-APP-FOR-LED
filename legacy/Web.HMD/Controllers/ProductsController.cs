using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Web.HMD.Models;

namespace Web.HMD.Controllers
{
    public class ProductsController : Controller
    {
        [HttpGet]
        public IActionResult Panels()
        {
            return RedirectToAction(nameof(PanelTypes));
        }

        [HttpGet]
        public IActionResult PanelTypes()
        {
            return View(GetPanelCatalog());
        }

        [HttpGet]
        public IActionResult PanelP10()
        {
            return PanelPage("P10");
        }

        [HttpGet]
        public IActionResult PanelP5()
        {
            return PanelPage("P5");
        }

        [HttpGet]
        public IActionResult PanelP25()
        {
            return PanelPage("P2.5");
        }

        [HttpGet]
        public IActionResult PanelP186()
        {
            return PanelPage("P1.86");
        }

        [HttpGet]
        public IActionResult PanelP153()
        {
            return PanelPage("P1.53");
        }

        [HttpGet]
        public IActionResult PanelP19()
        {
            return PanelPage("P1.9");
        }

        [HttpGet]
        public IActionResult PanelP125()
        {
            return PanelPage("P1.25");
        }

        [HttpGet]
        public IActionResult PanelP09()
        {
            return PanelPage("P0.9");
        }

        [HttpGet]
        public IActionResult PanelDetail(string code)
        {
            return PanelPage(code);
        }

        [HttpPost]
        public IActionResult AddPanelToCart(string code)
        {
            var panel = GetPanelCatalog().FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (panel == null)
            {
                return NotFound();
            }

            var catalog = GetPanelCatalog();
            var panelIndex = catalog.FindIndex(x => x.Code.Equals(panel.Code, StringComparison.OrdinalIgnoreCase));
            var panelId = 100 + panelIndex;

            var cart = GetCart();
            if (!cart.Any(x => x.Id == panelId))
            {
                cart.Add(new LedRepairViewModel
                {
                    Id = panelId,
                    Title = $"Panel {panel.Code}",
                    Description = panel.Description,
                    Price = panel.Price,
                    EstimatedTime = "Ayni gun sevk",
                    ImageUrl = "/images/services/e80.png",
                    Details = $"{panel.PixelPitch} - {panel.RefreshRate}"
                });
                SaveCart(cart);
            }

            TempData["Message"] = $"Panel {panel.Code} sepetinize eklendi.";
            return RedirectToAction("CartStatus", "Home");
        }

        [HttpGet]
        public IActionResult Receivers()
        {
            return View(GetReceiverCatalog());
        }

        [HttpGet]
        public IActionResult Receiver5A75B() => ReceiverPage("5A-75B");
        [HttpGet]
        public IActionResult Receiver5A75E() => ReceiverPage("5A-75E");
        [HttpGet]
        public IActionResult ReceiverE80() => ReceiverPage("E80");
        [HttpGet]
        public IActionResult ReceiverE120() => ReceiverPage("E120");
        [HttpGet]
        public IActionResult ReceiverE320() => ReceiverPage("E320");
        [HttpGet]
        public IActionResult ReceiverE320Pro() => ReceiverPage("E320PRO");
        [HttpGet]
        public IActionResult ReceiverK5Plus() => ReceiverPage("K5+");
        [HttpGet]
        public IActionResult ReceiverK8() => ReceiverPage("K8");
        [HttpGet]
        public IActionResult ReceiverK9Plus() => ReceiverPage("K9+");
        [HttpGet]
        public IActionResult ReceiverK10() => ReceiverPage("K10");
        [HttpGet]
        public IActionResult ReceiverI5Plus() => ReceiverPage("I5+");
        [HttpGet]
        public IActionResult ReceiverI8() => ReceiverPage("I8");
        [HttpGet]
        public IActionResult ReceiverI9Plus() => ReceiverPage("I9+");
        [HttpGet]
        public IActionResult ReceiverI10() => ReceiverPage("I10");

        [HttpGet]
        public IActionResult ReceiverDetail(string code)
        {
            return ReceiverPage(code);
        }

        [HttpPost]
        public IActionResult AddReceiverToCart(string code)
        {
            var catalog = GetReceiverCatalog();
            var receiver = catalog.FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (receiver == null)
            {
                return NotFound();
            }

            var receiverIndex = catalog.FindIndex(x => x.Code.Equals(receiver.Code, StringComparison.OrdinalIgnoreCase));
            var receiverId = 200 + receiverIndex;

            var cart = GetCart();
            if (!cart.Any(x => x.Id == receiverId))
            {
                cart.Add(new LedRepairViewModel
                {
                    Id = receiverId,
                    Title = $"Receiver {receiver.Code}",
                    Description = $"{receiver.Interface} - {receiver.PwmCapacity}",
                    Price = receiver.Price,
                    EstimatedTime = "Ayni gun sevk",
                    ImageUrl = "/images/services/e80.png",
                    Details = $"Ports: {receiver.Ports}, Calibration: {receiver.CalibrationAccuracy}"
                });
                SaveCart(cart);
            }

            TempData["Message"] = $"Receiver {receiver.Code} sepetinize eklendi.";
            return RedirectToAction("CartStatus", "Home");
        }

        [HttpGet]
        public IActionResult ASeries()
        {
            var specs = new List<ASeriesSpecViewModel>
            {
                new() { Model = "A20", Port = "HUB75*8", MaxResolution = "1920*1080@30HZ" },
                new() { Model = "A20B", Port = "HUB75*8", MaxResolution = "1920*1080@30HZ" },
                new() { Model = "A35", Port = "LAN*1", MaxResolution = "3840*2160@30HZ" },
                new() { Model = "A40", Port = "LAN*1", MaxResolution = "3840*2160@30HZ" },
                new() { Model = "A60", Port = "LAN*2", MaxResolution = "3840*2160@30HZ" },
                new() { Model = "A100", Port = "LAN*2", MaxResolution = "3840*2160@30HZ" },
                new() { Model = "A200", Port = "LAN*4", MaxResolution = "3840*2160@30HZ" },
                new() { Model = "A500", Port = "LAN*8", MaxResolution = "7680*4320@60HZ" },
                new() { Model = "AX06", Port = "LAN*6", MaxResolution = "7680*4320@60HZ" },
                new() { Model = "AX08", Port = "LAN*8", MaxResolution = "7680*4320@60HZ" }
            };

            return View(specs);
        }

        [HttpGet]
        public IActionResult PowerSupplies()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Motherboards()
        {
            return View(GetMotherboardCatalog());
        }

        [HttpGet]
        public IActionResult MotherboardDetail(string model)
        {
            var board = GetMotherboardCatalog().FirstOrDefault(x => x.Model.Equals(model, StringComparison.OrdinalIgnoreCase));
            if (board == null)
            {
                return NotFound();
            }

            return View(board);
        }

        private IActionResult PanelPage(string code)
        {
            var panel = GetPanelCatalog().FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (panel == null)
            {
                return NotFound();
            }

            return View("PanelDetail", panel);
        }

        private static List<PanelTypeViewModel> GetPanelCatalog()
        {
            return new List<PanelTypeViewModel>
            {
                new() { Code = "P10", Description = "Dis mekan ve uzak izleme icin ekonomik panel.", PixelPitch = "10.0 mm", RecommendedUse = "Dis mekan reklam ekranlari", MinViewingDistance = "10 m", RefreshRate = "1920 Hz", Price = 18, RouteAction = nameof(PanelP10) },
                new() { Code = "P5", Description = "Dis/yarim dis mekan kullanim icin net goruntu.", PixelPitch = "5.0 mm", RecommendedUse = "Vitrin ve cephe ekranlari", MinViewingDistance = "5 m", RefreshRate = "1920 Hz", Price = 19, RouteAction = nameof(PanelP5) },
                new() { Code = "P2.5", Description = "Ic mekan icin dengeli fiyat/performans paneli.", PixelPitch = "2.5 mm", RecommendedUse = "Toplanti salonu ve magaza ici", MinViewingDistance = "2.5 m", RefreshRate = "3840 Hz", Price = 20, RouteAction = nameof(PanelP25) },
                new() { Code = "P1.86", Description = "Yuksek detay gerektiren ic mekan kurulumlari.", PixelPitch = "1.86 mm", RecommendedUse = "Kurumsal sunum duvarlari", MinViewingDistance = "1.8 m", RefreshRate = "3840 Hz", Price = 21, RouteAction = nameof(PanelP186) },
                new() { Code = "P1.53", Description = "Canli renk ve keskinlik sunan premium ic mekan panel.", PixelPitch = "1.53 mm", RecommendedUse = "TV studyo ve showroom", MinViewingDistance = "1.5 m", RefreshRate = "3840 Hz", Price = 22, RouteAction = nameof(PanelP153) },
                new() { Code = "P1.9", Description = "Genel amacli ic mekan uygulamalari icin ideal.", PixelPitch = "1.9 mm", RecommendedUse = "Etkinlik ve sahne arkasi ekran", MinViewingDistance = "1.9 m", RefreshRate = "3840 Hz", Price = 23, RouteAction = nameof(PanelP19) },
                new() { Code = "P1.25", Description = "Yakindan izleme icin yuksek piksel yogunlugu.", PixelPitch = "1.25 mm", RecommendedUse = "Kontrol odasi ve premium duvar", MinViewingDistance = "1.2 m", RefreshRate = "3840 Hz", Price = 24, RouteAction = nameof(PanelP125) },
                new() { Code = "P0.9", Description = "Ultra ince piksel araligi ile en ust seviye detay.", PixelPitch = "0.9 mm", RecommendedUse = "Broadcast ve ileri seviye komuta merkezi", MinViewingDistance = "0.9 m", RefreshRate = "3840 Hz", Price = 25, RouteAction = nameof(PanelP09) }
            };
        }

        private static List<ReceiverTypeViewModel> GetReceiverCatalog()
        {
            return new List<ReceiverTypeViewModel>
            {
                new() { Code = "5A-75B", RouteAction = nameof(Receiver5A75B), Interface = "HUB75", Ports = 8, CalibrationAccuracy = "8 bit", PwmCapacity = "512x384", NormalCapacity = "512x256", LsCapacity = "512x324", Remark = "W/H <= 1024", Price = 30 },
                new() { Code = "5A-75E", RouteAction = nameof(Receiver5A75E), Interface = "HUB75", Ports = 16, CalibrationAccuracy = "8 bit", PwmCapacity = "512x512", NormalCapacity = "512x256", LsCapacity = "512x324", Remark = "W/H <= 1024", Price = 31 },
                new() { Code = "E80", RouteAction = nameof(ReceiverE80), Interface = "HUB75", Ports = 8, CalibrationAccuracy = "8 bit", PwmCapacity = "512x256", NormalCapacity = "512x128", LsCapacity = "512x162", Remark = "W/H <= 1024", Price = 32 },
                new() { Code = "E120", RouteAction = nameof(ReceiverE120), Interface = "HUB75", Ports = 12, CalibrationAccuracy = "8 bit", PwmCapacity = "512x384", NormalCapacity = "512x256", LsCapacity = "512x324", Remark = "W/H <= 1024", Price = 33 },
                new() { Code = "E320", RouteAction = nameof(ReceiverE320), Interface = "HUB320", Ports = 8, CalibrationAccuracy = "8 bit", PwmCapacity = "512x512", NormalCapacity = "512x256", LsCapacity = "512x324", Remark = "W/H <= 1024", Price = 34 },
                new() { Code = "E320PRO", RouteAction = nameof(ReceiverE320Pro), Interface = "HUB320", Ports = 8, CalibrationAccuracy = "14 bit", PwmCapacity = "512x512", NormalCapacity = "-", LsCapacity = "-", Remark = "W/H <= 1024", Price = 35 }
            };
        }

        private static List<MotherboardTypeViewModel> GetMotherboardCatalog()
        {
            return new List<MotherboardTypeViewModel>
            {
                new() { Model = "X2S", Port = "2*1G", Pixel = "1,31 million", Resolution = "1920x1200@60HZ" },
                new() { Model = "X2M", Port = "2*1G", Pixel = "1,31 million", Resolution = "1920x1080@60HZ" },
                new() { Model = "X4S", Port = "4*1G", Pixel = "2,62 million", Resolution = "1920x1200@60HZ" },
                new() { Model = "X4E", Port = "4*1G", Pixel = "2,62 million", Resolution = "1920x1200@60HZ" },
                new() { Model = "X4M", Port = "4*1G", Pixel = "2,62 million", Resolution = "1920x1080@60HZ" },
                new() { Model = "X6", Port = "6*1G", Pixel = "3,93 million", Resolution = "1920x1200@60HZ" },
                new() { Model = "X7", Port = "8*1G", Pixel = "5,24 million", Resolution = "1920x1200@60HZ" },
                new() { Model = "X8E", Port = "8*1G", Pixel = "5,24 million", Resolution = "4096x2160@60HZ" },
                new() { Model = "X8M", Port = "8*1G", Pixel = "5,24 million", Resolution = "4096x2160@60HZ" },
                new() { Model = "X12", Port = "12*1G", Pixel = "7,86 million", Resolution = "1920x1200@60HZ" },
                new() { Model = "X12M", Port = "12*1G", Pixel = "7,86 million", Resolution = "4096x2160@60HZ" },
                new() { Model = "X16E", Port = "16*1G", Pixel = "10,49 million", Resolution = "4096x2160@60HZ" },
                new() { Model = "X20", Port = "20*1G", Pixel = "13,11 million", Resolution = "4096x2160@60HZ" },
                new() { Model = "X20M", Port = "20*1G", Pixel = "13,11 million", Resolution = "4096x2160@60HZ" },
                new() { Model = "X26M", Port = "26*1G", Pixel = "17,04 million", Resolution = "4096x2160@60HZ" },
                new() { Model = "X40M", Port = "40*1G", Pixel = "26,21 million", Resolution = "4096x2160@60HZ" }
            };
        }

        private IActionResult ReceiverPage(string code)
        {
            var receiver = GetReceiverCatalog().FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (receiver == null)
            {
                return NotFound();
            }

            return View("ReceiverDetail", receiver);
        }

        private List<LedRepairViewModel> GetCart()
        {
            var cartJson = HttpContext.Session.GetString("CartItems");
            if (string.IsNullOrWhiteSpace(cartJson))
            {
                return new List<LedRepairViewModel>();
            }

            return JsonSerializer.Deserialize<List<LedRepairViewModel>>(cartJson) ?? new List<LedRepairViewModel>();
        }

        private void SaveCart(List<LedRepairViewModel> cart)
        {
            HttpContext.Session.SetString("CartItems", JsonSerializer.Serialize(cart));
        }
    }
}

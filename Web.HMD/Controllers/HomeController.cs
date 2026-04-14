using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Web.HMD.Models;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Web.HMD.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View(GetFeaturedServices());
        }

        [HttpGet]
        public IActionResult ServiceDetail(int id)
        {
            var service = GetFeaturedServices().FirstOrDefault(x => x.Id == id);
            if (service == null)
            {
                return NotFound();
            }

            return View(service);
        }

        [HttpPost]
        public IActionResult AddToCart(int id)
        {
            var service = GetFeaturedServices().FirstOrDefault(x => x.Id == id);
            if (service == null)
            {
                return NotFound();
            }

            var cart = GetCart();
            if (!cart.Any(x => x.Id == id))
            {
                cart.Add(service);
                SaveCart(cart);
            }

            TempData["Message"] = $"{service.Title} sepetinize eklendi.";
            return RedirectToAction(nameof(CartStatus));
        }

        [HttpPost]
        public IActionResult RemoveFromCart(int id)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                cart.Remove(item);
                SaveCart(cart);
            }

            return RedirectToAction(nameof(CartStatus));
        }

        [HttpGet]
        public IActionResult CartStatus()
        {
            return View(GetCart());
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Sepet()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private List<LedRepairViewModel> GetFeaturedServices()
        {
            return new List<LedRepairViewModel>
            {
                new LedRepairViewModel
                {
                    Id = 1,
                    Title = "LED Panel Onarımı",
                    Description = "Ölü piksellerin ve arızalı LED modüllerinin profesyonel tamiri.",
                    Price = 1500,
                    ImageUrl = "https://images.unsplash.com/photo-1517420704952-d9f3974122b5?w=500&q=80",
                    Details = "Panel içindeki arızalı modüller yenilenir, renk kalibrasyonu yapılır ve tüm satır/sütun testleri tamamlanır.",
                    EstimatedTime = "1-2 iş günü"
                },
                new LedRepairViewModel
                {
                    Id = 2,
                    Title = "Kontrol Kartı Değişimi",
                    Description = "Bozuk gönderici/alıcı (sender/receiver) kartların tespiti ve değişimi.",
                    Price = 2000,
                    ImageUrl = "https://images.unsplash.com/photo-1518770660439-4636190af475?w=500&q=80",
                    Details = "Mevcut kartlar test edilir, arızalı olanlar yenilenir ve veri iletim stabilitesi kontrol edilerek devreye alınır.",
                    EstimatedTime = "Aynı gün teslim"
                },
                new LedRepairViewModel
                {
                    Id = 3,
                    Title = "Güç Kaynağı (Power Supply) Tamiri",
                    Description = "Voltaj dalgalanmalarından zarar görmüş trafoların bakımı.",
                    Price = 800,
                    ImageUrl = "https://images.unsplash.com/photo-1581092334651-ddf26d9a09d0?w=500&q=80",
                    Details = "Arızalı güç birimleri ölçüm cihazlarıyla analiz edilir, gerekli parça değişimleri sonrası yük testi uygulanır.",
                    EstimatedTime = "4-8 saat"
                },
                new LedRepairViewModel
                {
                    Id = 4,
                    Title = "Dış Mekan Kabin Bakımı",
                    Description = "Su geçirmezlik (IP65) izolasyon yenileme ve fiziksel temizlik.",
                    Price = 3000,
                    ImageUrl = "https://images.unsplash.com/photo-1551288049-bebda4e38f71?w=500&q=80",
                    Details = "Dış yüzey ve bağlantı noktaları temizlenir, conta yenileme yapılır, kabin su ve toz dayanım testinden geçirilir.",
                    EstimatedTime = "2-3 iş günü"
                }
            };
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

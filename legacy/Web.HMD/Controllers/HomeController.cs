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
            return View(GetFeaturedServices());
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
                    Price = 10,
                    ImageUrl = "https://images.unsplash.com/photo-1488590528505-98d2b5aba04b?auto=format&fit=crop&w=900&q=80",
                    Details = "Panel içindeki arızalı modüller yenilenir, renk kalibrasyonu yapılır ve tüm satır/sütun testleri tamamlanır.",
                    EstimatedTime = "1-2 iş günü"
                },
                new LedRepairViewModel
                {
                    Id = 2,
                    Title = "Kontrol Kartı Değişimi",
                    Description = "Bozuk gönderici/alıcı (sender/receiver) kartların tespiti ve değişimi.",
                    Price = 11,
                    ImageUrl = "https://images.unsplash.com/photo-1518770660439-4636190af475?w=500&q=80",
                    Details = "Mevcut kartlar test edilir, arızalı olanlar yenilenir ve veri iletim stabilitesi kontrol edilerek devreye alınır.",
                    EstimatedTime = "Aynı gün teslim"
                },
                new LedRepairViewModel
                {
                    Id = 3,
                    Title = "Güç Kaynağı (Power Supply) Tamiri",
                    Description = "Voltaj dalgalanmalarından zarar görmüş trafoların bakımı.",
                    Price = 12,
                    ImageUrl = "https://images.unsplash.com/photo-1581092334651-ddf26d9a09d0?w=500&q=80",
                    Details = "Arızalı güç birimleri ölçüm cihazlarıyla analiz edilir, gerekli parça değişimleri sonrası yük testi uygulanır.",
                    EstimatedTime = "4-8 saat"
                },
                new LedRepairViewModel
                {
                    Id = 4,
                    Title = "Dış Mekan Kabin Bakımı",
                    Description = "Su geçirmezlik (IP65) izolasyon yenileme ve fiziksel temizlik.",
                    Price = 13,
                    ImageUrl = "https://images.unsplash.com/photo-1551288049-bebda4e38f71?w=500&q=80",
                    Details = "Dış yüzey ve bağlantı noktaları temizlenir, conta yenileme yapılır, kabin su ve toz dayanım testinden geçirilir.",
                    EstimatedTime = "2-3 iş günü"
                },
                new LedRepairViewModel
                {
                    Id = 6,
                    Title = "Modul Degisimi ve Lehim Onarimi",
                    Description = "Hasarli modul, soket ve lehim noktalarini hassas onarimla yeniler.",
                    Price = 15,
                    ImageUrl = "https://images.unsplash.com/photo-1581092580497-e0d23cbdf1dc?w=500&q=80",
                    Details = "Kart seviyesi analiz ile arizali bolge belirlenir, parca degisimi yapilarak test edilir.",
                    EstimatedTime = "1 iş günü"
                },
                new LedRepairViewModel
                {
                    Id = 7,
                    Title = "Kurulum ve Saha Devreye Alma",
                    Description = "Yeni LED ekranlarin montaji, kablolama ve yayin entegrasyonunu tamamlar.",
                    Price = 16,
                    ImageUrl = "https://images.unsplash.com/photo-1461749280684-dccba630e2f6?w=500&q=80",
                    Details = "Saha kesifi, guc ve data planlamasi ile sorunsuz devreye alma hizmeti sunulur.",
                    EstimatedTime = "1-2 iş günü"
                },
                new LedRepairViewModel
                {
                    Id = 8,
                    Title = "Acil Servis ve Yerinde Mudahale",
                    Description = "Kritik arizalara oncelikli ekip yonlendirmesi ile hizli mudahale eder.",
                    Price = 17,
                    ImageUrl = "https://images.unsplash.com/photo-1556155092-490a1ba16284?w=500&q=80",
                    Details = "Acil durumlarda yerinde kontrol, parca degisimi ve gecici/kalici cozum saglanir.",
                    EstimatedTime = "2-6 saat"
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

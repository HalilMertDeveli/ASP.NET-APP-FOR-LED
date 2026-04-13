using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Web.HMD.Models;

namespace Web.HMD.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            var services = new List<LedRepairViewModel>
            {
                new LedRepairViewModel { Id = 1, Title = "LED Panel Onarımı", Description = "Ölü piksellerin ve arızalı LED modüllerinin profesyonel tamiri.", Price = 1500, ImageUrl = "https://images.unsplash.com/photo-1517420704952-d9f3974122b5?w=500&q=80" },
                new LedRepairViewModel { Id = 2, Title = "Kontrol Kartı Değişimi", Description = "Bozuk gönderici/alıcı (sender/receiver) kartların tespiti ve değişimi.", Price = 2000, ImageUrl = "https://images.unsplash.com/photo-1518770660439-4636190af475?w=500&q=80" },
                new LedRepairViewModel { Id = 3, Title = "Güç Kaynağı (Power Supply) Tamiri", Description = "Voltaj dalgalanmalarından zarar görmüş trafoların bakımı.", Price = 800, ImageUrl = "https://images.unsplash.com/photo-1581092334651-ddf26d9a09d0?w=500&q=80" },
                new LedRepairViewModel { Id = 4, Title = "Dış Mekan Kabin Bakımı", Description = "Su geçirmezlik (IP65) izolasyon yenileme ve fiziksel temizlik.", Price = 3000, ImageUrl = "https://images.unsplash.com/photo-1551288049-bebda4e38f71?w=500&q=80" }
            };

            return View(services);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

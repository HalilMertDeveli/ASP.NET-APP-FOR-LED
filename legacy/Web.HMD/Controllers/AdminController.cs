using Microsoft.AspNetCore.Mvc;

namespace Web.HMD.Controllers
{
    public class AdminController : Controller
    {
        private const string AdminSessionKey = "IsAdminLoggedIn";
        private const string AdminUsername = "admin";
        private const string AdminPassword = "admin";

        [HttpGet]
        public IActionResult Login()
        {
            if (IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Dashboard));
            }

            return View();
        }

        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            if (string.Equals(username, AdminUsername, StringComparison.OrdinalIgnoreCase) &&
                password == AdminPassword)
            {
                HttpContext.Session.SetString(AdminSessionKey, "true");
                return RedirectToAction(nameof(Dashboard));
            }

            TempData["AdminLoginError"] = "Kullanici adi veya sifre hatali.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult Dashboard()
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction(nameof(Login));
            }

            return View();
        }

        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove(AdminSessionKey);
            return RedirectToAction(nameof(Login));
        }

        private bool IsAdminLoggedIn()
        {
            return string.Equals(HttpContext.Session.GetString(AdminSessionKey), "true", StringComparison.Ordinal);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Web.HMD.Models;
using Entity.HMD.Context;
using Entity.HMD.Entity;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Web.HMD.Controllers
{
    public class AccountController : Controller
    {
        private readonly LedContext _context;

        public AccountController(LedContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = _context.Users.FirstOrDefault(x => x.Email == model.Email && x.PasswordHash == model.Password);
                
                if (user != null)
                {
                    // Kimlik (Claim) oluşturma işlemi
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.FullName),
                        new Claim(ClaimTypes.Email, user.Email)
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = model.RememberMe // Beni Hatırla işaretlendiyse kalıcı çerez
                    };

                    // Tarayıcıya ASP.NET Cookie'sini basıyor ve oturumu resmen açıyor
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

                    TempData["Message"] = $"Hoş Geldiniz, {user.FullName}!";
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ModelState.AddModelError("", "E-posta adresi ya da şifre hatalı.");
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var isEmailExist = _context.Users.Any(x => x.Email == model.Email);
                if (isEmailExist)
                {
                    ModelState.AddModelError("Email", "Bu e-posta adresi zaten kullanımda.");
                    return View(model);
                }
                
                var newUser = new User
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Phone = model.Phone,
                    PasswordHash = model.Password 
                };

                _context.Users.Add(newUser);
                _context.SaveChanges();

                // KAYıT BAŞARILI, O HALDE KULLANICIYI DOĞRUDAN SİSTEME DAHİL ET (OTOMATİK GİRİŞ)
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, newUser.Id.ToString()),
                    new Claim(ClaimTypes.Name, newUser.FullName),
                    new Claim(ClaimTypes.Email, newUser.Email)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                TempData["Message"] = "Kayıt Başarıyla Gerçekleşti ve Giriş Yapıldı!";
                return RedirectToAction("Index", "Home");
            }
            return View(model);
        }

        // Çıkış Yapma (Logout) İşlemi
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}

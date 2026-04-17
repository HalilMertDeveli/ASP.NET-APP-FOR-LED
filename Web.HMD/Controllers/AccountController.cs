using Microsoft.AspNetCore.Mvc;
using Web.HMD.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using System.Collections.Generic;
using System.Threading.Tasks;
using LedApp.Application.Services;
using LedApp.Application.DTOs;
using System;
using System.Linq;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Web.HMD.Controllers
{
    public class AccountController : Controller
    {
        // ARTIK VERİTABANINI (LedContext) BİLMİYOR! Sadece AuthService'ten hizmet alıyor.
        private readonly IAuthService _authService;

        public AccountController(IAuthService authService)
        {
            _authService = authService;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null) return NotFound();

            var model = new SettingsViewModel
            {
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Settings(SettingsViewModel model)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            if (ModelState.IsValid)
            {
                // Profil Güncelleme
                var updateDto = new UpdateUserDto
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Phone = model.Phone
                };

                var updateResult = await _authService.UpdateUserAsync(userId, updateDto);
                if (!updateResult)
                {
                    ModelState.AddModelError("", "Profil güncellenirken bir hata oluştu.");
                    return View(model);
                }

                // Şifre Değiştirme (Eğer alanlar doluysa)
                if (!string.IsNullOrEmpty(model.CurrentPassword) && !string.IsNullOrEmpty(model.NewPassword))
                {
                    var passwordResult = await _authService.ChangePasswordAsync(userId, model.CurrentPassword, model.NewPassword);
                    if (!passwordResult)
                    {
                        ModelState.AddModelError("CurrentPassword", "Mevcut şifre hatalı.");
                        return View(model);
                    }
                    TempData["Message"] = "Profil ve şifre başarıyla güncellendi.";
                }
                else
                {
                    TempData["Message"] = "Profil bilgileri başarıyla güncellendi.";
                }

                return RedirectToAction(nameof(Settings));
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExternalLogin(string provider, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(provider) || !string.Equals(provider, GoogleDefaults.AuthenticationScheme, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "Sadece Google ile giriş destekleniyor.");
                return View("Login");
            }

            var schemeProvider = HttpContext.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
            var providers = await schemeProvider.GetAllSchemesAsync();
            var selectedProvider = providers.FirstOrDefault(p => string.Equals(p.Name, GoogleDefaults.AuthenticationScheme, StringComparison.OrdinalIgnoreCase));
            if (selectedProvider == null)
            {
                ModelState.AddModelError(string.Empty, "Google giriş sağlayıcısı aktif değil.");
                return View("Login");
            }

            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { provider, returnUrl }) ?? "/Account/Login";
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, selectedProvider.Name);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string provider, string? returnUrl = null, string? remoteError = null)
        {
            returnUrl ??= Url.Action("Index", "Home") ?? "/";
            var providerLabel = "Google";

            if (!string.IsNullOrWhiteSpace(remoteError))
            {
                ModelState.AddModelError(string.Empty, $"{providerLabel} ile giriş başarısız: {remoteError}");
                return View("Login");
            }

            var externalResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (externalResult?.Principal == null)
            {
                ModelState.AddModelError(string.Empty, $"{providerLabel} kimlik doğrulaması tamamlanamadı.");
                return View("Login");
            }

            var email = externalResult.Principal.FindFirstValue(ClaimTypes.Email);
            var providerUserId = externalResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(providerUserId))
            {
                providerUserId = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(string.Empty, "Google hesabında e-posta bilgisi bulunamadı.");
                return View("Login");
            }

            var fullName = externalResult.Principal.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = email.Split('@').FirstOrDefault() ?? $"{providerLabel} Kullanıcısı";
            }

            var userDto = await _authService.GetByEmailAsync(email);
            if (userDto == null)
            {
                var generatedPassword = $"{providerLabel}_{providerUserId}_{Guid.NewGuid():N}";
                userDto = await _authService.RegisterAsync(new CreateUserDto
                {
                    FullName = fullName,
                    Email = email,
                    Phone = "Google",
                    Password = generatedPassword
                });
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userDto.Id.ToString()),
                new Claim(ClaimTypes.Name, userDto.FullName),
                new Claim(ClaimTypes.Email, userDto.Email)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties { IsPersistent = true });

            TempData["Message"] = $"{providerLabel} ile giriş başarılı, hoş geldiniz {userDto.FullName}!";
            return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Artık SQL sorgusu yazmıyoruz, şifre kıyaslamıyoruz. Hepsini Service yapıyor.
                var userDto = await _authService.LoginAsync(model.Email, model.Password);
                
                if (userDto != null) // Başarılıysa
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, userDto.Id.ToString()),
                        new Claim(ClaimTypes.Name, userDto.FullName),
                        new Claim(ClaimTypes.Email, userDto.Email)
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = model.RememberMe
                    };

                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

                    TempData["Message"] = $"Hoş Geldiniz, {userDto.FullName}!";
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
                var isEmailExist = await _authService.CheckEmailExistsAsync(model.Email);
                if (isEmailExist)
                {
                    ModelState.AddModelError("Email", "Bu e-posta adresi zaten kullanımda.");
                    return View(model);
                }
                
                // Form'dan gelen ViewModel'ı, Application'ın anladığı DTO'ya çeviriyoruz:
                var createDto = new CreateUserDto
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Phone = model.Phone,
                    Password = model.Password 
                };

                // İçeride BCrypt ile şifrelenip veritabanına eklenecek
                var newUserDto = await _authService.RegisterAsync(createDto);

                // Otomatik Giriş
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, newUserDto.Id.ToString()),
                    new Claim(ClaimTypes.Name, newUserDto.FullName),
                    new Claim(ClaimTypes.Email, newUserDto.Email)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                TempData["Message"] = "Kayıt Başarıyla Gerçekleşti ve Giriş Yapıldı!";
                return RedirectToAction("Index", "Home");
            }
            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}

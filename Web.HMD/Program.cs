using Entity.HMD.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using LedApp.Domain.Interfaces;
using LedApp.Infrastructure.Repositories;
using LedApp.Application.Services;

namespace Web.HMD
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            builder.Services.AddDbContext<LedContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("LedAppDb")));

            // ONION MİMARİSİ: DEPENDENCY INJECTION (Bağımlılık Enjeksiyonu)
            // Sistem "IGenericRepository" isteri gördüğünde "GenericRepository" sınıfını verecek.
            builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IAuthService, AuthService>();

            var authenticationBuilder = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.LogoutPath = "/Account/Logout";
                    options.Cookie.Name = "LedAppCookie";
                });

            var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
            var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
            if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
            {
                authenticationBuilder.AddGoogle(options =>
                {
                    options.ClientId = googleClientId;
                    options.ClientSecret = googleClientSecret;
                });
            }

            var facebookAppId = builder.Configuration["Authentication:Facebook:AppId"];
            var facebookAppSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
            if (!string.IsNullOrWhiteSpace(facebookAppId) && !string.IsNullOrWhiteSpace(facebookAppSecret))
            {
                authenticationBuilder.AddFacebook(options =>
                {
                    options.AppId = facebookAppId;
                    options.AppSecret = facebookAppSecret;
                    options.Fields.Add("name");
                    options.Fields.Add("email");
                });
            }
            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}

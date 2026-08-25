using LedApp.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LedApp.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IPanelSupportService, PanelSupportService>();
            return services;
        }
    }
}

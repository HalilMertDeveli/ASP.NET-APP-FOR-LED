using LedApp.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace LedApp.Infrastructure.Configuration
{
    public class PanelLibraryPathProvider : IPanelLibraryPathProvider
    {
        private readonly IConfiguration _configuration;

        public PanelLibraryPathProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string? GetDefaultLibraryRootPath()
        {
            return _configuration["PanelLibrary:RootPath"];
        }
    }
}

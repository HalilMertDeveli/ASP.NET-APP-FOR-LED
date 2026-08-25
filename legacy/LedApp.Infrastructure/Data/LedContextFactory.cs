using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Entity.HMD.Context
{
    public class LedContextFactory : IDesignTimeDbContextFactory<LedContext>
    {
        public LedContext CreateDbContext(string[] args)
        {
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Web.HMD");
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("LedAppDb")
                ?? throw new InvalidOperationException("Connection string 'LedAppDb' was not found.");

            var optionsBuilder = new DbContextOptionsBuilder<LedContext>();
            optionsBuilder.UseSqlServer(connectionString);
            return new LedContext(optionsBuilder.Options);
        }
    }
}

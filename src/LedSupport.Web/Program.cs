using LedSupport.Web.Options;
using LedSupport.Web.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

// Map common Supabase env names onto ASP.NET configuration keys.
MapEnvAlias("SUPABASE_URL", "Supabase__Url");
MapEnvAlias("SUPABASE_SERVICE_ROLE_KEY", "Supabase__ServiceRoleKey");
MapEnvAlias("SUPABASE_KEY", "Supabase__ServiceRoleKey");
MapEnvAlias("RESEND_API_KEY", "Resend__ApiKey");

// Vercel/dashboard sometimes sets empty env placeholders (""). That breaks bool/int binding.
foreach (var key in new[]
{
    "Support__RequireStore",
    "Support__RequireFirestore",
    "Support__RateLimitPerWindow",
    "Support__RateLimitWindowMinutes",
    "Smtp__Port",
    "Smtp__EnableSsl"
})
{
    var value = Environment.GetEnvironmentVariable(key);
    if (value is not null && string.IsNullOrWhiteSpace(value))
    {
        Environment.SetEnvironmentVariable(key, null);
    }
}

var builder = WebApplication.CreateBuilder(args);

// Vercel / PaaS: listen on $PORT; local defaults stay from launchSettings / --urls
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.Configure<SiteSettings>(builder.Configuration.GetSection(SiteSettings.SectionName));
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection(SmtpSettings.SectionName));
builder.Services.Configure<SupportSettings>(builder.Configuration.GetSection(SupportSettings.SectionName));
builder.Services.Configure<ResendSettings>(builder.Configuration.GetSection(ResendSettings.SectionName));
builder.Services.Configure<SupabaseSettings>(builder.Configuration.GetSection(SupabaseSettings.SectionName));

builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IGitHubStatsService, GitHubStatsService>();
builder.Services.AddHttpClient<IResendEmailService, ResendEmailService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<SupabaseSupportRequestStore>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<ISupportRequestStore>(sp => sp.GetRequiredService<SupabaseSupportRequestStore>());
builder.Services.AddScoped<DirectSupportRequestService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<ISupportRequestService>(sp => sp.GetRequiredService<DirectSupportRequestService>());

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

LogSupportConfiguration(app);

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // Reverse proxy (Vercel) terminates TLS — do not force HTTPS redirect here.
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();

static void MapEnvAlias(string from, string to)
{
    var value = Environment.GetEnvironmentVariable(from);
    if (string.IsNullOrWhiteSpace(value))
    {
        return;
    }

    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(to)))
    {
        Environment.SetEnvironmentVariable(to, value);
    }
}

static void LogSupportConfiguration(WebApplication app)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SupportStartup");
    var support = app.Services.GetRequiredService<IOptions<SupportSettings>>().Value;
    var resend = app.Services.GetRequiredService<IOptions<ResendSettings>>().Value;
    var supabase = app.Services.GetRequiredService<IOptions<SupabaseSettings>>().Value;

    var resendOk = !string.IsNullOrWhiteSpace(resend.ApiKey) &&
                   !resend.ApiKey.Contains("YOUR_", StringComparison.Ordinal);
    var supabaseOk = !string.IsNullOrWhiteSpace(supabase.Url) &&
                     !supabase.Url.Contains("YOUR_", StringComparison.Ordinal) &&
                     !string.IsNullOrWhiteSpace(supabase.ServiceRoleKey) &&
                     !supabase.ServiceRoleKey.Contains("YOUR_", StringComparison.Ordinal);

    logger.LogInformation(
        "Support mode={Mode}, ResendConfigured={Resend}, SupabaseConfigured={Supabase}, RequireStore={Req}, ToEmail={To}",
        support.Mode,
        resendOk,
        supabaseOk,
        support.RequireStore,
        resend.ToEmail);

    if (!resendOk)
    {
        logger.LogError(
            "Contact form will fail until Resend:ApiKey is set via user-secrets / env. " +
            "Example: dotnet user-secrets set \"Resend:ApiKey\" \"re_xxx\"");
    }

    if (!supabaseOk)
    {
        logger.LogError(
            "Contact form persistence will fail until Supabase:Url and Supabase:ServiceRoleKey are set.");
    }
}

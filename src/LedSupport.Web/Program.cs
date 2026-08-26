using LedSupport.Web.Api;
using LedSupport.Web.Options;
using LedSupport.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

// Map common Supabase env names onto ASP.NET configuration keys.
MapEnvAlias("SUPABASE_URL", "Supabase__Url");
MapEnvAlias("NEXT_PUBLIC_SUPABASE_URL", "Supabase__Url");
MapEnvAlias("SUPABASE_SERVICE_ROLE_KEY", "Supabase__ServiceRoleKey");
MapEnvAlias("SUPABASE_SECRET_KEY", "Supabase__ServiceRoleKey");
MapEnvAlias("SUPABASE_KEY", "Supabase__ServiceRoleKey");
MapEnvAlias("SUPABASE_ANON_KEY", "Supabase__PublishableKey");
MapEnvAlias("SUPABASE_PUBLISHABLE_KEY", "Supabase__PublishableKey");
MapEnvAlias("NEXT_PUBLIC_SUPABASE_ANON_KEY", "Supabase__PublishableKey");
MapEnvAlias("NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY", "Supabase__PublishableKey");
MapEnvAlias("RESEND_API_KEY", "Resend__ApiKey");
MapEnvAlias("RESEND_FROM_EMAIL", "Resend__FromEmail");
MapEnvAlias("RESEND_TO_EMAIL", "Resend__ToEmail");

// Vercel/dashboard sometimes sets empty env placeholders (""). That breaks bool/int binding.
foreach (var key in new[]
{
    "Support__RequireStore",
    "Support__RateLimitPerWindow",
    "Support__RateLimitWindowMinutes",
    "Smtp__Port",
    "Smtp__EnableSsl",
    "Supabase__Url",
    "Supabase__PublishableKey",
    "Supabase__ServiceRoleKey",
    "Supabase__AnonKey",
    "Resend__ApiKey",
    "Resend__FromEmail",
    "Resend__ToEmail",
    "SUPABASE_URL",
    "SUPABASE_SERVICE_ROLE_KEY",
    "SUPABASE_SECRET_KEY",
    "RESEND_API_KEY"
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

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "led.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.LoginPath = "/Giris";
        options.AccessDeniedPath = "/ErisimEngellendi";
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizePage("/Hesap");
    options.Conventions.AuthorizePage("/Destek");
    options.Conventions.AuthorizePage("/Talepler");
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
    options.Conventions.AllowAnonymousToPage("/Giris");
    options.Conventions.AllowAnonymousToPage("/GirisCallback");
    options.Conventions.AllowAnonymousToPage("/ErisimEngellendi");
});
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
builder.Services.AddHttpClient<ISupabaseAccountService, SupabaseAccountService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<IChatStore, SupabaseChatStore>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<ICustomerRequestStore, CustomerRequestStore>(client =>
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

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.File.Name;
        if (path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "public,max-age=604800";
        }
    }
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapSupportApi();

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

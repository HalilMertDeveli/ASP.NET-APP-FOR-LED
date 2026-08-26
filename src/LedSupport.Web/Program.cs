using LedSupport.Web.Options;
using LedSupport.Web.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

// Vercel/dashboard sometimes sets empty env placeholders (""). That breaks bool/int binding.
foreach (var key in new[]
{
    "Support__RequireFirestore",
    "Support__RateLimitPerWindow",
    "Support__RateLimitWindowMinutes",
    "Smtp__Port",
    "Smtp__EnableSsl",
    "FirebaseSupport__RateLimitPerWindow",
    "FirebaseSupport__RateLimitWindowMinutes"
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
builder.Services.Configure<FirebaseSupportSettings>(
    builder.Configuration.GetSection(FirebaseSupportSettings.SectionName));
builder.Services.Configure<SupportSettings>(builder.Configuration.GetSection(SupportSettings.SectionName));
builder.Services.Configure<ResendSettings>(builder.Configuration.GetSection(ResendSettings.SectionName));
builder.Services.Configure<FirebaseSettings>(builder.Configuration.GetSection(FirebaseSettings.SectionName));

builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IGitHubStatsService, GitHubStatsService>();
builder.Services.AddHttpClient<IResendEmailService, ResendEmailService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<FirebaseSupportRequestService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<ISupportRequestStore, FirestoreSupportRequestStore>();
builder.Services.AddScoped<DirectSupportRequestService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.AddScoped<ISupportRequestService>(sp =>
{
    var mode = sp.GetRequiredService<IOptions<SupportSettings>>().Value.Mode;
    if (string.Equals(mode, "Function", StringComparison.OrdinalIgnoreCase))
    {
        return sp.GetRequiredService<FirebaseSupportRequestService>();
    }

    return sp.GetRequiredService<DirectSupportRequestService>();
});

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

static void LogSupportConfiguration(WebApplication app)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SupportStartup");
    var support = app.Services.GetRequiredService<IOptions<SupportSettings>>().Value;
    var resend = app.Services.GetRequiredService<IOptions<ResendSettings>>().Value;
    var firebase = app.Services.GetRequiredService<IOptions<FirebaseSettings>>().Value;
    var function = app.Services.GetRequiredService<IOptions<FirebaseSupportSettings>>().Value;

    var resendOk = !string.IsNullOrWhiteSpace(resend.ApiKey) && !resend.ApiKey.Contains("YOUR_", StringComparison.Ordinal);
    var credsOk = !string.IsNullOrWhiteSpace(firebase.CredentialsPath) &&
                  !firebase.CredentialsPath.Contains("YOUR_", StringComparison.Ordinal) &&
                  File.Exists(firebase.CredentialsPath);
    var functionOk = !string.IsNullOrWhiteSpace(function.SubmitUrl) &&
                     !function.SubmitUrl.Contains("YOUR_", StringComparison.Ordinal) &&
                     !string.IsNullOrWhiteSpace(function.IngestSecret) &&
                     !function.IngestSecret.Contains("YOUR_", StringComparison.Ordinal);

    logger.LogInformation(
        "Support mode={Mode}, ResendConfigured={Resend}, FirestoreCredentials={Creds}, FunctionConfigured={Fn}, RequireFirestore={Req}",
        support.Mode,
        resendOk,
        credsOk,
        functionOk,
        support.RequireFirestore);

    if (string.Equals(support.Mode, "Direct", StringComparison.OrdinalIgnoreCase) && !resendOk)
    {
        logger.LogError(
            "Contact form will fail until Resend:ApiKey is set via user-secrets / env. " +
            "Example: dotnet user-secrets set \"Resend:ApiKey\" \"re_xxx\"");
    }
}

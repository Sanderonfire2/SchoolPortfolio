using System.IO;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SchoolPortfolio.Data;
using SchoolPortfolio.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();

// De Kestrel-servernaam niet meesturen: minder informatie voor scanners.
// (Achter IIS bepaalt de host deze header zelf.)
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddControllersWithViews();

builder.Services.AddScoped<UploadService>();
builder.Services.AddScoped<EmailService>();

// De SQLite-verbinding staat in appsettings.json als "Data Source=app.db".
// Onder IIS (gedeelde hosting) is de werkmap niet de site-map, waardoor een
// relatieve verbinding naar een onbeschrijfbare map zou wijzen. Daarom maken we
// het pad absoluut ten opzichte van de content-root.
var verbinding = builder.Configuration.GetConnectionString("Default") ?? "Data Source=app.db";
if (verbinding.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
{
    var ruwPad = verbinding["Data Source=".Length..].Trim();
    if (!string.IsNullOrWhiteSpace(ruwPad) && !Path.IsPathRooted(ruwPad))
    {
        var absoluutPad = Path.Combine(builder.Environment.ContentRootPath, ruwPad);
        verbinding = $"Data Source={absoluutPad}";
    }
}
builder.Services.AddDbContext<SiteDbContext>(options => options.UseSqlite(verbinding));

builder.Services.AddSingleton<EmailGeheimen>();

// Sleutelring voor Data Protection (antiforgery + versleutelde e-mailinstellingen).
// Deze moet op een beschrijfbare én blijvende plaats staan: anders verandert de
// sleutel na elke herstart en faalt elk antiforgery-token (elke login-POST).
// De vaste applicatienaam zorgt ervoor dat de sleutels ook na een deploy naar een
// andere fysieke map gevonden worden.
var sleutelMap = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "dp-keys");
var sleutelOpslagGelukt = true;
try
{
    Directory.CreateDirectory(sleutelMap);
}
catch (Exception ex)
{
    sleutelOpslagGelukt = false;
    Console.Error.WriteLine(
        $"WAARSCHUWING: de map voor Data Protection-sleutels ({sleutelMap}) is niet beschrijfbaar: {ex.Message}");
}

if (sleutelOpslagGelukt)
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(sleutelMap))
        .SetApplicationName("SchoolPortfolio");
}
else
{
    // Terugvallen op de standaardopslag van de host, zodat de site toch start.
    builder.Services.AddDataProtection().SetApplicationName("SchoolPortfolio");
}

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/inloggen";
        options.AccessDeniedPath = "/admin/inloggen";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Cookie.Name = "SchoolPortfolio.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // In productie gaat de inlogcookie alleen over https mee, zodat een
        // onderschepte cookie op een kale http-verbinding waardeloos is. Lokaal
        // draait de site op http, daarom SameAsRequest in Development.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });

// Antiforgery-cookie: HttpOnly en SameSite Strict. De Secure-vlag volgt het
// schema (SameAsRequest): ASP.NET Core weigert een token te maken wanneer de
// SecurePolicy Always is en de aanvraag niet via https binnenkomt, en dan zou
// elk formulier op een verkeerd geconfigureerde host een 500 geven.
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "SchoolPortfolio.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddAuthorization();

// De host (IIS/Plesk) termineert TLS en geeft het oorspronkelijke schema door in
// X-Forwarded-Proto. Zonder deze middleware denkt de app dat elke aanvraag http is
// (verkeerde redirects en cookies) en ziet hij het echte client-IP niet.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Alleen de laatste proxy-hop vertrouwen: de proxy voegt het echte
    // clientadres achteraan toe, dus een bezoeker kan de snelheidslimiet niet
    // omzeilen door zelf een X-Forwarded-For mee te sturen.
    options.ForwardLimit = 1;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Snelheidslimiet tegen brute-force inlogpogingen en misbruik van het contactformulier.
// Vaste vensters per IP-adres.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("inloggen", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "onbekend",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15)
            }));
    options.AddPolicy("contactformulier", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "onbekend",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10)
            }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    DataInitializer.Initialiseer(scope.ServiceProvider);
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/fout");
    app.UseHsts();
}

// Vriendelijke pagina voor 400/404/429 in plaats van de kale foutpagina van de
// host. De foutcode wordt doorgegeven aan /fout.
app.UseStatusCodePagesWithReExecute("/fout", "?code={0}");

// Veiligheidsheaders op elke reactie.
app.Use(async (context, volgende) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    // SAMEORIGIN in plaats van DENY: de AREI-pdf's worden in een iframe op de eigen
    // site getoond. Andere sites kunnen de pagina nog steeds niet inkaderen.
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
    headers["Cross-Origin-Opener-Policy"] = "same-origin";
    headers["X-Permitted-Cross-Domain-Policies"] = "none";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "img-src 'self' data:; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "script-src 'self' 'unsafe-inline'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-src 'self'; " +
        "frame-ancestors 'self'; " +
        "object-src 'none'";

    // Geüploade SVG's zijn afbeeldingen, geen documenten: ook na het saneren
    // krijgen ze een sandbox-CSP, zodat een script er nooit kan uitvoeren.
    if (context.Request.Path.StartsWithSegments("/uploads") &&
        context.Request.Path.Value?.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) == true)
    {
        headers["Content-Security-Policy"] =
            "default-src 'none'; img-src data:; style-src 'unsafe-inline'; sandbox";
        headers["X-Content-Type-Options"] = "nosniff";
    }

    await volgende();
});

// Beheerpagina's mogen nooit in browsercache of proxies blijven hangen.
app.Use(async (context, volgende) =>
{
    if (context.Request.Path.StartsWithSegments("/admin"))
    {
        context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        context.Response.Headers["Pragma"] = "no-cache";
    }
    await volgende();
});

app.UseStaticFiles();
app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

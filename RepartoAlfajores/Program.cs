using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Data;
using RepartoAlfajores.Filters;
using RepartoAlfajores.Services.Implementations;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.Utils;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Soporta DATABASE_URL en formato postgres:// o postgresql:// (Neon/Render)
// Npgsql requiere connection string key-value, asi que se parsea la URI.
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    // .NET Uri no reconoce el scheme postgresql://, se normaliza para poder parsear
    var normalizedUrl = databaseUrl
        .Replace("postgresql://", "https://")
        .Replace("postgres://", "https://");
    var uri = new Uri(normalizedUrl);
    var userInfo = uri.UserInfo.Split(':', 2);
    // OJO: al normalizar a https://, un puerto omitido se reporta como 443 (default https),
    // no como -1. IsDefaultPort detecta que la URL original no traía puerto -> usar 5432.
    var port = uri.IsDefaultPort ? 5432 : uri.Port;
    var database = uri.AbsolutePath.TrimStart('/');
    var connStr = $"Host={uri.Host};Port={port};Database={database};" +
                  $"Username={Uri.UnescapeDataString(userInfo[0])};Password={Uri.UnescapeDataString(userInfo[1])};" +
                  $"SSL Mode=Require;Trust Server Certificate=true";
    builder.Configuration["ConnectionStrings:DefaultConnection"] = connStr;
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null)));

// Render termina TLS en su proxy y reenvía la request como HTTP. Sin esto, la app se cree
// en texto plano: la cookie de sesión sale sin la marca Secure y UseHttpsRedirection queda
// desorientado. Sólo se confían los headers del proxy (KnownNetworks/KnownProxies se vacían
// porque la IP del proxy de Render no es fija).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Un solo usuario: se limita el endpoint entero en vez de particionar por IP. Detrás del
// proxy, RemoteIpAddress es la del proxy, así que particionar por IP daría un límite falso
// —o bypasseable falsificando X-Forwarded-For—.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(5);
        limiter.PermitLimit = 10;
        limiter.QueueLimit = 0;
    });
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        // En producción siempre Secure: con UseForwardedHeaders la app ya sabe que la request
        // original fue HTTPS. En desarrollo se sirve por HTTP plano, así que forzarlo dejaría
        // la sesión sin cookie.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

// La cookie de antiforgery la configura su propio servicio, aparte de la de autenticación,
// y por defecto no exige Secure.
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    // Convierte las NegocioException en un mensaje en pantalla en vez de un error 500.
    options.Filters.Add<ManejadorDeErroresFilter>();
});

builder.Services.AddScoped<IZonaService, ZonaService>();
builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<ICuentaCorrienteService, CuentaCorrienteService>();
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<ICobroService, CobroService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReporteService, ReporteService>();
builder.Services.AddScoped<IConfiguracionService, ConfiguracionService>();
builder.Services.AddHttpClient<IAIService, AIService>();
builder.Services.AddScoped<IVentaVozService, VentaVozService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Neon (Postgres serverless) suspende el compute tras inactividad; al despertar
    // las primeras conexiones pueden fallar. Reintentamos hasta que responda.
    for (var intento = 1; ; intento++)
    {
        try
        {
            await db.Database.MigrateAsync();
            await Seeder.SeedAsync(db);
            break;
        }
        catch (Exception ex) when (intento < 6)
        {
            Console.WriteLine($"[startup] Intento {intento} de conectar a la base falló: {ex.Message}. Reintentando en 5s...");
            await Task.Delay(5000);
        }
    }
}

// Primero de todo: el resto del pipeline necesita saber que la request original fue HTTPS.
app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append(
        "Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' https://cdnjs.cloudflare.com 'unsafe-inline'; " +
        "style-src 'self' https://cdnjs.cloudflare.com 'unsafe-inline'; " +
        "font-src 'self' https://cdnjs.cloudflare.com; " +
        "img-src 'self' data:;"
    );
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Sin esto, un NotFound() de un controller devuelve una página en blanco.
app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Devuelve el commit desplegado para poder verificar un deploy con un curl,
// sin depender del dashboard de Render.
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    commit = VersionInfo.Commit,
    rama = VersionInfo.Rama
}));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

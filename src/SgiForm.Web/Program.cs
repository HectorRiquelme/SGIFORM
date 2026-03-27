using SgiForm.Web.Components;
using SgiForm.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── Blazor Server ────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ─── Estado de autenticación (Scoped = por circuito Blazor) ──────────────────
// AuthStateService reemplaza Session/IHttpContextAccessor en componentes Blazor.
builder.Services.AddScoped<AuthStateService>();
builder.Services.AddSingleton<ReleaseNotesService>();

// ─── Cliente HTTP para la API ─────────────────────────────────────────────────
// AddHttpClient<ApiClient> registra ApiClient como Scoped automáticamente
// con un HttpClient configurado con BaseAddress. NO agregar AddScoped<ApiClient>()
// adicional porque sobreescribiría el registro y perdería el BaseAddress.
// TrimEnd('/') + "/" garantiza que BaseAddress siempre tenga trailing slash.
// Sin trailing slash, HttpClient resuelve "api/v1/auth/login" relativo al
// directorio padre, descartando el último segmento (ej: /sgiformapi).
// RFC 3986: "http://host/sgiformapi" + "api/v1" → "http://host/api/v1" (MAL)
//           "http://host/sgiformapi/" + "api/v1" → "http://host/sgiformapi/api/v1" (BIEN)
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5043";
builder.Services.AddHttpClient<ApiClient>(c =>
{
    c.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// ─── PathBase (sub-path deployment, ej: /sgiform) ────────────────────────────
// Configurar en appsettings.Production.json → "PathBase": "/sgiform"
// Debe ir ANTES de cualquier otro middleware
var pathBase = builder.Configuration["PathBase"] ?? "";
if (!string.IsNullOrEmpty(pathBase))
    app.UsePathBase(pathBase);

// ─── Middleware ───────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseAntiforgery();
// ─── Static assets (.NET 10) ─────────────────────────────────────────────────
// MapStaticAssets reemplaza UseStaticFiles en .NET 9+ para Blazor:
// sirve wwwroot + archivos de framework (_framework/blazor.web.js, etc.)
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

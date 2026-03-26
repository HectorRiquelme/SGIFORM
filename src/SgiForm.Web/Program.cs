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
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5043";
builder.Services.AddHttpClient<ApiClient>(c =>
{
    c.BaseAddress = new Uri(apiBaseUrl);
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
app.UseStaticFiles();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

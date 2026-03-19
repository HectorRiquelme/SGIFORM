using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SanitasField.Mobile.Database;
using SanitasField.Mobile.Services;
using SanitasField.Mobile.ViewModels;
using SanitasField.Mobile.Views;

namespace SanitasField.Mobile;

/// <summary>
/// Punto de entrada de la aplicación MAUI Android.
/// Configura los servicios, la base de datos local y la navegación.
/// </summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ─── HTTP Client compartido via HttpClientHolder ──────────────────────
        // Lee la URL guardada por el usuario, o usa la default
        var savedUrl = Preferences.Get("api_url", "https://api.sanitasfield.cl");
        var apiBaseUrl = savedUrl.TrimEnd('/') + "/";
        var httpHolder = new HttpClientHolder(apiBaseUrl);
        builder.Services.AddSingleton(httpHolder);

        // ─── Conectividad ─────────────────────────────────────────────────────
        builder.Services.AddSingleton(Connectivity.Current);
        builder.Services.AddSingleton(Geolocation.Default);
        builder.Services.AddSingleton(MediaPicker.Default);

        // ─── Base de datos local ──────────────────────────────────────────────
        builder.Services.AddSingleton<AppDatabase>();

        // ─── Servicios ────────────────────────────────────────────────────────
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<SyncService>();
        builder.Services.AddTransient<FlowEngine>();

        // ─── ViewModels ───────────────────────────────────────────────────────
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<InspeccionesListViewModel>();
        builder.Services.AddTransient<InspeccionViewModel>();
        builder.Services.AddTransient<SincronizacionViewModel>();

        // ─── Pages ────────────────────────────────────────────────────────────
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<InspeccionesListPage>();
        builder.Services.AddTransient<InspeccionPage>();
        builder.Services.AddTransient<SincronizacionPage>();

        return builder.Build();
    }
}

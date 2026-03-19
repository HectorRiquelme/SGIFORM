using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SanitasField.Mobile.Database;
using SanitasField.Mobile.Services;

namespace SanitasField.Mobile.ViewModels;

public partial class SincronizacionViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly SyncService _sync;
    private readonly AppDatabase _db;

    public SincronizacionViewModel(AuthService auth, SyncService sync, AppDatabase db)
    {
        _auth = auth;
        _sync = sync;
        _db = db;
    }

    [ObservableProperty] private string nombreOperador = "";
    [ObservableProperty] private string empresaSlug = "";
    [ObservableProperty] private bool tieneConexion;
    [ObservableProperty] private string ultimaSyncTexto = "Nunca";
    [ObservableProperty] private int pendientesSync;
    [ObservableProperty] private string? mensajeSync;
    [ObservableProperty] private bool sincronizando;
    public string AppVersion => $"v{AppInfo.VersionString} — SGI-FORM";

    [RelayCommand]
    public async Task CargarAsync()
    {
        NombreOperador = _auth.OperadorNombre ?? "Operador";
        EmpresaSlug = _auth.TenantSlug ?? "";
        TieneConexion = _sync.TieneConexion;

        var stats = await _db.GetEstadisticasAsync();
        PendientesSync = stats?.PendientesSync ?? 0;

        var ultima = _auth.UltimaSync;
        UltimaSyncTexto = ultima.HasValue
            ? ultima.Value.LocalDateTime.ToString("dd/MM/yyyy HH:mm")
            : "Nunca";
    }

    [RelayCommand]
    public async Task SincronizarAsync()
    {
        if (!_sync.TieneConexion)
        {
            MensajeSync = "Sin conexión a Internet.";
            return;
        }

        Sincronizando = true;
        MensajeSync = "Sincronizando...";

        try
        {
            var progress = new Progress<string>(msg => MensajeSync = msg);
            var result = await _sync.SincronizarCompletoAsync(progress);

            MensajeSync = result.Exitoso
                ? $"Listo: {result.Recibidos} recibidas, {result.Enviados} enviadas"
                : $"Error: {result.ErrorMsg}";

            await CargarAsync();
        }
        catch (Exception ex)
        {
            MensajeSync = $"Error: {ex.Message}";
        }
        finally
        {
            Sincronizando = false;
        }
    }

    [RelayCommand]
    public async Task CerrarSesionAsync()
    {
        bool confirmar = await Shell.Current.DisplayAlert(
            "Cerrar sesión",
            "¿Estás seguro? Los datos no sincronizados se perderán.",
            "Cerrar sesión", "Cancelar");

        if (!confirmar) return;

        await _auth.LogoutAsync();
        await Shell.Current.GoToAsync("//login");
    }
}

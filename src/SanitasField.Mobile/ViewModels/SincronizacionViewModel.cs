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
    private readonly ReleaseNotesService _releaseNotes;

    public SincronizacionViewModel(
        AuthService auth,
        SyncService sync,
        AppDatabase db,
        ReleaseNotesService releaseNotes)
    {
        _auth = auth;
        _sync = sync;
        _db = db;
        _releaseNotes = releaseNotes;
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

    [RelayCommand]
    public async Task VerNotasVersionAsync()
    {
        var documento = await _releaseNotes.GetAsync();
        if (documento.Notes.Count == 0)
        {
            await Shell.Current.DisplayAlert(
                "Notas de versión",
                "No hay notas de versión disponibles localmente.",
                "OK");
            return;
        }

        var ultima = documento.Notes
            .OrderByDescending(n => n.Date)
            .First();

        var detalle = string.Join("\n", ultima.Changes.Select(c => $"- {c}"));
        var mensaje = $"v{ultima.Version} ({ultima.Date})\n{ultima.Title}\n\n{detalle}";

        await Shell.Current.DisplayAlert("Notas de versión", mensaje, "Cerrar");
    }
}

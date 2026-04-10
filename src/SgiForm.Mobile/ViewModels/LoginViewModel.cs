using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SgiForm.Mobile.Services;

namespace SgiForm.Mobile.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly SyncService _sync;
    private readonly ReleaseNotesService _releaseNotes;

    public LoginViewModel(AuthService auth, SyncService sync, ReleaseNotesService releaseNotes)
    {
        _auth = auth;
        _sync = sync;
        _releaseNotes = releaseNotes;
        ApiUrl = Preferences.Get("api_url", "https://apps.solucionescloud.cl/sgiformapi");
        CodigoOperador = Preferences.Get("last_codigo_operador", "");
        EmpresaSlug = Preferences.Get("last_empresa_slug", "");
#if DEBUG
        if (string.IsNullOrEmpty(CodigoOperador)) CodigoOperador = "OP001";
        if (string.IsNullOrEmpty(EmpresaSlug)) EmpresaSlug = "sanitaria-demo";
        Password = "Op@123";
#endif
    }

    [ObservableProperty] private string codigoOperador = "";
    [ObservableProperty] private string empresaSlug = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string apiUrl = "";
    [ObservableProperty] private bool iniciandoSesion = false;
    [ObservableProperty] private string? errorMsg;
    [ObservableProperty] private bool mostrarConfigAvanzada = false;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(CodigoOperador) ||
            string.IsNullOrWhiteSpace(EmpresaSlug) ||
            string.IsNullOrWhiteSpace(Password))
        {
            ErrorMsg = "Completa todos los campos";
            return;
        }

        IniciandoSesion = true;
        ErrorMsg = null;

        var url = ApiUrl.TrimEnd('/') + "/";
        Preferences.Set("api_url", ApiUrl);
        Preferences.Set("last_codigo_operador", CodigoOperador);
        Preferences.Set("last_empresa_slug", EmpresaSlug);
        _auth.ActualizarBaseUrl(url);

        var result = await _auth.LoginAsync(CodigoOperador, EmpresaSlug, Password);

        if (result.Success)
        {
            // Navegar a la lista de inspecciones
            await Shell.Current.GoToAsync("//inspecciones");

            // Descargar asignaciones en background (con manejo de errores)
            _ = Task.Run(async () =>
            {
                try { await _sync.DescargarAsignacionesAsync(); }
                catch { /* Se reintentará en la próxima sincronización */ }
            });
        }
        else
        {
            ErrorMsg = result.Error;
        }

        IniciandoSesion = false;
    }

    [RelayCommand]
    private void ToggleConfigAvanzada() =>
        MostrarConfigAvanzada = !MostrarConfigAvanzada;

    [RelayCommand]
    private async Task VerNotasVersionAsync()
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

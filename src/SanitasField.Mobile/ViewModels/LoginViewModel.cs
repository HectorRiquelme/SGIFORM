using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SanitasField.Mobile.Services;

namespace SanitasField.Mobile.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly SyncService _sync;

    public LoginViewModel(AuthService auth, SyncService sync)
    {
        _auth = auth;
        _sync = sync;
        // Leer URL guardada
        ApiUrl = Preferences.Get("api_url", "https://api.sanitasfield.cl");
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

        // Guardar y aplicar URL de API
        var url = ApiUrl.TrimEnd('/') + "/";
        Preferences.Set("api_url", ApiUrl);
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
}

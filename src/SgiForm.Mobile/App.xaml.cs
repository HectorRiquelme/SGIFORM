using SgiForm.Mobile.Services;

namespace SgiForm.Mobile;

public partial class App : Application
{
    private readonly AuthService _auth;

    public App(AuthService auth)
    {
        InitializeComponent();
        _auth = auth;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var shell = new AppShell();

        // Inicializar token desde SecureStorage (async)
        // y navegar si ya tiene sesión DESPUÉS de que el Shell esté listo
        shell.Loaded += async (_, _) =>
        {
            await _auth.InitializeAsync();
            if (_auth.EsAutenticado)
            {
                try
                {
                    await shell.GoToAsync("//inspecciones");
                }
                catch
                {
                    // Si la navegación falla, quedarse en login
                }
            }
        };

        return new Window(shell);
    }
}

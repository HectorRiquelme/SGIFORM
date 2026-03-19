using SanitasField.Mobile.ViewModels;

namespace SanitasField.Mobile.Views;

/// <summary>
/// Página de ejecución de inspección. Renderiza el formulario dinámico
/// basado en la estructura de flujo descargada del servidor.
/// </summary>
[QueryProperty(nameof(AsignacionId), "asignacion_id")]
public partial class InspeccionPage : ContentPage
{
    private readonly InspeccionViewModel _vm;

    public string? AsignacionId
    {
        get => _vm.AsignacionId;
        set => _vm.AsignacionId = value;
    }

    public InspeccionPage(InspeccionViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _vm.InicializarAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnAppearing: {ex.Message}");
        }
    }
}

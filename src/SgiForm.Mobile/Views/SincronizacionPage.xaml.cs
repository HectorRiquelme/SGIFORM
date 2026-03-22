using SgiForm.Mobile.ViewModels;

namespace SgiForm.Mobile.Views;

public partial class SincronizacionPage : ContentPage
{
    private readonly SincronizacionViewModel _vm;

    public SincronizacionPage(SincronizacionViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _vm.CargarAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnAppearing: {ex.Message}");
        }
    }
}

using SanitasField.Mobile.ViewModels;

namespace SanitasField.Mobile.Views;

public partial class InspeccionesListPage : ContentPage
{
    private readonly InspeccionesListViewModel _vm;

    public InspeccionesListPage(InspeccionesListViewModel vm)
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

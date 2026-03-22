using SgiForm.Mobile.Views;

namespace SgiForm.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Registrar rutas para navegación programática
        Routing.RegisterRoute("inspeccion", typeof(InspeccionPage));
    }
}

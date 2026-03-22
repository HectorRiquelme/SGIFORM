using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SgiForm.Mobile.Database;
using SgiForm.Mobile.Models;
using SgiForm.Mobile.Services;

namespace SgiForm.Mobile.ViewModels;

public partial class InspeccionesListViewModel : ObservableObject
{
    private readonly AppDatabase _db;
    private readonly SyncService _sync;
    private readonly AuthService _auth;

    public InspeccionesListViewModel(AppDatabase db, SyncService sync, AuthService auth)
    {
        _db = db;
        _sync = sync;
        _auth = auth;
    }

    [ObservableProperty] private List<AsignacionLocal> asignaciones = new();
    [ObservableProperty] private List<AsignacionLocal> asignacionesFiltradas = new();
    [ObservableProperty] private string filtroEstado = "todas";
    [ObservableProperty] private string? textoBusqueda;
    [ObservableProperty] private bool cargando = false;
    [ObservableProperty] private bool sincronizando = false;
    [ObservableProperty] private string? mensajeSync;
    [ObservableProperty] private int pendientesSync = 0;
    [ObservableProperty] private EstadisticasLocales? estadisticas;
    [ObservableProperty] private bool tieneConexion;
    [ObservableProperty] private string nombreOperador = "";

    [RelayCommand]
    public async Task CargarAsync()
    {
        Cargando = true;
        TieneConexion = _sync.TieneConexion;
        NombreOperador = _auth.OperadorNombre ?? "Operador";

        Asignaciones = await _db.GetAsignacionesAsync();
        Estadisticas = await _db.GetEstadisticasAsync();
        PendientesSync = Estadisticas?.PendientesSync ?? 0;
        AplicarFiltros();

        Cargando = false;
    }

    [RelayCommand]
    public async Task SincronizarAsync()
    {
        if (!_sync.TieneConexion)
        {
            MensajeSync = "Sin conexión. Los datos se sincronizarán cuando tengas Internet.";
            return;
        }

        Sincronizando = true;
        MensajeSync = "Sincronizando...";

        var progress = new Progress<string>(msg => MensajeSync = msg);
        var result = await _sync.SincronizarCompletoAsync(progress);

        MensajeSync = result.Exitoso
            ? $"Sincronizado: {result.Recibidos} recibidas, {result.Enviados} enviadas"
            : $"Error: {result.ErrorMsg}";

        await CargarAsync();
        Sincronizando = false;
    }

    [RelayCommand]
    public void FiltrarPorEstado(string estado)
    {
        FiltroEstado = estado;
        AplicarFiltros();
    }

    partial void OnTextoBusquedaChanged(string? value) => AplicarFiltros();
    partial void OnFiltroEstadoChanged(string value) => AplicarFiltros();

    private void AplicarFiltros()
    {
        var q = Asignaciones.AsEnumerable();

        // Filtro de estado
        if (FiltroEstado != "todas")
            q = q.Where(a => a.Estado == FiltroEstado);

        // Búsqueda de texto
        if (!string.IsNullOrWhiteSpace(TextoBusqueda))
        {
            var busq = TextoBusqueda.ToLower();
            q = q.Where(a =>
                (a.IdServicio?.Contains(busq) == true) ||
                (a.NumeroMedidor?.Contains(busq) == true) ||
                (a.Direccion?.ToLower().Contains(busq) == true) ||
                (a.NombreCliente?.ToLower().Contains(busq) == true) ||
                (a.Ruta?.Contains(busq) == true));
        }

        AsignacionesFiltradas = q.ToList();
    }

    [RelayCommand]
    public async Task AbrirInspeccionAsync(AsignacionLocal asignacion)
    {
        await Shell.Current.GoToAsync(
            $"inspeccion?asignacion_id={asignacion.Id}");
    }
}

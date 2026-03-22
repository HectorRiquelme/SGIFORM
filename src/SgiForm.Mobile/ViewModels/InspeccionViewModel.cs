using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SgiForm.Mobile.Database;
using SgiForm.Mobile.Models;
using SgiForm.Mobile.Services;

namespace SgiForm.Mobile.ViewModels;

/// <summary>
/// ViewModel para la ejecución de una inspección en campo.
/// Coordina el FlowEngine, la BD local y la captura de fotos/GPS.
/// Implementa MVVM con CommunityToolkit para reducir boilerplate.
/// </summary>
[QueryProperty(nameof(AsignacionId), "asignacion_id")]
public partial class InspeccionViewModel : ObservableObject
{
    private readonly AppDatabase _db;
    private readonly FlowEngine _engine;
    private readonly SyncService _sync;

    public InspeccionViewModel(AppDatabase db, FlowEngine engine, SyncService sync)
    {
        _db = db;
        _engine = engine;
        _sync = sync;
        _engine.EstadoCambiado += OnEstadoCambiado;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PROPIEDADES OBSERVABLES
    // ──────────────────────────────────────────────────────────────────────────
    [ObservableProperty] private string? asignacionId;
    [ObservableProperty] private AsignacionLocal? asignacion;
    [ObservableProperty] private InspeccionLocal? inspeccion;
    [ObservableProperty] private List<SeccionLocal> secciones = new();
    [ObservableProperty] private SeccionLocal? seccionActual;
    [ObservableProperty] private List<PreguntaLocal> preguntasActuales = new();
    [ObservableProperty] private int seccionIndex = 0;
    [ObservableProperty] private int porcentajeAvance = 0;
    [ObservableProperty] private bool cargando = true;
    [ObservableProperty] private bool guardando = false;
    [ObservableProperty] private string? mensajeError;
    [ObservableProperty] private string estadoBadge = "borrador";
    [ObservableProperty] private bool tieneFotografiasPendientes = false;

    public bool EsPrimeraSeccion => SeccionIndex == 0;
    public bool EsUltimaSeccion => SeccionIndex == Secciones.Count - 1;
    public string TituloSeccion => SeccionActual?.Titulo ?? "";
    public int TotalSecciones => Secciones.Count;
    public string ProgressText => $"Sección {SeccionIndex + 1} de {TotalSecciones}";

    // ──────────────────────────────────────────────────────────────────────────
    // INICIALIZACIÓN
    // ──────────────────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task InicializarAsync()
    {
        if (AsignacionId == null) return;

        Cargando = true;
        MensajeError = null;

        try
        {
            // Cargar asignación
            Asignacion = await _db.GetAsignacionAsync(AsignacionId);
            if (Asignacion == null)
            {
                MensajeError = "Asignación no encontrada en el dispositivo";
                return;
            }

            // Buscar o crear inspección
            Inspeccion = await _db.GetInspeccionByAsignacionAsync(AsignacionId);
            if (Inspeccion == null)
            {
                Inspeccion = new InspeccionLocal
                {
                    AsignacionId = AsignacionId,
                    FlujoVersionId = Asignacion.FlujoVersionId,
                    Estado = "en_progreso",
                    FechaInicio = DateTime.UtcNow
                };
                await _db.SaveInspeccionAsync(Inspeccion);

                // Actualizar estado de asignación
                Asignacion.Estado = "en_ejecucion";
                await _db.UpsertAsignacionAsync(Asignacion);
            }

            // Inicializar motor de formulario
            if (string.IsNullOrEmpty(Asignacion.FlujoVersionId))
            {
                MensajeError = "Esta asignación no tiene un flujo asociado";
                return;
            }
            await _engine.InicializarAsync(Asignacion.FlujoVersionId, Inspeccion.Id);

            // Cargar secciones visibles
            Secciones = _engine.GetSecciones();
            NavigarSeccion(0);

            EstadoBadge = Inspeccion.Estado;
            PorcentajeAvance = _engine.GetPorcentajeAvance();

            // Capturar GPS de inicio (con manejo de errores)
            _ = Task.Run(async () =>
            {
                try { await CapturarGpsInicioAsync(); }
                catch { /* GPS no disponible */ }
            });
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al cargar: {ex.Message}";
        }
        finally
        {
            Cargando = false;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // NAVEGACIÓN ENTRE SECCIONES
    // ──────────────────────────────────────────────────────────────────────────
    [RelayCommand]
    public void SiguienteSeccion()
    {
        if (SeccionIndex < Secciones.Count - 1)
            NavigarSeccion(SeccionIndex + 1);
    }

    [RelayCommand]
    public void AnteriorSeccion()
    {
        if (SeccionIndex > 0)
            NavigarSeccion(SeccionIndex - 1);
    }

    private void NavigarSeccion(int index)
    {
        SeccionIndex = index;
        SeccionActual = Secciones.ElementAtOrDefault(index);

        if (SeccionActual != null)
        {
            PreguntasActuales = _engine.GetPreguntasDeSección(SeccionActual.Id);
        }

        OnPropertyChanged(nameof(EsPrimeraSeccion));
        OnPropertyChanged(nameof(EsUltimaSeccion));
        OnPropertyChanged(nameof(TituloSeccion));
        OnPropertyChanged(nameof(ProgressText));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // RESPONDER PREGUNTAS
    // ──────────────────────────────────────────────────────────────────────────
    public void ResponderPregunta(string preguntaId, object? valor)
    {
        _engine.ActualizarRespuesta(preguntaId, valor);

        // Actualizar preguntas visibles en sección actual
        if (SeccionActual != null)
            PreguntasActuales = _engine.GetPreguntasDeSección(SeccionActual.Id);
    }

    public RespuestaLocal? GetRespuesta(string preguntaId) =>
        _engine.GetRespuesta(preguntaId);

    // ──────────────────────────────────────────────────────────────────────────
    // GUARDAR BORRADOR
    // ──────────────────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task GuardarBorradorAsync()
    {
        if (Inspeccion == null) return;

        Guardando = true;
        try
        {
            await PersistirRespuestasAsync();

            Inspeccion.Estado = "en_progreso";
            Inspeccion.TotalRespondidas = _engine.GetTodasLasRespuestas().Count;
            await _db.SaveInspeccionAsync(Inspeccion);
        }
        finally
        {
            Guardando = false;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CERRAR INSPECCIÓN
    // ──────────────────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task<bool> CerrarInspeccionAsync()
    {
        if (Inspeccion == null) return false;

        // Validar antes de cerrar
        var errores = _engine.ValidarCierre();
        if (errores.Any())
        {
            MensajeError = "Campos obligatorios sin completar:\n• " + string.Join("\n• ", errores);
            return false;
        }

        Guardando = true;
        MensajeError = null;

        try
        {
            await PersistirRespuestasAsync();

            // Capturar GPS de cierre
            await CapturarGpsCierreAsync();

            Inspeccion.Estado = "completada";
            Inspeccion.FechaFin = DateTime.UtcNow;
            Inspeccion.TotalRespondidas = _engine.GetTodasLasRespuestas().Count;
            Inspeccion.DuracionSegundos = (int)(
                (Inspeccion.FechaFin.Value - (Inspeccion.FechaInicio ?? Inspeccion.FechaFin.Value))
                .TotalSeconds);

            await _db.SaveInspeccionAsync(Inspeccion);

            // Actualizar asignación
            if (Asignacion != null)
            {
                Asignacion.Estado = "finalizada";
                await _db.UpsertAsignacionAsync(Asignacion);
            }

            // Encolar para sync
            await _db.EnqueueAsync(new SyncQueueItem
            {
                EntityType = "inspeccion",
                EntityId = Inspeccion.Id,
                Operation = "UPDATE",
                PayloadJson = Inspeccion.Id // Referencia para recuperar luego
            });

            // Si hay conexión, sincronizar inmediatamente (con manejo de errores)
            if (_sync.TieneConexion)
            {
                _ = Task.Run(async () =>
                {
                    try { await _sync.SincronizarCompletoAsync(); }
                    catch { /* Se reintentará en la próxima sincronización */ }
                });
            }

            EstadoBadge = "completada";
            return true;
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al cerrar: {ex.Message}";
            return false;
        }
        finally
        {
            Guardando = false;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GPS
    // ──────────────────────────────────────────────────────────────────────────
    private async Task CapturarGpsInicioAsync()
    {
        if (Inspeccion == null) return;
        try
        {
            var loc = await Geolocation.GetLastKnownLocationAsync();
            loc ??= await Geolocation.GetLocationAsync(new GeolocationRequest(
                GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));

            if (loc != null && Inspeccion != null)
            {
                Inspeccion.CoordXInicio = loc.Longitude;
                Inspeccion.CoordYInicio = loc.Latitude;
                Inspeccion.PrecisionGps = loc.Accuracy;
                await _db.SaveInspeccionAsync(Inspeccion);
            }
        }
        catch { /* GPS no disponible */ }
    }

    private async Task CapturarGpsCierreAsync()
    {
        if (Inspeccion == null) return;
        try
        {
            var loc = await Geolocation.GetLocationAsync(new GeolocationRequest(
                GeolocationAccuracy.Best, TimeSpan.FromSeconds(5)));

            if (loc != null)
            {
                Inspeccion.CoordXFin = loc.Longitude;
                Inspeccion.CoordYFin = loc.Latitude;
            }
        }
        catch { /* GPS no disponible */ }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // FOTOGRAFÍAS
    // ──────────────────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task CapturarFotoAsync(string preguntaId)
    {
        if (Inspeccion == null) return;

        try
        {
            // Tomar foto con la cámara
            var foto = await MediaPicker.Default.CapturePhotoAsync();
            if (foto == null) return;

            // Obtener GPS de la foto
            double? lat = null, lon = null;
            try
            {
                var loc = await Geolocation.GetLastKnownLocationAsync();
                if (loc != null) { lat = loc.Latitude; lon = loc.Longitude; }
            }
            catch { }

            // Comprimir y guardar
            var destPath = Path.Combine(
                FileSystem.CacheDirectory, "fotos", Inspeccion.Id);
            Directory.CreateDirectory(destPath);
            var fileName = $"{Guid.NewGuid()}.jpg";
            var destFile = Path.Combine(destPath, fileName);

            await using var srcStream = await foto.OpenReadAsync();
            // Comprimir imagen (resize a max 1920px, calidad 75%)
            await ComprimirImagenAsync(srcStream, destFile);

            var tamanio = new FileInfo(destFile).Length;

            // Guardar en BD local
            var fotografiaLocal = new FotografiaLocal
            {
                InspeccionId = Inspeccion.Id,
                PreguntaId = preguntaId,
                RutaLocal = destFile,
                NombreArchivo = fileName,
                TamanioBytes = tamanio,
                CoordenadaX = lon,
                CoordenadaY = lat,
                Orden = (await _db.GetFotografiasAsync(Inspeccion.Id)).Count
            };

            await _db.SaveFotografiaAsync(fotografiaLocal);

            // Encolar para sync
            await _db.EnqueueAsync(new SyncQueueItem
            {
                EntityType = "fotografia",
                EntityId = fotografiaLocal.Id,
                Operation = "INSERT"
            });

            // Actualizar respuesta con path de foto
            _engine.ActualizarRespuesta(preguntaId, destFile);

            Inspeccion.TotalFotografias++;
            await _db.SaveInspeccionAsync(Inspeccion);

            TieneFotografiasPendientes = true;
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al capturar foto: {ex.Message}";
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // HELPERS PRIVADOS
    // ──────────────────────────────────────────────────────────────────────────
    private async Task PersistirRespuestasAsync()
    {
        if (Inspeccion == null) return;

        foreach (var kvp in _engine.GetTodasLasRespuestas())
        {
            kvp.Value.InspeccionId = Inspeccion.Id;
            await _db.SaveRespuestaAsync(kvp.Value);
        }
    }

    private static async Task ComprimirImagenAsync(Stream input, string destPath)
    {
        // Implementación simplificada - en producción usar SkiaSharp o similar
        // para redimensionar a max 1920px y comprimir al 75% de calidad JPEG
        await using var output = File.Create(destPath);
        await input.CopyToAsync(output);
    }

    private void OnEstadoCambiado()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PorcentajeAvance = _engine.GetPorcentajeAvance();
            if (SeccionActual != null)
                PreguntasActuales = _engine.GetPreguntasDeSección(SeccionActual.Id);
        });
    }
}

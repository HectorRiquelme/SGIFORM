using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SanitasField.Mobile.Database;
using SanitasField.Mobile.Models;

namespace SanitasField.Mobile.Services;

/// <summary>
/// Servicio de sincronización con el servidor API.
/// Implementa el protocolo offline-first:
///   1. Descargar asignaciones y flujos
///   2. Subir inspecciones completadas
///   3. Subir fotografías (batch, comprimidas)
///   4. Confirmar sincronización
/// </summary>
public class SyncService
{
    private readonly HttpClientHolder _httpHolder;
    private readonly AppDatabase _db;
    private readonly AuthService _auth;
    private readonly IConnectivity _connectivity;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public SyncService(HttpClientHolder httpHolder, AppDatabase db, AuthService auth, IConnectivity connectivity)
    {
        _httpHolder = httpHolder;
        _db = db;
        _auth = auth;
        _connectivity = connectivity;
    }

    private HttpClient Http => _httpHolder.Client;

    public bool TieneConexion =>
        _connectivity.NetworkAccess == NetworkAccess.Internet;

    // ──────────────────────────────────────────────────────────────────────────
    // DESCARGA DE ASIGNACIONES
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<SyncResult> DescargarAsignacionesAsync(
        DateTimeOffset? desde = null,
        IProgress<string>? progreso = null)
    {
        if (!TieneConexion)
            return SyncResult.Error("Sin conexión a Internet");

        try
        {
            progreso?.Report("Conectando con el servidor...");
            SetAuthHeader();

            var url = "api/v1/sync/download";
            if (desde.HasValue)
                url += $"?desde={Uri.EscapeDataString(desde.Value.ToString("O"))}";

            var response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return SyncResult.Error($"Error del servidor: {(int)response.StatusCode}");

            progreso?.Report("Procesando datos descargados...");
            var json = await response.Content.ReadAsStringAsync();
            var paquete = JsonSerializer.Deserialize<DownloadPackage>(json, JsonOpts);

            if (paquete == null) return SyncResult.Error("Respuesta vacía del servidor");

            int asignacionesGuardadas = 0;

            // Guardar asignaciones + flujos
            foreach (var asig in paquete.Asignaciones ?? Enumerable.Empty<AsignacionDownload>())
            {
                // Guardar flujo si no está en local
                if (asig.FlujoVersion != null)
                    await GuardarFlujoAsync(asig.FlujoVersion);

                var local = MapearAsignacion(asig);
                await _db.UpsertAsignacionAsync(local);
                asignacionesGuardadas++;
            }

            // Guardar catálogos
            foreach (var cat in paquete.Catalogos ?? Enumerable.Empty<CatalogoDownload>())
            {
                await _db.UpsertCatalogoAsync(new CatalogoLocal
                {
                    Tipo = cat.Tipo,
                    Codigo = cat.Codigo,
                    Texto = cat.Texto,
                    Orden = cat.Orden,
                    Activo = cat.Activo
                });
            }

            progreso?.Report($"Descargadas {asignacionesGuardadas} asignaciones");
            return SyncResult.Ok(asignacionesGuardadas, 0);
        }
        catch (Exception ex)
        {
            return SyncResult.Error($"Error de sincronización: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SUBIDA DE INSPECCIONES
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<SyncResult> SubirInspeccionesAsync(IProgress<string>? progreso = null)
    {
        if (!TieneConexion)
            return SyncResult.Error("Sin conexión a Internet");

        try
        {
            SetAuthHeader();

            // Obtener inspecciones no sincronizadas
            var pendientes = await _db.GetPendingSyncAsync(20);
            var inspeccionItems = pendientes
                .Where(s => s.EntityType == "inspeccion")
                .ToList();

            if (!inspeccionItems.Any())
                return SyncResult.Ok(0, 0);

            progreso?.Report($"Subiendo {inspeccionItems.Count} inspecciones...");

            var inspecciones = new List<object>();

            foreach (var item in inspeccionItems)
            {
                var inspeccion = await _db.GetInspeccionAsync(item.EntityId!);
                if (inspeccion == null) continue;

                var respuestas = await _db.GetRespuestasAsync(inspeccion.Id);

                inspecciones.Add(new
                {
                    asignacion_id = inspeccion.AsignacionId,
                    estado = inspeccion.Estado,
                    fecha_inicio = inspeccion.FechaInicio,
                    fecha_fin = inspeccion.FechaFin,
                    coord_x_inicio = inspeccion.CoordXInicio,
                    coord_y_inicio = inspeccion.CoordYInicio,
                    coord_x_fin = inspeccion.CoordXFin,
                    coord_y_fin = inspeccion.CoordYFin,
                    app_version = AppInfo.VersionString,
                    respuestas = respuestas.Select(r => new
                    {
                        pregunta_id = r.PreguntaId,
                        tipo_control = r.TipoControl,
                        valor_texto = r.ValorTexto,
                        valor_entero = r.ValorEntero,
                        valor_decimal = r.ValorDecimal,
                        valor_fecha = DateTimeOffset.TryParse(r.ValorFecha, out var parsedDate)
                            ? parsedDate
                            : (DateTimeOffset?)null,
                        valor_booleano = r.ValorBooleano,
                        valor_json = r.ValorJson
                    }).ToList()
                });
            }

            var payload = new { inspecciones };
            var content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOpts),
                Encoding.UTF8, "application/json");

            var response = await Http.PostAsync("api/v1/sync/upload", content);

            if (response.IsSuccessStatusCode)
            {
                foreach (var item in inspeccionItems)
                    await _db.MarkSyncSentAsync(item.Id);

                progreso?.Report($"Subidas {inspeccionItems.Count} inspecciones exitosamente");
                return SyncResult.Ok(0, inspeccionItems.Count);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                foreach (var item in inspeccionItems)
                    await _db.MarkSyncErrorAsync(item.Id, error);
                return SyncResult.Error($"Error al subir: {error}");
            }
        }
        catch (Exception ex)
        {
            return SyncResult.Error(ex.Message);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SUBIDA DE FOTOGRAFÍAS
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<SyncResult> SubirFotografiasAsync(IProgress<string>? progreso = null)
    {
        if (!TieneConexion)
            return SyncResult.Error("Sin conexión a Internet");

        try
        {
            SetAuthHeader();

            var pendientes = await _db.GetPendingSyncAsync(50);
            var fotoItems = pendientes.Where(s => s.EntityType == "fotografia").ToList();

            if (!fotoItems.Any()) return SyncResult.Ok(0, 0);

            int subidas = 0;

            // Subir en batches de 5
            foreach (var batch in fotoItems.Chunk(5))
            {
                foreach (var item in batch)
                {
                    var foto = await _db.GetFotografiaByIdAsync(item.EntityId!);
                    if (foto == null || !File.Exists(foto.RutaLocal)) continue;

                    progreso?.Report($"Subiendo foto {subidas + 1}/{fotoItems.Count}...");

                    await using var fs = File.OpenRead(foto.RutaLocal!);
                    var content = new MultipartFormDataContent();
                    content.Add(new StringContent(foto.InspeccionId!), "inspeccionId");
                    if (foto.PreguntaId != null)
                        content.Add(new StringContent(foto.PreguntaId), "preguntaId");
                    if (foto.CoordenadaX.HasValue)
                        content.Add(new StringContent(foto.CoordenadaX.Value.ToString()), "coordX");
                    if (foto.CoordenadaY.HasValue)
                        content.Add(new StringContent(foto.CoordenadaY.Value.ToString()), "coordY");

                    var fileContent = new StreamContent(fs);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                    content.Add(fileContent, "fotos", foto.NombreArchivo!);

                    var response = await Http.PostAsync("api/v1/sync/photos", content);

                    if (response.IsSuccessStatusCode)
                    {
                        await _db.MarkSyncSentAsync(item.Id);
                        subidas++;
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        await _db.MarkSyncErrorAsync(item.Id, error);
                    }
                }
            }

            return SyncResult.Ok(0, subidas);
        }
        catch (Exception ex)
        {
            return SyncResult.Error(ex.Message);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SINCRONIZACIÓN COMPLETA
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<SyncResult> SincronizarCompletoAsync(IProgress<string>? progreso = null)
    {
        progreso?.Report("Iniciando sincronización...");

        // 1. Descargar novedades
        var descarga = await DescargarAsignacionesAsync(
            desde: _auth.UltimaSync, progreso: progreso);

        progreso?.Report("Subiendo inspecciones completadas...");

        // 2. Subir inspecciones
        var subida = await SubirInspeccionesAsync(progreso);

        // 3. Subir fotos
        progreso?.Report("Subiendo fotografías...");
        var fotos = await SubirFotografiasAsync(progreso);

        // 4. Actualizar timestamp de última sync
        if (descarga.Exitoso && subida.Exitoso)
            _auth.UltimaSync = DateTimeOffset.UtcNow;

        progreso?.Report("Sincronización completada");

        return new SyncResult(
            descarga.Exitoso && subida.Exitoso,
            descarga.Recibidos,
            subida.Enviados + fotos.Enviados,
            descarga.ErrorMsg ?? subida.ErrorMsg ?? fotos.ErrorMsg);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // HELPERS PRIVADOS
    // ──────────────────────────────────────────────────────────────────────────
    private void SetAuthHeader()
    {
        var token = _auth.AccessToken;
        if (!string.IsNullOrEmpty(token))
            Http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task GuardarFlujoAsync(FlujoVersionDownload fv)
    {
        await _db.UpsertFlujoVersionAsync(new FlujoVersionLocal
        {
            Id = fv.Id,
            FlujoId = fv.FlujoId,
            NumeroVersion = fv.NumeroVersion,
            DescargadoEn = DateTime.UtcNow
        });

        foreach (var seccion in fv.Secciones ?? Enumerable.Empty<SeccionDownload>())
        {
            await _db.UpsertSeccionAsync(new SeccionLocal
            {
                Id = seccion.Id, FlujoVersionId = fv.Id,
                Codigo = seccion.Codigo, Titulo = seccion.Titulo,
                Descripcion = seccion.Descripcion, Orden = seccion.Orden,
                CondicionalJson = seccion.CondicionalJson
            });

            foreach (var pregunta in seccion.Preguntas ?? Enumerable.Empty<PreguntaDownload>())
            {
                await _db.UpsertPreguntaAsync(new PreguntaLocal
                {
                    Id = pregunta.Id, FlujoVersionId = fv.Id, SeccionId = seccion.Id,
                    Codigo = pregunta.Codigo, Texto = pregunta.Texto,
                    TipoControl = pregunta.TipoControl, Obligatorio = pregunta.Obligatorio,
                    Orden = pregunta.Orden, Visible = pregunta.Visible,
                    Editable = pregunta.Editable, ValorPorDefecto = pregunta.ValorPorDefecto,
                    ValidacionesJson = pregunta.ValidacionesJson ?? "{}",
                    ConfiguracionJson = pregunta.ConfiguracionJson ?? "{}"
                });

                foreach (var opcion in pregunta.Opciones ?? Enumerable.Empty<OpcionDownload>())
                {
                    await _db.UpsertOpcionAsync(new OpcionLocal
                    {
                        Id = opcion.Id, PreguntaId = pregunta.Id,
                        Codigo = opcion.Codigo, Texto = opcion.Texto,
                        Orden = opcion.Orden, Activo = opcion.Activo
                    });
                }
            }
        }

        foreach (var regla in fv.Reglas ?? Enumerable.Empty<ReglaDownload>())
        {
            await _db.UpsertReglaAsync(new ReglaLocal
            {
                Id = regla.Id, FlujoVersionId = fv.Id,
                PreguntaOrigenId = regla.PreguntaOrigenId,
                Operador = regla.Operador, ValorComparacion = regla.ValorComparacion,
                Accion = regla.Accion, PreguntaDestinoId = regla.PreguntaDestinoId,
                SeccionDestinoId = regla.SeccionDestinoId,
                ParametrosJson = regla.ParametrosJson ?? "{}", Orden = regla.Orden, Activo = true
            });
        }
    }

    private static AsignacionLocal MapearAsignacion(AsignacionDownload src) => new()
    {
        Id = src.Id,
        EmpresaId = src.EmpresaId,
        ServicioId = src.ServicioInspeccion?.Id,
        TipoInspeccionId = src.TipoInspeccionId,
        TipoInspeccionNombre = src.TipoInspeccion?.Nombre,
        FlujoVersionId = src.FlujoVersionId,
        Estado = src.Estado ?? "pendiente",
        Prioridad = src.Prioridad,
        IdServicio = src.ServicioInspeccion?.IdServicio,
        NumeroMedidor = src.ServicioInspeccion?.NumeroMedidor,
        Marca = src.ServicioInspeccion?.Marca,
        Diametro = src.ServicioInspeccion?.Diametro,
        Direccion = src.ServicioInspeccion?.Direccion,
        NombreCliente = src.ServicioInspeccion?.NombreCliente,
        CoordenadaX = (double?)src.ServicioInspeccion?.CoordenadaX,
        CoordenadaY = (double?)src.ServicioInspeccion?.CoordenadaY,
        Lote = src.ServicioInspeccion?.Lote,
        Localidad = src.ServicioInspeccion?.Localidad,
        Ruta = src.ServicioInspeccion?.Ruta,
        Libreta = src.ServicioInspeccion?.Libreta,
        UpdatedAt = DateTime.UtcNow
    };
}

// ──────────────────────────────────────────────────────────────────────────────
// DTOs de descarga (deserialización del paquete del servidor)
// ──────────────────────────────────────────────────────────────────────────────
public record DownloadPackage(
    List<AsignacionDownload>? Asignaciones,
    List<CatalogoDownload>? Catalogos,
    DateTimeOffset Timestamp);

public record AsignacionDownload(
    string Id, string? EmpresaId, string? TipoInspeccionId, string? FlujoVersionId,
    string? Estado, string? Prioridad,
    TipoInspeccionDownload? TipoInspeccion,
    ServicioDownload? ServicioInspeccion,
    FlujoVersionDownload? FlujoVersion);

public record TipoInspeccionDownload(string Id, string Nombre);

public record ServicioDownload(
    string Id, string? IdServicio, string? NumeroMedidor, string? Marca, string? Diametro,
    string? Direccion, string? NombreCliente, decimal? CoordenadaX, decimal? CoordenadaY,
    string? Lote, string? Localidad, string? Ruta, string? Libreta);

public record FlujoVersionDownload(
    string Id, string? FlujoId, int NumeroVersion,
    List<SeccionDownload>? Secciones, List<ReglaDownload>? Reglas);

public record SeccionDownload(
    string Id, string? Codigo, string? Titulo, string? Descripcion,
    int Orden, bool Visible, string? CondicionalJson,
    List<PreguntaDownload>? Preguntas);

public record PreguntaDownload(
    string Id, string? Codigo, string? Texto, string? TipoControl,
    string? Placeholder, string? Ayuda, bool Obligatorio, int Orden,
    bool Visible, bool Editable, string? ValorPorDefecto,
    string? ValidacionesJson, string? ConfiguracionJson,
    List<OpcionDownload>? Opciones);

public record OpcionDownload(string Id, string? Codigo, string? Texto, int Orden, bool Activo);

public record ReglaDownload(
    string Id, string? PreguntaOrigenId, string? Operador, string? ValorComparacion,
    string? Accion, string? PreguntaDestinoId, string? SeccionDestinoId,
    string? ParametrosJson, int Orden);

public record CatalogoDownload(string? Tipo, string? Codigo, string? Texto, int Orden, bool Activo);

public record SyncResult(bool Exitoso, int Recibidos, int Enviados, string? ErrorMsg)
{
    public static SyncResult Ok(int recibidos, int enviados) => new(true, recibidos, enviados, null);
    public static SyncResult Error(string msg) => new(false, 0, 0, msg);
}

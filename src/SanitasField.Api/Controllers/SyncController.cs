using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SanitasField.Domain.Entities;
using SanitasField.Domain.Enums;
using SanitasField.Infrastructure.Persistence;
using System.Text.Json;

namespace SanitasField.Api.Controllers;

/// <summary>
/// Controller de sincronización para la app móvil.
/// Implementa el protocolo offline-first:
///   1. GET /sync/download  → descargar asignaciones + flujos
///   2. POST /sync/upload   → subir inspecciones completadas
///   3. POST /sync/photos   → subir fotografías
///   4. PUT /sync/confirm   → confirmar sync
/// </summary>
[ApiController]
[Route("api/v1/sync")]
[Authorize(Roles = "operador")]
public class SyncController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<SyncController> _logger;

    public SyncController(AppDbContext db, IConfiguration config, ILogger<SyncController> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    private Guid OperadorId =>
        Guid.Parse(User.FindFirst("operador_id")!.Value);

    private Guid EmpresaId =>
        Guid.Parse(User.FindFirst("empresa_id")!.Value);

    private string? DeviceId =>
        User.FindFirst("device_id")?.Value;

    // ──────────────────────────────────────────────────────────────────────────
    // GET /sync/download  — descargar paquete de trabajo
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] DateTimeOffset? desde)
    {
        var operador = await _db.Operadores.FindAsync(OperadorId);
        if (operador == null) return Unauthorized();

        // Asignaciones activas para este operador
        var asignaciones = await _db.AsignacionesInspeccion
            .Include(a => a.ServicioInspeccion)
            .Include(a => a.TipoInspeccion)
            .Include(a => a.FlujoVersion)
                .ThenInclude(v => v.Secciones.OrderBy(s => s.Orden))
                    .ThenInclude(s => s.Preguntas.OrderBy(p => p.Orden))
                        .ThenInclude(p => p.Opciones.Where(o => o.Activo).OrderBy(o => o.Orden))
            .Include(a => a.FlujoVersion)
                .ThenInclude(v => v.Reglas.Where(r => r.Activo).OrderBy(r => r.Orden))
            .Where(a => a.OperadorId == OperadorId
                     && a.EmpresaId == EmpresaId
                     && a.DeletedAt == null
                     && a.Estado != EstadoAsignacion.Cerrada
                     && a.Estado != EstadoAsignacion.Rechazada
                     && (desde == null || a.UpdatedAt >= desde))
            .ToListAsync();

        // Catálogos actualizados
        var catalogos = await _db.Catalogos
            .Where(c => (c.EmpresaId == null || c.EmpresaId == EmpresaId) && c.Activo)
            .ToListAsync();

        // Actualizar fecha de última sincronización
        operador.FechaUltimaSync = DateTimeOffset.UtcNow;
        operador.AppVersionUltima = Request.Headers["X-App-Version"].FirstOrDefault();
        await _db.SaveChangesAsync();

        // Log de sync
        _db.SincronizacionLogs.Add(new SincronizacionLog
        {
            EmpresaId = EmpresaId,
            OperadorId = OperadorId,
            DeviceId = DeviceId,
            Tipo = TipoSync.Download,
            RegistrosRecibidos = asignaciones.Count,
            IpOrigen = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();

        return Ok(new
        {
            timestamp = DateTimeOffset.UtcNow,
            operador = new { operador.Id, operador.Nombre, operador.Apellido, operador.CodigoOperador },
            asignaciones,
            catalogos,
            total_asignaciones = asignaciones.Count
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /sync/upload  — subir inspecciones desde el dispositivo
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromBody] SyncUploadRequest req)
    {
        var errores = new List<object>();
        int procesados = 0;

        foreach (var inspReq in req.Inspecciones)
        {
            try
            {
                // Verificar que la asignación pertenece al operador
                var asignacion = await _db.AsignacionesInspeccion.FirstOrDefaultAsync(a =>
                    a.Id == inspReq.AsignacionId && a.OperadorId == OperadorId);

                if (asignacion == null)
                {
                    errores.Add(new { asignacion_id = inspReq.AsignacionId, error = "Asignación no encontrada" });
                    continue;
                }

                // Buscar inspección existente o crear nueva
                var inspeccion = await _db.Inspecciones
                    .Include(i => i.Respuestas)
                    .FirstOrDefaultAsync(i => i.AsignacionId == asignacion.Id);

                if (inspeccion == null)
                {
                    inspeccion = new Inspeccion
                    {
                        EmpresaId = EmpresaId,
                        AsignacionId = asignacion.Id,
                        OperadorId = OperadorId,
                        ServicioInspeccionId = asignacion.ServicioInspeccionId,
                        FlujoVersionId = asignacion.FlujoVersionId,
                        DeviceId = DeviceId,
                        AppVersion = inspReq.AppVersion
                    };
                    _db.Inspecciones.Add(inspeccion);
                }

                // Actualizar datos
                inspeccion.Estado = Enum.Parse<EstadoInspeccion>(inspReq.Estado, true);
                inspeccion.FechaInicio = inspReq.FechaInicio;
                inspeccion.FechaFin = inspReq.FechaFin;
                inspeccion.CoordXInicio = inspReq.CoordXInicio;
                inspeccion.CoordYInicio = inspReq.CoordYInicio;
                inspeccion.CoordXFin = inspReq.CoordXFin;
                inspeccion.CoordYFin = inspReq.CoordYFin;
                inspeccion.SincronizadoEn = DateTimeOffset.UtcNow;

                // Guardar respuestas (upsert por pregunta_id)
                foreach (var respReq in inspReq.Respuestas)
                {
                    var respExistente = inspeccion.Respuestas
                        .FirstOrDefault(r => r.PreguntaId == respReq.PreguntaId);

                    if (respExistente != null)
                    {
                        // Actualizar respuesta existente
                        ActualizarRespuesta(respExistente, respReq);
                    }
                    else
                    {
                        var nuevaResp = new InspeccionRespuesta
                        {
                            InspeccionId = inspeccion.Id,
                            PreguntaId = respReq.PreguntaId,
                            TipoControl = Enum.Parse<TipoControl>(respReq.TipoControl, true)
                        };
                        ActualizarRespuesta(nuevaResp, respReq);
                        _db.InspeccionRespuestas.Add(nuevaResp);
                    }
                }

                // Actualizar estado de asignación
                if (inspeccion.Estado == EstadoInspeccion.Completada ||
                    inspeccion.Estado == EstadoInspeccion.Enviada)
                {
                    asignacion.Estado = EstadoAsignacion.Finalizada;
                    asignacion.FechaFinalizacion = DateTimeOffset.UtcNow;
                }
                else if (inspeccion.Estado == EstadoInspeccion.EnProgreso)
                {
                    asignacion.Estado = EstadoAsignacion.EnEjecucion;
                }

                // Historial
                _db.InspeccionHistoriales.Add(new InspeccionHistorial
                {
                    InspeccionId = inspeccion.Id,
                    OperadorId = OperadorId,
                    Accion = "sync_upload",
                    EstadoNuevo = inspeccion.Estado
                });

                await _db.SaveChangesAsync();
                procesados++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando inspección {AsignacionId}", inspReq.AsignacionId);
                errores.Add(new { asignacion_id = inspReq.AsignacionId, error = ex.Message });
            }
        }

        // Log de sync
        _db.SincronizacionLogs.Add(new SincronizacionLog
        {
            EmpresaId = EmpresaId,
            OperadorId = OperadorId,
            DeviceId = DeviceId,
            Tipo = TipoSync.Upload,
            RegistrosEnviados = req.Inspecciones.Count,
            Exitoso = !errores.Any()
        });
        await _db.SaveChangesAsync();

        return Ok(new
        {
            procesados,
            errores_count = errores.Count,
            errores,
            timestamp = DateTimeOffset.UtcNow
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /sync/photos  — subir fotografías en multipart
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost("photos")]
    [RequestSizeLimit(100_000_000)] // 100 MB por request
    public async Task<IActionResult> UploadPhotos(
        [FromForm] Guid inspeccionId,
        [FromForm] Guid? preguntaId,
        [FromForm] decimal? coordX,
        [FromForm] decimal? coordY,
        IFormFileCollection fotos)
    {
        if (fotos == null || !fotos.Any())
            return BadRequest(new { error = "No se enviaron fotos" });

        // Verificar que la inspección pertenece al operador
        var inspeccion = await _db.Inspecciones.FirstOrDefaultAsync(i =>
            i.Id == inspeccionId && i.OperadorId == OperadorId);

        if (inspeccion == null) return NotFound(new { error = "Inspección no encontrada" });

        var uploadPath = Path.Combine(_config["Storage:UploadPath"] ?? "uploads", "fotos",
            EmpresaId.ToString(), inspeccionId.ToString());
        Directory.CreateDirectory(uploadPath);

        var fotosGuardadas = new List<object>();
        var maxMb = int.Parse(_config["Storage:MaxPhotoMb"] ?? "10");

        foreach (var foto in fotos)
        {
            if (foto.Length > maxMb * 1_000_000)
            {
                fotosGuardadas.Add(new { nombre = foto.FileName, error = $"Supera {maxMb} MB" });
                continue;
            }

            var extension = Path.GetExtension(foto.FileName).ToLower();
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
            {
                fotosGuardadas.Add(new { nombre = foto.FileName, error = "Formato no permitido" });
                continue;
            }

            // Calcular hash SHA256 para deduplicación
            string hash;
            await using (var ms = new MemoryStream())
            {
                await foto.CopyToAsync(ms);
                hash = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(ms.ToArray())).ToLower();
            }

            // Verificar duplicado
            var existe = await _db.InspeccionFotografias
                .AnyAsync(f => f.HashSha256 == hash && f.InspeccionId == inspeccionId);

            if (existe)
            {
                fotosGuardadas.Add(new { nombre = foto.FileName, hash, duplicado = true });
                continue;
            }

            // Guardar archivo
            var nombreArchivo = $"{Guid.NewGuid()}{extension}";
            var rutaCompleta = Path.Combine(uploadPath, nombreArchivo);

            await using (var stream = foto.OpenReadStream())
            await using (var fileStream = System.IO.File.Create(rutaCompleta))
                await stream.CopyToAsync(fileStream);

            var fotografia = new InspeccionFotografia
            {
                InspeccionId = inspeccionId,
                PreguntaId = preguntaId,
                NombreArchivo = nombreArchivo,
                RutaAlmacenamiento = rutaCompleta,
                TamanioBytes = (int)foto.Length,
                CoordenadaX = coordX,
                CoordenadaY = coordY,
                HashSha256 = hash,
                Formato = extension.TrimStart('.'),
                Orden = await _db.InspeccionFotografias.CountAsync(f => f.InspeccionId == inspeccionId)
            };

            _db.InspeccionFotografias.Add(fotografia);
            fotosGuardadas.Add(new { nombre = foto.FileName, id = fotografia.Id, hash });
        }

        // Actualizar contador en inspección
        inspeccion.TotalFotografias = await _db.InspeccionFotografias
            .CountAsync(f => f.InspeccionId == inspeccionId);

        await _db.SaveChangesAsync();

        return Ok(new { fotos_procesadas = fotosGuardadas.Count, detalle = fotosGuardadas });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /sync/status/{operadorId}
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("status/{operadorId:guid}")]
    public async Task<IActionResult> Status(Guid operadorId)
    {
        if (operadorId != OperadorId) return Forbid();

        var stats = await _db.AsignacionesInspeccion
            .Where(a => a.OperadorId == operadorId && a.EmpresaId == EmpresaId && a.DeletedAt == null)
            .GroupBy(a => a.Estado)
            .Select(g => new { estado = g.Key.ToString().ToLower(), total = g.Count() })
            .ToListAsync();

        var ultimaSync = await _db.SincronizacionLogs
            .Where(s => s.OperadorId == operadorId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            operador_id = operadorId,
            estadisticas = stats,
            ultima_sync = ultimaSync?.CreatedAt,
            timestamp_servidor = DateTimeOffset.UtcNow
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────
    private static void ActualizarRespuesta(InspeccionRespuesta resp, RespuestaUploadItem req)
    {
        resp.ValorTexto = req.ValorTexto;
        resp.ValorBooleano = req.ValorBooleano;
        resp.ValorJson = req.ValorJson;

        if (req.ValorEntero.HasValue) resp.ValorEntero = req.ValorEntero;
        if (req.ValorDecimal.HasValue) resp.ValorDecimal = req.ValorDecimal;
        if (req.ValorFecha.HasValue) resp.ValorFecha = DateOnly.FromDateTime(req.ValorFecha.Value.DateTime);
        if (req.ValorFechaHora.HasValue) resp.ValorFechaHora = req.ValorFechaHora;
        resp.RespondidaEn = DateTimeOffset.UtcNow;
    }
}

// DTOs de Sync
public record SyncUploadRequest(List<InspeccionUploadItem> Inspecciones);

public record InspeccionUploadItem(
    Guid AsignacionId,
    string Estado,
    DateTimeOffset? FechaInicio,
    DateTimeOffset? FechaFin,
    decimal? CoordXInicio, decimal? CoordYInicio,
    decimal? CoordXFin, decimal? CoordYFin,
    string? AppVersion,
    List<RespuestaUploadItem> Respuestas);

public record RespuestaUploadItem(
    Guid PreguntaId,
    string TipoControl,
    string? ValorTexto,
    long? ValorEntero,
    decimal? ValorDecimal,
    DateTimeOffset? ValorFecha,
    DateTimeOffset? ValorFechaHora,
    bool? ValorBooleano,
    string? ValorJson);

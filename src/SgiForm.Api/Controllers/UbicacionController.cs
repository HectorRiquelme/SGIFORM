using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SgiForm.Domain.Enums;
using SgiForm.Infrastructure.Persistence;

namespace SgiForm.Api.Controllers;

/// <summary>
/// Endpoints de georreferencia: ubicación de operadores y servicios en el mapa.
/// </summary>
[ApiController]
[Route("api/v1/ubicacion")]
[Authorize]
public class UbicacionController : ControllerBase
{
    private readonly AppDbContext _db;
    public UbicacionController(AppDbContext db) => _db = db;
    private Guid EmpresaId => Guid.Parse(User.FindFirst("empresa_id")!.Value);

    // ── GET /ubicacion/operadores ─────────────────────────────────────────────
    /// <summary>
    /// Retorna la última ubicación conocida de cada operador activo,
    /// derivada de la inspección más reciente que tenga coordenadas.
    /// </summary>
    [HttpGet("operadores")]
    public async Task<IActionResult> GetUbicacionOperadores(
        [FromQuery] Guid? zonaId,
        [FromQuery] string? localidad,
        [FromQuery] Guid? operadorId)
    {
        var q = _db.Operadores
            .Where(o => o.EmpresaId == EmpresaId && o.Activo && o.DeletedAt == null);

        if (operadorId.HasValue) q = q.Where(o => o.Id == operadorId.Value);
        if (!string.IsNullOrEmpty(localidad)) q = q.Where(o => o.Localidad == localidad);

        var operadores = await q
            .Select(o => new { o.Id, o.CodigoOperador, o.Nombre, o.Apellido, o.Zona, o.Localidad })
            .ToListAsync();

        var opIds = operadores.Select(o => o.Id).ToList();

        // Última inspección con coordenadas por operador
        var ultimasInsp = await _db.Inspecciones
            .Where(i => i.EmpresaId == EmpresaId && opIds.Contains(i.OperadorId)
                     && (i.CoordXFin != null || i.CoordXInicio != null))
            .GroupBy(i => i.OperadorId)
            .Select(g => g.OrderByDescending(i => i.UpdatedAt).First())
            .ToListAsync();

        var result = operadores.Select(o =>
        {
            var insp = ultimasInsp.FirstOrDefault(i => i.OperadorId == o.Id);
            var lat = insp?.CoordYFin ?? insp?.CoordYInicio;
            var lng = insp?.CoordXFin ?? insp?.CoordXInicio;
            return new
            {
                o.Id, o.CodigoOperador,
                nombre = o.Nombre + " " + o.Apellido,
                o.Zona, o.Localidad,
                lat, lng,
                tiene_ubicacion = lat.HasValue && lng.HasValue,
                ultima_actividad = insp?.UpdatedAt
            };
        }).ToList();

        return Ok(result);
    }

    // ── GET /ubicacion/operador/{id} ──────────────────────────────────────────
    /// <summary>
    /// Retorna la última ubicación conocida de un operador específico.
    /// Usado para polling periódico desde el cliente.
    /// </summary>
    [HttpGet("operador/{id:guid}")]
    public async Task<IActionResult> GetUbicacionOperador(Guid id)
    {
        var operador = await _db.Operadores
            .Where(o => o.Id == id && o.EmpresaId == EmpresaId && o.DeletedAt == null)
            .Select(o => new { o.Id, o.CodigoOperador, o.Nombre, o.Apellido, o.Zona, o.Localidad })
            .FirstOrDefaultAsync();

        if (operador == null) return NotFound();

        var insp = await _db.Inspecciones
            .Where(i => i.OperadorId == id && i.EmpresaId == EmpresaId
                     && (i.CoordXFin != null || i.CoordXInicio != null))
            .OrderByDescending(i => i.UpdatedAt)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            operador.Id, operador.CodigoOperador,
            nombre = operador.Nombre + " " + operador.Apellido,
            operador.Zona, operador.Localidad,
            lat = insp?.CoordYFin ?? insp?.CoordYInicio,
            lng = insp?.CoordXFin ?? insp?.CoordXInicio,
            tiene_ubicacion = insp != null && (insp.CoordXFin ?? insp.CoordXInicio) != null,
            ultima_actividad = insp?.UpdatedAt
        });
    }

    // ── GET /ubicacion/servicios ──────────────────────────────────────────────
    /// <summary>
    /// Retorna servicios con coordenadas para mostrar en el mapa.
    /// </summary>
    [HttpGet("servicios")]
    public async Task<IActionResult> GetServiciosGeo(
        [FromQuery] string? localidad,
        [FromQuery] string? ruta,
        [FromQuery] bool? conAsignacion,
        [FromQuery] int limite = 500)
    {
        var q = _db.ServiciosInspeccion
            .Where(s => s.EmpresaId == EmpresaId && s.Activo
                     && s.CoordenadaY != null && s.CoordenadaX != null);

        if (!string.IsNullOrEmpty(localidad)) q = q.Where(s => s.Localidad == localidad);
        if (!string.IsNullOrEmpty(ruta)) q = q.Where(s => s.Ruta == ruta);
        if (conAsignacion.HasValue) q = q.Where(s => s.TieneAsignacion == conAsignacion.Value);

        var items = await q
            .Take(limite)
            .Select(s => new
            {
                s.Id, s.IdServicio, s.NombreCliente, s.Direccion,
                s.Localidad, s.Ruta,
                lat = s.CoordenadaY, lng = s.CoordenadaX,
                s.TieneAsignacion
            })
            .ToListAsync();

        return Ok(items);
    }

    // ── GET /ubicacion/inspecciones-hoy ──────────────────────────────────────
    /// <summary>
    /// Retorna las inspecciones del día con coordenadas (para histórica del día).
    /// </summary>
    [HttpGet("inspecciones-hoy")]
    public async Task<IActionResult> GetInspeccionesHoy(
        [FromQuery] Guid? operadorId,
        [FromQuery] string? localidad)
    {
        // "Hoy" en el timezone local del servidor (Chile) convertido a UTC.
        // Npgsql 8+ exige que los DateTimeOffset enviados a columnas
        // `timestamp with time zone` tengan offset +00:00 (UTC). Usar
        // DateTimeOffset.UtcNow.Date devuelve un DateTime Kind=Unspecified
        // que EF/Npgsql interpreta con el offset local del server
        // (-04:00 en Chile) y la query falla con ArgumentException.
        var nowLocal = DateTimeOffset.Now;
        var hoy = new DateTimeOffset(nowLocal.Date, nowLocal.Offset).ToUniversalTime();
        var q = _db.Inspecciones
            .Where(i => i.EmpresaId == EmpresaId
                     && i.CreatedAt >= hoy
                     && (i.CoordXFin != null || i.CoordXInicio != null));

        if (operadorId.HasValue) q = q.Where(i => i.OperadorId == operadorId.Value);
        if (!string.IsNullOrEmpty(localidad))
            q = q.Where(i => i.ServicioInspeccion.Localidad == localidad);

        var items = await q
            .OrderByDescending(i => i.UpdatedAt)
            .Take(200)
            .Select(i => new
            {
                i.Id,
                operador = i.Operador.Nombre + " " + i.Operador.Apellido,
                i.Estado,
                servicio = i.ServicioInspeccion.IdServicio,
                lat = i.CoordYFin ?? i.CoordYInicio,
                lng = i.CoordXFin ?? i.CoordXInicio,
                i.FechaInicio, i.FechaFin
            })
            .ToListAsync();

        return Ok(items);
    }
}

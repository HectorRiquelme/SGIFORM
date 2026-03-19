using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SanitasField.Domain.Entities;
using SanitasField.Domain.Enums;
using SanitasField.Infrastructure.Persistence;

namespace SanitasField.Api.Controllers;

[ApiController]
[Route("api/v1/asignaciones")]
[Authorize]
public class AsignacionController : ControllerBase
{
    private readonly AppDbContext _db;

    public AsignacionController(AppDbContext db) => _db = db;

    private Guid EmpresaId =>
        Guid.Parse(User.FindFirst("empresa_id")!.Value);

    private Guid UsuarioId =>
        Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    // ──────────────────────────────────────────────────────────────────────────
    // GET /asignaciones
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? operadorId,
        [FromQuery] string? estado,
        [FromQuery] string? localidad,
        [FromQuery] string? ruta,
        [FromQuery] string? lote,
        [FromQuery] Guid? tipoInspeccionId,
        [FromQuery] int pagina = 1,
        [FromQuery] int porPagina = 25)
    {
        var q = _db.AsignacionesInspeccion
            .Include(a => a.ServicioInspeccion)
            .Include(a => a.Operador)
            .Include(a => a.TipoInspeccion)
            .Where(a => a.EmpresaId == EmpresaId && a.DeletedAt == null);

        if (operadorId.HasValue)
            q = q.Where(a => a.OperadorId == operadorId.Value);

        if (!string.IsNullOrWhiteSpace(estado) &&
            Enum.TryParse<EstadoAsignacion>(estado, true, out var estadoEnum))
            q = q.Where(a => a.Estado == estadoEnum);

        if (!string.IsNullOrWhiteSpace(localidad))
            q = q.Where(a => a.ServicioInspeccion.Localidad == localidad);

        if (!string.IsNullOrWhiteSpace(ruta))
            q = q.Where(a => a.ServicioInspeccion.Ruta == ruta);

        if (!string.IsNullOrWhiteSpace(lote))
            q = q.Where(a => a.ServicioInspeccion.Lote == lote);

        if (tipoInspeccionId.HasValue)
            q = q.Where(a => a.TipoInspeccionId == tipoInspeccionId.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(a => a.FechaAsignacion)
            .Skip((pagina - 1) * porPagina)
            .Take(porPagina)
            .Select(a => new
            {
                a.Id,
                estado = a.Estado.ToString().ToLower(),
                prioridad = a.Prioridad.ToString().ToLower(),
                a.FechaAsignacion,
                a.FechaFinalizacion,
                servicio = new
                {
                    a.ServicioInspeccion.Id,
                    a.ServicioInspeccion.IdServicio,
                    a.ServicioInspeccion.NumeroMedidor,
                    a.ServicioInspeccion.Direccion,
                    a.ServicioInspeccion.Localidad,
                    a.ServicioInspeccion.Ruta
                },
                operador = new
                {
                    a.Operador.Id,
                    nombre = a.Operador.Nombre + " " + a.Operador.Apellido,
                    a.Operador.CodigoOperador
                },
                tipo_inspeccion = new { a.TipoInspeccion.Id, a.TipoInspeccion.Nombre }
            })
            .ToListAsync();

        return Ok(new { total, pagina, por_pagina = porPagina, items });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /asignaciones/por-operador/{operadorId}  (para app móvil)
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("por-operador/{operadorId:guid}")]
    public async Task<IActionResult> GetByOperador(
        Guid operadorId,
        [FromQuery] DateTimeOffset? desde)
    {
        // Verificar que el operador pertenece a la empresa
        var opExiste = await _db.Operadores.AnyAsync(o =>
            o.Id == operadorId && o.EmpresaId == EmpresaId && o.Activo);
        if (!opExiste) return NotFound();

        var q = _db.AsignacionesInspeccion
            .Include(a => a.ServicioInspeccion)
            .Include(a => a.TipoInspeccion)
            .Include(a => a.FlujoVersion)
                .ThenInclude(v => v.Secciones.OrderBy(s => s.Orden))
                    .ThenInclude(s => s.Preguntas.OrderBy(p => p.Orden))
                        .ThenInclude(p => p.Opciones.Where(o => o.Activo))
            .Include(a => a.FlujoVersion)
                .ThenInclude(v => v.Reglas.Where(r => r.Activo))
            .Where(a => a.OperadorId == operadorId
                     && a.EmpresaId == EmpresaId
                     && a.DeletedAt == null
                     && a.Estado != EstadoAsignacion.Cerrada
                     && a.Estado != EstadoAsignacion.Rechazada);

        if (desde.HasValue)
            q = q.Where(a => a.UpdatedAt >= desde.Value);

        var items = await q
            .OrderBy(a => a.Prioridad)
            .ThenBy(a => a.FechaAsignacion)
            .ToListAsync();

        return Ok(items);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /asignaciones — asignación manual
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Create([FromBody] CreateAsignacionRequest req)
    {
        // Validar que el servicio y operador pertenecen a la empresa
        var servicio = await _db.ServiciosInspeccion.FirstOrDefaultAsync(s =>
            s.Id == req.ServicioInspeccionId && s.EmpresaId == EmpresaId && s.Activo);
        if (servicio == null)
            return BadRequest(new { error = "Servicio no encontrado" });

        var operador = await _db.Operadores.FirstOrDefaultAsync(o =>
            o.Id == req.OperadorId && o.EmpresaId == EmpresaId && o.Activo && o.DeletedAt == null);
        if (operador == null)
            return BadRequest(new { error = "Operador no encontrado" });

        var flujoVersion = await _db.FlujoVersiones.FirstOrDefaultAsync(v =>
            v.Id == req.FlujoVersionId && v.Estado == EstadoFlujoVersion.Publicado);
        if (flujoVersion == null)
            return BadRequest(new { error = "Versión de flujo no encontrada o no publicada" });

        var asignacion = new AsignacionInspeccion
        {
            EmpresaId = EmpresaId,
            ServicioInspeccionId = req.ServicioInspeccionId,
            OperadorId = req.OperadorId,
            TipoInspeccionId = req.TipoInspeccionId,
            FlujoVersionId = req.FlujoVersionId,
            Prioridad = Enum.TryParse<Prioridad>(req.Prioridad, true, out var p) ? p : Prioridad.Normal,
            Observaciones = req.Observaciones,
            AsignadoPor = UsuarioId,
            FechaInicioEsperada = req.FechaInicioEsperada,
            FechaFinEsperada = req.FechaFinEsperada
        };

        _db.AsignacionesInspeccion.Add(asignacion);

        // Marcar servicio con asignación
        servicio.TieneAsignacion = true;

        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { }, new { asignacion.Id });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /asignaciones/masiva — asignación masiva por filtros
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost("masiva")]
    [Authorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> AsignacionMasiva([FromBody] AsignacionMasivaRequest req)
    {
        var operador = await _db.Operadores.FirstOrDefaultAsync(o =>
            o.Id == req.OperadorId && o.EmpresaId == EmpresaId && o.Activo);
        if (operador == null)
            return BadRequest(new { error = "Operador no encontrado" });

        var flujoVersion = await _db.FlujoVersiones.FirstOrDefaultAsync(v =>
            v.Id == req.FlujoVersionId && v.Estado == EstadoFlujoVersion.Publicado);
        if (flujoVersion == null)
            return BadRequest(new { error = "Versión de flujo no publicada" });

        // Filtrar servicios sin asignación activa
        var q = _db.ServiciosInspeccion
            .Where(s => s.EmpresaId == EmpresaId && s.Activo && !s.TieneAsignacion);

        if (!string.IsNullOrWhiteSpace(req.Localidad))
            q = q.Where(s => s.Localidad == req.Localidad);
        if (!string.IsNullOrWhiteSpace(req.Ruta))
            q = q.Where(s => s.Ruta == req.Ruta);
        if (!string.IsNullOrWhiteSpace(req.Lote))
            q = q.Where(s => s.Lote == req.Lote);

        var servicios = await q.Take(req.LimiteMaximo ?? 500).ToListAsync();

        if (!servicios.Any())
            return Ok(new { asignadas = 0, mensaje = "No se encontraron servicios disponibles con los filtros especificados" });

        var asignaciones = servicios.Select(s => new AsignacionInspeccion
        {
            EmpresaId = EmpresaId,
            ServicioInspeccionId = s.Id,
            OperadorId = req.OperadorId,
            TipoInspeccionId = req.TipoInspeccionId,
            FlujoVersionId = req.FlujoVersionId,
            Prioridad = Prioridad.Normal,
            AsignadoPor = UsuarioId
        }).ToList();

        await _db.AsignacionesInspeccion.AddRangeAsync(asignaciones);

        // Marcar servicios
        foreach (var s in servicios) s.TieneAsignacion = true;

        await _db.SaveChangesAsync();

        return Ok(new { asignadas = asignaciones.Count });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT /asignaciones/{id}/estado
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPut("{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoRequest req)
    {
        var asignacion = await _db.AsignacionesInspeccion.FirstOrDefaultAsync(a =>
            a.Id == id && a.EmpresaId == EmpresaId && a.DeletedAt == null);

        if (asignacion == null) return NotFound();

        if (!Enum.TryParse<EstadoAsignacion>(req.Estado, true, out var nuevoEstado))
            return BadRequest(new { error = $"Estado inválido: {req.Estado}" });

        asignacion.Estado = nuevoEstado;

        if (nuevoEstado == EstadoAsignacion.Finalizada)
            asignacion.FechaFinalizacion = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }
}

// DTOs
public record CreateAsignacionRequest(
    Guid ServicioInspeccionId, Guid OperadorId,
    Guid TipoInspeccionId, Guid FlujoVersionId,
    string? Prioridad, string? Observaciones,
    DateOnly? FechaInicioEsperada, DateOnly? FechaFinEsperada);

public record AsignacionMasivaRequest(
    Guid OperadorId, Guid TipoInspeccionId, Guid FlujoVersionId,
    string? Localidad, string? Ruta, string? Lote,
    int? LimiteMaximo);

public record CambiarEstadoRequest(string Estado);

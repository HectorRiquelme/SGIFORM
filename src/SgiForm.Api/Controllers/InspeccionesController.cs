using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SgiForm.Domain.Entities;
using SgiForm.Domain.Enums;
using SgiForm.Infrastructure.Persistence;

namespace SgiForm.Api.Controllers;

[ApiController]
[Route("api/v1/inspecciones")]
[Authorize]
public class InspeccionesController : ControllerBase
{
    private readonly AppDbContext _db;
    public InspeccionesController(AppDbContext db) => _db = db;

    private Guid EmpresaId => Guid.Parse(User.FindFirst("empresa_id")!.Value);
    private Guid UsuarioId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? estado,
        [FromQuery] Guid? operadorId,
        [FromQuery] int pagina = 1,
        [FromQuery] int porPagina = 25)
    {
        var q = _db.Inspecciones
            .Include(i => i.Operador)
            .Include(i => i.ServicioInspeccion)
            .Where(i => i.EmpresaId == EmpresaId);

        if (!string.IsNullOrEmpty(estado) && Enum.TryParse<EstadoInspeccion>(estado, true, out var est))
            q = q.Where(i => i.Estado == est);

        if (operadorId.HasValue)
            q = q.Where(i => i.OperadorId == operadorId.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(i => i.CreatedAt)
            .Skip((pagina - 1) * porPagina)
            .Take(porPagina)
            .Select(i => new
            {
                i.Id,
                estado = i.Estado.ToString().ToLower(),
                i.TotalPreguntas, i.TotalRespondidas, i.TotalFotografias,
                coord_x_fin = i.CoordXFin,
                sincronizado_en = i.SincronizadoEn,
                servicio_id_servicio = i.ServicioInspeccion.IdServicio,
                servicio_direccion   = i.ServicioInspeccion.Direccion,
                operador_nombre      = i.Operador.Nombre + " " + i.Operador.Apellido,
                i.FechaInicio, i.FechaFin
            })
            .ToListAsync();

        return Ok(new { total, pagina, por_pagina = porPagina, items });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var i = await _db.Inspecciones
            .Include(x => x.Operador)
            .Include(x => x.ServicioInspeccion)
            .Include(x => x.Respuestas)
            .Include(x => x.Fotografias)
            .Include(x => x.Historial)
            .Where(x => x.Id == id && x.EmpresaId == EmpresaId)
            .FirstOrDefaultAsync();
        if (i == null) return NotFound();
        return Ok(i);
    }

    [HttpPost("{id:guid}/aprobar")]
    [Authorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Aprobar(Guid id, [FromBody] RevisionRequest? req)
    {
        var insp = await _db.Inspecciones.FirstOrDefaultAsync(i => i.Id == id && i.EmpresaId == EmpresaId);
        if (insp == null) return NotFound();

        var anterior = insp.Estado;
        insp.Estado = EstadoInspeccion.Aprobada;
        insp.RevisionPor = UsuarioId;
        insp.RevisionEn = DateTimeOffset.UtcNow;

        _db.InspeccionHistoriales.Add(new InspeccionHistorial
        {
            InspeccionId = id, UsuarioId = UsuarioId,
            Accion = "aprobar", EstadoAnterior = anterior,
            EstadoNuevo = EstadoInspeccion.Aprobada
        });

        await _db.SaveChangesAsync();
        return Ok(new { mensaje = "Inspección aprobada" });
    }

    [HttpPost("{id:guid}/observar")]
    [Authorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Observar(Guid id, [FromBody] RevisionRequest req)
    {
        var insp = await _db.Inspecciones.FirstOrDefaultAsync(i => i.Id == id && i.EmpresaId == EmpresaId);
        if (insp == null) return NotFound();

        var anterior = insp.Estado;
        insp.Estado = EstadoInspeccion.Observada;
        insp.RevisionPor = UsuarioId;
        insp.RevisionEn = DateTimeOffset.UtcNow;
        insp.RevisionObservacion = req.Observacion;

        _db.InspeccionHistoriales.Add(new InspeccionHistorial
        {
            InspeccionId = id, UsuarioId = UsuarioId,
            Accion = "observar", EstadoAnterior = anterior,
            EstadoNuevo = EstadoInspeccion.Observada,
            Observacion = req.Observacion
        });

        await _db.SaveChangesAsync();
        return Ok(new { mensaje = "Inspección observada" });
    }

    [HttpPost("{id:guid}/rechazar")]
    [Authorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Rechazar(Guid id, [FromBody] RevisionRequest req)
    {
        var insp = await _db.Inspecciones.FirstOrDefaultAsync(i => i.Id == id && i.EmpresaId == EmpresaId);
        if (insp == null) return NotFound();

        var anterior = insp.Estado;
        insp.Estado = EstadoInspeccion.Rechazada;
        insp.RevisionPor = UsuarioId;
        insp.RevisionEn = DateTimeOffset.UtcNow;
        insp.RevisionObservacion = req.Observacion;

        _db.InspeccionHistoriales.Add(new InspeccionHistorial
        {
            InspeccionId = id, UsuarioId = UsuarioId,
            Accion = "rechazar", EstadoAnterior = anterior,
            EstadoNuevo = EstadoInspeccion.Rechazada,
            Observacion = req.Observacion
        });

        await _db.SaveChangesAsync();
        return Ok(new { mensaje = "Inspección rechazada" });
    }
}

public record RevisionRequest(string? Observacion);

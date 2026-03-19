using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SanitasField.Domain.Enums;
using SanitasField.Infrastructure.Persistence;

namespace SanitasField.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db) => _db = db;

    private Guid EmpresaId =>
        Guid.Parse(User.FindFirst("empresa_id")!.Value);

    // ──────────────────────────────────────────────────────────────────────────
    // GET /dashboard/resumen
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen()
    {
        var empresa = EmpresaId;

        var totalServicios = await _db.ServiciosInspeccion
            .CountAsync(s => s.EmpresaId == empresa && s.Activo);

        var asignacionesPorEstado = await _db.AsignacionesInspeccion
            .Where(a => a.EmpresaId == empresa && a.DeletedAt == null)
            .GroupBy(a => a.Estado)
            .Select(g => new { estado = g.Key.ToString().ToLower(), total = g.Count() })
            .ToListAsync();

        var inspeccionesPorEstado = await _db.Inspecciones
            .Where(i => i.EmpresaId == empresa)
            .GroupBy(i => i.Estado)
            .Select(g => new { estado = g.Key.ToString().ToLower(), total = g.Count() })
            .ToListAsync();

        var totalFotografias = await _db.InspeccionFotografias
            .CountAsync(f => f.Inspeccion.EmpresaId == empresa);

        var totalConGps = await _db.Inspecciones
            .CountAsync(i => i.EmpresaId == empresa && i.CoordXFin != null);

        var operadoresActivos = await _db.Operadores
            .CountAsync(o => o.EmpresaId == empresa && o.Activo && o.DeletedAt == null);

        return Ok(new
        {
            total_servicios = totalServicios,
            asignaciones = asignacionesPorEstado,
            inspecciones = inspeccionesPorEstado,
            total_fotografias = totalFotografias,
            inspecciones_con_gps = totalConGps,
            operadores_activos = operadoresActivos,
            fecha_actualizacion = DateTimeOffset.UtcNow
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /dashboard/por-operador
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("por-operador")]
    public async Task<IActionResult> PorOperador()
    {
        var data = await _db.Operadores
            .Where(o => o.EmpresaId == EmpresaId && o.Activo && o.DeletedAt == null)
            .Select(o => new
            {
                operador_id = o.Id,
                nombre = o.Nombre + " " + o.Apellido,
                o.CodigoOperador,
                o.Zona,
                pendientes = o.Asignaciones.Count(a => a.Estado == EstadoAsignacion.Pendiente && a.DeletedAt == null),
                en_ejecucion = o.Asignaciones.Count(a => a.Estado == EstadoAsignacion.EnEjecucion && a.DeletedAt == null),
                finalizadas = o.Asignaciones.Count(a => a.Estado == EstadoAsignacion.Finalizada && a.DeletedAt == null),
                sincronizadas = o.Asignaciones.Count(a => a.Estado == EstadoAsignacion.Sincronizada && a.DeletedAt == null),
                total = o.Asignaciones.Count(a => a.DeletedAt == null),
                ultima_sync = o.FechaUltimaSync
            })
            .OrderBy(o => o.nombre)
            .ToListAsync();

        return Ok(data);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /dashboard/por-localidad
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("por-localidad")]
    public async Task<IActionResult> PorLocalidad()
    {
        var data = await _db.ServiciosInspeccion
            .Where(s => s.EmpresaId == EmpresaId && s.Activo)
            .GroupBy(s => s.Localidad)
            .Select(g => new
            {
                localidad = g.Key ?? "Sin localidad",
                total_servicios = g.Count(),
                con_asignacion = g.Count(s => s.TieneAsignacion)
            })
            .OrderBy(g => g.localidad)
            .ToListAsync();

        return Ok(data);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /dashboard/por-ruta
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("por-ruta")]
    public async Task<IActionResult> PorRuta([FromQuery] string? localidad)
    {
        var q = _db.ServiciosInspeccion
            .Where(s => s.EmpresaId == EmpresaId && s.Activo);

        if (!string.IsNullOrWhiteSpace(localidad))
            q = q.Where(s => s.Localidad == localidad);

        var data = await q
            .GroupBy(s => s.Ruta)
            .Select(g => new
            {
                ruta = g.Key ?? "Sin ruta",
                total = g.Count(),
                con_asignacion = g.Count(s => s.TieneAsignacion)
            })
            .OrderBy(g => g.ruta)
            .ToListAsync();

        return Ok(data);
    }
}

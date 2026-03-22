using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SgiForm.Infrastructure.Persistence;

namespace SgiForm.Api.Controllers;

[ApiController]
[Route("api/v1/servicios")]
[Authorize]
public class ServiciosController : ControllerBase
{
    private readonly AppDbContext _db;
    public ServiciosController(AppDbContext db) => _db = db;
    private Guid EmpresaId => Guid.Parse(User.FindFirst("empresa_id")!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? q,
        [FromQuery] string? localidad,
        [FromQuery] string? ruta,
        [FromQuery] string? lote,
        [FromQuery] bool? conAsignacion,
        [FromQuery] int pagina = 1,
        [FromQuery] int porPagina = 30)
    {
        var query = _db.ServiciosInspeccion
            .Where(s => s.EmpresaId == EmpresaId && s.Activo);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(s =>
                s.IdServicio.Contains(q) ||
                (s.Direccion != null && s.Direccion.Contains(q)) ||
                (s.NombreCliente != null && s.NombreCliente.Contains(q)) ||
                (s.NumeroMedidor != null && s.NumeroMedidor.Contains(q)));

        if (!string.IsNullOrWhiteSpace(localidad))
            query = query.Where(s => s.Localidad == localidad);
        if (!string.IsNullOrWhiteSpace(ruta))
            query = query.Where(s => s.Ruta == ruta);
        if (!string.IsNullOrWhiteSpace(lote))
            query = query.Where(s => s.Lote == lote);
        if (conAsignacion.HasValue)
            query = query.Where(s => s.TieneAsignacion == conAsignacion.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(s => s.Localidad).ThenBy(s => s.Ruta).ThenBy(s => s.IdServicio)
            .Skip((pagina - 1) * porPagina)
            .Take(porPagina)
            .Select(s => new
            {
                s.Id, s.IdServicio, s.NumeroMedidor, s.Marca,
                s.Diametro, s.Direccion, s.NombreCliente,
                s.CoordenadaX, s.CoordenadaY,
                s.Lote, s.Localidad, s.Ruta, s.Libreta,
                s.ObservacionLibre, s.TieneAsignacion
            })
            .ToListAsync();

        return Ok(new { total, pagina, por_pagina = porPagina, items });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var s = await _db.ServiciosInspeccion
            .Where(x => x.Id == id && x.EmpresaId == EmpresaId)
            .FirstOrDefaultAsync();
        if (s == null) return NotFound();
        return Ok(s);
    }
}

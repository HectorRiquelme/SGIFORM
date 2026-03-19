using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SanitasField.Domain.Entities;
using SanitasField.Infrastructure.Persistence;

namespace SanitasField.Api.Controllers;

[ApiController]
[Route("api/v1/tipos-inspeccion")]
[Authorize]
public class TiposInspeccionController : ControllerBase
{
    private readonly AppDbContext _db;
    public TiposInspeccionController(AppDbContext db) => _db = db;

    private Guid EmpresaId =>
        Guid.Parse(User.FindFirst("empresa_id")!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? activo)
    {
        var q = _db.TiposInspeccion
            .Where(t => t.EmpresaId == EmpresaId && t.DeletedAt == null);

        if (activo.HasValue) q = q.Where(t => t.Activo == activo.Value);

        var items = await q
            .OrderBy(t => t.Nombre)
            .Select(t => new
            {
                t.Id, t.Codigo, t.Nombre, t.Descripcion,
                t.Activo, t.Icono, t.Color, t.CreatedAt,
                flujo_version_id_def = t.FlujoVersionIdDef
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var t = await _db.TiposInspeccion
            .Where(x => x.Id == id && x.EmpresaId == EmpresaId && x.DeletedAt == null)
            .Select(x => new
            {
                x.Id, x.Codigo, x.Nombre, x.Descripcion,
                x.Activo, x.Icono, x.Color, x.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (t == null) return NotFound();
        return Ok(t);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] TipoInspeccionRequest req)
    {
        var existe = await _db.TiposInspeccion.AnyAsync(t =>
            t.EmpresaId == EmpresaId && t.Codigo == req.Codigo && t.DeletedAt == null);

        if (existe) return Conflict(new { error = $"Código '{req.Codigo}' ya existe" });

        var tipo = new TipoInspeccion
        {
            EmpresaId = EmpresaId,
            Codigo = req.Codigo,
            Nombre = req.Nombre,
            Descripcion = req.Descripcion,
            Activo = true,
            Icono = req.Icono,
            Color = req.Color
        };

        _db.TiposInspeccion.Add(tipo);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = tipo.Id },
            new { tipo.Id, tipo.Codigo, tipo.Nombre });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] TipoInspeccionRequest req)
    {
        var tipo = await _db.TiposInspeccion.FirstOrDefaultAsync(t =>
            t.Id == id && t.EmpresaId == EmpresaId && t.DeletedAt == null);

        if (tipo == null) return NotFound();

        tipo.Nombre = req.Nombre ?? tipo.Nombre;
        tipo.Descripcion = req.Descripcion ?? tipo.Descripcion;
        tipo.Activo = req.Activo ?? tipo.Activo;
        tipo.Icono = req.Icono ?? tipo.Icono;
        tipo.Color = req.Color ?? tipo.Color;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tipo = await _db.TiposInspeccion.FirstOrDefaultAsync(t =>
            t.Id == id && t.EmpresaId == EmpresaId && t.DeletedAt == null);

        if (tipo == null) return NotFound();

        tipo.DeletedAt = DateTimeOffset.UtcNow;
        tipo.Activo = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record TipoInspeccionRequest(
    string Codigo, string Nombre,
    string? Descripcion, bool? Activo,
    string? Icono, string? Color);

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SgiForm.Domain.Entities;
using SgiForm.Infrastructure.Persistence;

namespace SgiForm.Api.Controllers;

[ApiController]
[Route("api/v1/zonas")]
[Authorize]
public class ZonasController : ControllerBase
{
    private readonly AppDbContext _db;
    public ZonasController(AppDbContext db) => _db = db;
    private Guid EmpresaId => Guid.Parse(User.FindFirst("empresa_id")!.Value);

    // ── GET /zonas ────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? soloActivas)
    {
        var q = _db.Zonas
            .Where(z => z.EmpresaId == EmpresaId && z.DeletedAt == null);
        if (soloActivas == true) q = q.Where(z => z.Activo);

        var items = await q
            .OrderBy(z => z.Nombre)
            .Select(z => new
            {
                z.Id, z.Codigo, z.Nombre, z.Descripcion, z.Activo, z.CreatedAt,
                total_localidades = z.Localidades.Count(l => l.DeletedAt == null)
            })
            .ToListAsync();

        return Ok(items);
    }

    // ── GET /zonas/{id} ───────────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOne(Guid id)
    {
        var z = await _db.Zonas
            .Where(x => x.Id == id && x.EmpresaId == EmpresaId && x.DeletedAt == null)
            .Select(x => new { x.Id, x.Codigo, x.Nombre, x.Descripcion, x.Activo, x.CreatedAt })
            .FirstOrDefaultAsync();
        if (z == null) return NotFound();
        return Ok(z);
    }

    // ── POST /zonas ───────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ZonaRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Codigo) || string.IsNullOrWhiteSpace(req.Nombre))
            return BadRequest(new { error = "Código y nombre son requeridos." });

        var existe = await _db.Zonas.AnyAsync(z =>
            z.EmpresaId == EmpresaId && z.Codigo == req.Codigo.Trim() && z.DeletedAt == null);
        if (existe)
            return Conflict(new { error = $"Ya existe una zona con código '{req.Codigo}'." });

        var zona = new Zona
        {
            EmpresaId = EmpresaId,
            Codigo = req.Codigo.Trim().ToUpper(),
            Nombre = req.Nombre.Trim(),
            Descripcion = req.Descripcion?.Trim(),
            Activo = req.Activo ?? true
        };
        _db.Zonas.Add(zona);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetOne), new { id = zona.Id },
            new { zona.Id, zona.Codigo, zona.Nombre, zona.Descripcion, zona.Activo });
    }

    // ── PUT /zonas/{id} ───────────────────────────────────────────────────────
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ZonaRequest req)
    {
        var zona = await _db.Zonas.FirstOrDefaultAsync(z =>
            z.Id == id && z.EmpresaId == EmpresaId && z.DeletedAt == null);
        if (zona == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(req.Codigo)) zona.Codigo = req.Codigo.Trim().ToUpper();
        if (!string.IsNullOrWhiteSpace(req.Nombre)) zona.Nombre = req.Nombre.Trim();
        zona.Descripcion = req.Descripcion?.Trim();
        if (req.Activo.HasValue) zona.Activo = req.Activo.Value;

        await _db.SaveChangesAsync();
        return Ok(new { zona.Id, zona.Codigo, zona.Nombre, zona.Descripcion, zona.Activo });
    }

    // ── DELETE /zonas/{id} ────────────────────────────────────────────────────
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var zona = await _db.Zonas.FirstOrDefaultAsync(z =>
            z.Id == id && z.EmpresaId == EmpresaId && z.DeletedAt == null);
        if (zona == null) return NotFound();

        zona.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── GET /zonas/{id}/localidades ───────────────────────────────────────────
    [HttpGet("{id:guid}/localidades")]
    public async Task<IActionResult> GetLocalidades(Guid id, [FromQuery] bool? soloActivas)
    {
        var zonaExiste = await _db.Zonas.AnyAsync(z =>
            z.Id == id && z.EmpresaId == EmpresaId && z.DeletedAt == null);
        if (!zonaExiste) return NotFound();

        var q = _db.Localidades.Where(l => l.ZonaId == id && l.DeletedAt == null);
        if (soloActivas == true) q = q.Where(l => l.Activo);

        var items = await q.OrderBy(l => l.Nombre)
            .Select(l => new { l.Id, l.Codigo, l.Nombre, l.Activo, l.ZonaId, l.CreatedAt })
            .ToListAsync();

        return Ok(items);
    }

    // ── GET /zonas/localidades (todas las localidades de la empresa) ──────────
    [HttpGet("localidades")]
    public async Task<IActionResult> GetTodasLocalidades([FromQuery] bool? soloActivas)
    {
        var q = _db.Localidades
            .Where(l => l.EmpresaId == EmpresaId && l.DeletedAt == null);
        if (soloActivas == true) q = q.Where(l => l.Activo);

        var items = await q.OrderBy(l => l.Nombre)
            .Select(l => new { l.Id, l.Codigo, l.Nombre, l.Activo, l.ZonaId })
            .ToListAsync();

        return Ok(items);
    }

    // ── POST /zonas/{id}/localidades ──────────────────────────────────────────
    [HttpPost("{id:guid}/localidades")]
    public async Task<IActionResult> CreateLocalidad(Guid id, [FromBody] LocalidadRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Codigo) || string.IsNullOrWhiteSpace(req.Nombre))
            return BadRequest(new { error = "Código y nombre son requeridos." });

        var zonaExiste = await _db.Zonas.AnyAsync(z =>
            z.Id == id && z.EmpresaId == EmpresaId && z.DeletedAt == null);
        if (!zonaExiste) return NotFound();

        var existe = await _db.Localidades.AnyAsync(l =>
            l.EmpresaId == EmpresaId && l.Codigo == req.Codigo.Trim() && l.DeletedAt == null);
        if (existe)
            return Conflict(new { error = $"Ya existe una localidad con código '{req.Codigo}'." });

        var loc = new Localidad
        {
            EmpresaId = EmpresaId,
            ZonaId = id,
            Codigo = req.Codigo.Trim().ToUpper(),
            Nombre = req.Nombre.Trim(),
            Activo = req.Activo ?? true
        };
        _db.Localidades.Add(loc);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetLocalidades), new { id },
            new { loc.Id, loc.Codigo, loc.Nombre, loc.Activo, loc.ZonaId });
    }

    // ── PUT /zonas/localidades/{locId} ────────────────────────────────────────
    [HttpPut("localidades/{locId:guid}")]
    public async Task<IActionResult> UpdateLocalidad(Guid locId, [FromBody] LocalidadRequest req)
    {
        var loc = await _db.Localidades.FirstOrDefaultAsync(l =>
            l.Id == locId && l.EmpresaId == EmpresaId && l.DeletedAt == null);
        if (loc == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(req.Codigo)) loc.Codigo = req.Codigo.Trim().ToUpper();
        if (!string.IsNullOrWhiteSpace(req.Nombre)) loc.Nombre = req.Nombre.Trim();
        if (req.Activo.HasValue) loc.Activo = req.Activo.Value;

        await _db.SaveChangesAsync();
        return Ok(new { loc.Id, loc.Codigo, loc.Nombre, loc.Activo, loc.ZonaId });
    }

    // ── DELETE /zonas/localidades/{locId} ─────────────────────────────────────
    [HttpDelete("localidades/{locId:guid}")]
    public async Task<IActionResult> DeleteLocalidad(Guid locId)
    {
        var loc = await _db.Localidades.FirstOrDefaultAsync(l =>
            l.Id == locId && l.EmpresaId == EmpresaId && l.DeletedAt == null);
        if (loc == null) return NotFound();

        loc.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record ZonaRequest(string? Codigo, string? Nombre, string? Descripcion, bool? Activo);
public record LocalidadRequest(string? Codigo, string? Nombre, bool? Activo);

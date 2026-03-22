using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SanitasField.Domain.Entities;
using SanitasField.Infrastructure.Persistence;

namespace SanitasField.Api.Controllers;

[ApiController]
[Route("api/v1/operadores")]
[Authorize]
public class OperadoresController : ControllerBase
{
    private readonly AppDbContext _db;

    public OperadoresController(AppDbContext db) => _db = db;

    private Guid EmpresaId =>
        Guid.Parse(User.FindFirst("empresa_id")!.Value);

    // ──────────────────────────────────────────────────────────────────────────
    // GET /operadores
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? buscar,
        [FromQuery] string? zona,
        [FromQuery] string? localidad,
        [FromQuery] bool? activo,
        [FromQuery] int pagina = 1,
        [FromQuery] int porPagina = 25)
    {
        var q = _db.Operadores
            .Where(o => o.EmpresaId == EmpresaId && o.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(buscar))
            q = q.Where(o => o.Nombre.Contains(buscar) || o.Apellido.Contains(buscar)
                           || o.CodigoOperador.Contains(buscar));

        if (!string.IsNullOrWhiteSpace(zona))
            q = q.Where(o => o.Zona == zona);

        if (!string.IsNullOrWhiteSpace(localidad))
            q = q.Where(o => o.Localidad == localidad);

        if (activo.HasValue)
            q = q.Where(o => o.Activo == activo.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderBy(o => o.Nombre)
            .Skip((pagina - 1) * porPagina)
            .Take(porPagina)
            .Select(o => new
            {
                o.Id,
                o.CodigoOperador,
                nombre_completo = o.Nombre + " " + o.Apellido,
                o.Nombre,
                o.Apellido,
                o.Email,
                o.Telefono,
                o.Zona,
                o.Localidad,
                o.Activo,
                o.FechaUltimaSync
            })
            .ToListAsync();

        return Ok(new { total, pagina, por_pagina = porPagina, items });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /operadores/{id}
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var op = await _db.Operadores
            .Where(o => o.Id == id && o.EmpresaId == EmpresaId && o.DeletedAt == null)
            .Select(o => new
            {
                o.Id,
                o.CodigoOperador,
                o.Nombre,
                o.Apellido,
                o.Rut,
                o.Email,
                o.Telefono,
                o.Zona,
                o.Localidad,
                o.Activo,
                o.DeviceIdRegistrado,
                o.AppVersionUltima,
                o.FechaUltimaSync,
                o.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (op == null) return NotFound();
        return Ok(op);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /operadores
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Create([FromBody] CreateOperadorRequest req)
    {
        // Validar longitud mínima de contraseña
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            return BadRequest(new { error = "La contraseña debe tener al menos 6 caracteres" });

        // Validar código único
        var existe = await _db.Operadores.AnyAsync(o =>
            o.EmpresaId == EmpresaId &&
            o.CodigoOperador == req.CodigoOperador &&
            o.DeletedAt == null);

        if (existe)
            return Conflict(new { error = $"Código de operador '{req.CodigoOperador}' ya existe en esta empresa" });

        var operador = new Operador
        {
            EmpresaId = EmpresaId,
            CodigoOperador = req.CodigoOperador,
            Nombre = req.Nombre,
            Apellido = req.Apellido,
            Rut = req.Rut,
            Email = req.Email,
            Telefono = req.Telefono,
            Zona = req.Zona,
            Localidad = req.Localidad,
            Activo = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
        };

        _db.Operadores.Add(operador);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = operador.Id },
            new { operador.Id, operador.CodigoOperador });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT /operadores/{id}
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOperadorRequest req)
    {
        var op = await _db.Operadores.FirstOrDefaultAsync(o =>
            o.Id == id && o.EmpresaId == EmpresaId && o.DeletedAt == null);

        if (op == null) return NotFound();

        op.Nombre = req.Nombre ?? op.Nombre;
        op.Apellido = req.Apellido ?? op.Apellido;
        op.Email = req.Email ?? op.Email;
        op.Telefono = req.Telefono ?? op.Telefono;
        op.Zona = req.Zona ?? op.Zona;
        op.Localidad = req.Localidad ?? op.Localidad;
        op.Activo = req.Activo ?? op.Activo;

        if (!string.IsNullOrWhiteSpace(req.NuevoPassword))
            op.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NuevoPassword);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DELETE /operadores/{id}  (soft delete)
    // ──────────────────────────────────────────────────────────────────────────
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var op = await _db.Operadores.FirstOrDefaultAsync(o =>
            o.Id == id && o.EmpresaId == EmpresaId && o.DeletedAt == null);

        if (op == null) return NotFound();

        op.DeletedAt = DateTimeOffset.UtcNow;
        op.Activo = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}

public record CreateOperadorRequest(
    string CodigoOperador,
    string Nombre,
    string Apellido,
    string Password,
    string? Rut,
    string? Email,
    string? Telefono,
    string? Zona,
    string? Localidad);

public record UpdateOperadorRequest(
    string? Nombre,
    string? Apellido,
    string? Email,
    string? Telefono,
    string? Zona,
    string? Localidad,
    bool? Activo,
    string? NuevoPassword);

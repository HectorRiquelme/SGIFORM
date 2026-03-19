using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SanitasField.Domain.Entities;
using SanitasField.Domain.Enums;
using SanitasField.Infrastructure.Persistence;

namespace SanitasField.Api.Controllers;

[ApiController]
[Route("api/v1/usuarios")]
[Authorize(Roles = "admin")]
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _db;
    public UsuariosController(AppDbContext db) => _db = db;
    private Guid EmpresaId => Guid.Parse(User.FindFirst("empresa_id")!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int pagina = 1, [FromQuery] int porPagina = 25)
    {
        var q = _db.Usuarios
            .Include(u => u.Rol)
            .Where(u => u.EmpresaId == EmpresaId && u.DeletedAt == null);

        var total = await q.CountAsync();
        var items = await q
            .OrderBy(u => u.Nombre)
            .Skip((pagina - 1) * porPagina).Take(porPagina)
            .Select(u => new
            {
                u.Id, u.Email, u.Nombre, u.Apellido, u.Telefono,
                nombre_completo = u.Nombre + " " + u.Apellido,
                estado = u.Estado.ToString().ToLower(),
                rol = new { u.Rol.Id, u.Rol.Nombre, u.Rol.Codigo },
                u.UltimoAcceso, u.CreatedAt
            })
            .ToListAsync();

        return Ok(new { total, pagina, por_pagina = porPagina, items });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var u = await _db.Usuarios.Include(x => x.Rol)
            .Where(x => x.Id == id && x.EmpresaId == EmpresaId && x.DeletedAt == null)
            .FirstOrDefaultAsync();
        if (u == null) return NotFound();
        return Ok(new
        {
            u.Id, u.Email, u.Nombre, u.Apellido, u.Telefono,
            estado = u.Estado.ToString().ToLower(),
            rol_id = u.RolId, rol_nombre = u.Rol.Nombre,
            u.UltimoAcceso, u.CreatedAt
        });
    }

    [HttpGet("roles")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRoles()
    {
        // Devuelve los roles disponibles para esta empresa (para los dropdowns)
        var roles = await _db.Roles
            .Where(r => r.EmpresaId == EmpresaId && r.Activo)
            .OrderBy(r => r.Nombre)
            .Select(r => new { r.Id, r.Nombre, r.Codigo, r.Descripcion })
            .ToListAsync();
        return Ok(roles);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUsuarioRequest req)
    {
        if (await _db.Usuarios.AnyAsync(u => u.Email == req.Email.ToLower()))
            return Conflict(new { error = $"Email '{req.Email}' ya está registrado" });

        var rolExiste = await _db.Roles.AnyAsync(r => r.Id == req.RolId && r.EmpresaId == EmpresaId);
        if (!rolExiste) return BadRequest(new { error = "Rol no encontrado" });

        var usuario = new Usuario
        {
            EmpresaId = EmpresaId,
            RolId = req.RolId,
            Email = req.Email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Nombre = req.Nombre,
            Apellido = req.Apellido,
            Telefono = req.Telefono,
            Estado = EstadoUsuario.Activo
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = usuario.Id },
            new { usuario.Id, usuario.Email });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUsuarioRequest req)
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x =>
            x.Id == id && x.EmpresaId == EmpresaId && x.DeletedAt == null);
        if (u == null) return NotFound();

        u.Nombre = req.Nombre ?? u.Nombre;
        u.Apellido = req.Apellido ?? u.Apellido;
        u.Telefono = req.Telefono ?? u.Telefono;
        u.RolId = req.RolId ?? u.RolId;

        if (req.Activo.HasValue)
            u.Estado = req.Activo.Value ? EstadoUsuario.Activo : EstadoUsuario.Inactivo;

        if (!string.IsNullOrWhiteSpace(req.NuevoPassword))
            u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NuevoPassword);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x =>
            x.Id == id && x.EmpresaId == EmpresaId && x.DeletedAt == null);
        if (u == null) return NotFound();

        u.DeletedAt = DateTimeOffset.UtcNow;
        u.Estado = EstadoUsuario.Inactivo;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateUsuarioRequest(string Email, string Password, string Nombre,
    string Apellido, Guid RolId, string? Telefono);
public record UpdateUsuarioRequest(string? Nombre, string? Apellido, string? Telefono,
    Guid? RolId, bool? Activo, string? NuevoPassword);

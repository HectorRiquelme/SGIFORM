using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SanitasField.Domain.Entities;
using SanitasField.Domain.Enums;
using SanitasField.Infrastructure.Persistence;

namespace SanitasField.Infrastructure.Services;

public interface IAuthService
{
    Task<AuthResult> LoginUsuarioAsync(string email, string password, string? ipOrigen = null);
    Task<AuthResult> LoginOperadorAsync(string codigoOperador, string empresaSlug, string password, string? deviceId = null);
    Task<AuthResult> RefreshTokenAsync(string refreshToken);
    Task RevocarTokenAsync(string refreshToken);
}

public record AuthResult(bool Success, string? Token, string? RefreshToken, string? Error,
    Guid? UserId, string? Nombre, string? Rol, Guid? EmpresaId, string? TenantSlug);

/// <summary>
/// Servicio de autenticación para usuarios web y operadores móviles.
/// Genera JWT con claims de empresa/tenant para el middleware multitenant.
/// </summary>
public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    // -------------------------------------------------------------------------
    // Login usuario web
    // -------------------------------------------------------------------------
    public async Task<AuthResult> LoginUsuarioAsync(string email, string password, string? ipOrigen = null)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.Empresa)
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Email == email.ToLower() && u.DeletedAt == null);

        if (usuario == null)
            return Fail("Credenciales inválidas");

        if (usuario.Estado == EstadoUsuario.Bloqueado)
            return Fail("Cuenta bloqueada. Contacte al administrador.");

        if (usuario.BloqueadoHasta.HasValue && usuario.BloqueadoHasta > DateTimeOffset.UtcNow)
            return Fail($"Cuenta bloqueada temporalmente hasta {usuario.BloqueadoHasta:HH:mm}.");

        if (!BCrypt.Net.BCrypt.Verify(password, usuario.PasswordHash))
        {
            usuario.IntentosFallidos++;
            if (usuario.IntentosFallidos >= 5)
                usuario.BloqueadoHasta = DateTimeOffset.UtcNow.AddMinutes(15);
            await _db.SaveChangesAsync();
            return Fail("Credenciales inválidas");
        }

        // Reset bloqueo
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.UltimoAcceso = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Email, usuario.Email),
            new(ClaimTypes.Name, usuario.NombreCompleto),
            new(ClaimTypes.Role, usuario.Rol.Codigo),
            new("empresa_id", usuario.EmpresaId.ToString()),
            new("tenant_slug", usuario.Empresa.TenantSlug),
            new("tipo_usuario", "web")
        };

        var (jwt, refresh) = await GenerarTokensAsync(claims, usuario.Id, ipOrigen);
        return new AuthResult(true, jwt, refresh, null,
            usuario.Id, usuario.NombreCompleto, usuario.Rol.Codigo,
            usuario.EmpresaId, usuario.Empresa.TenantSlug);
    }

    // -------------------------------------------------------------------------
    // Login operador móvil
    // -------------------------------------------------------------------------
    public async Task<AuthResult> LoginOperadorAsync(string codigoOperador, string empresaSlug, string password, string? deviceId = null)
    {
        var empresa = await _db.Empresas
            .FirstOrDefaultAsync(e => e.TenantSlug == empresaSlug && e.Activo && e.DeletedAt == null);

        if (empresa == null)
            return Fail("Empresa no encontrada");

        var operador = await _db.Operadores
            .FirstOrDefaultAsync(o => o.CodigoOperador == codigoOperador
                                   && o.EmpresaId == empresa.Id
                                   && o.Activo
                                   && o.DeletedAt == null);

        if (operador == null)
            return Fail("Credenciales inválidas");

        if (!BCrypt.Net.BCrypt.Verify(password, operador.PasswordHash))
            return Fail("Credenciales inválidas");

        // Actualizar device_id si se provee
        if (!string.IsNullOrEmpty(deviceId))
            operador.DeviceIdRegistrado = deviceId;

        await _db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, operador.Id.ToString()),
            new(ClaimTypes.Name, operador.NombreCompleto),
            new(ClaimTypes.Role, "operador"),
            new("empresa_id", empresa.Id.ToString()),
            new("tenant_slug", empresa.TenantSlug),
            new("tipo_usuario", "movil"),
            new("operador_id", operador.Id.ToString()),
            new("device_id", deviceId ?? "")
        };

        // Login móvil: JWT de larga duración (7 días), sin refresh token
        // porque la tabla refresh_token tiene FK a usuario, no a operador
        var jwt = GenerarJwt(claims, expMinutesOverride: 60 * 24 * 7);
        return new AuthResult(true, jwt, null, null,
            operador.Id, operador.NombreCompleto, "operador",
            empresa.Id, empresa.TenantSlug);
    }

    // -------------------------------------------------------------------------
    // Refresh Token
    // -------------------------------------------------------------------------
    public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
    {
        var token = await _db.RefreshTokens
            .Include(t => t.Usuario)
                .ThenInclude(u => u.Empresa)
            .Include(t => t.Usuario)
                .ThenInclude(u => u.Rol)
            .FirstOrDefaultAsync(t => t.Token == refreshToken);

        if (token == null || token.Revocado || token.ExpiraEn < DateTimeOffset.UtcNow)
            return Fail("Token inválido o expirado");

        var usuario = token.Usuario;

        // Validar que el usuario siga activo
        if (usuario.DeletedAt != null)
            return Fail("Usuario no encontrado");

        if (usuario.Estado == EstadoUsuario.Bloqueado)
            return Fail("Cuenta bloqueada. Contacte al administrador.");

        if (usuario.BloqueadoHasta.HasValue && usuario.BloqueadoHasta > DateTimeOffset.UtcNow)
            return Fail($"Cuenta bloqueada temporalmente hasta {usuario.BloqueadoHasta:HH:mm}.");

        // Revocar el token anterior (rotación de refresh tokens)
        token.Revocado = true;
        await _db.SaveChangesAsync();

        // Generar nuevos tokens directamente sin re-validar password
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Email, usuario.Email),
            new(ClaimTypes.Name, usuario.NombreCompleto),
            new(ClaimTypes.Role, usuario.Rol.Codigo),
            new("empresa_id", usuario.EmpresaId.ToString()),
            new("tenant_slug", usuario.Empresa.TenantSlug),
            new("tipo_usuario", "web")
        };

        var (jwt, newRefresh) = await GenerarTokensAsync(claims, usuario.Id, null);
        return new AuthResult(true, jwt, newRefresh, null,
            usuario.Id, usuario.NombreCompleto, usuario.Rol.Codigo,
            usuario.EmpresaId, usuario.Empresa.TenantSlug);
    }

    public async Task RevocarTokenAsync(string refreshToken)
    {
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);
        if (token != null)
        {
            token.Revocado = true;
            await _db.SaveChangesAsync();
        }
    }

    // -------------------------------------------------------------------------
    // Helpers privados
    // -------------------------------------------------------------------------

    /// <summary>Genera solo el JWT (sin refresh token). Usado para login móvil.</summary>
    private string GenerarJwt(List<Claim> claims, int? expMinutesOverride = null)
    {
        var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key no configurado");
        var jwtIssuer = _config["Jwt:Issuer"] ?? "SanitasField";
        var jwtAudience = _config["Jwt:Audience"] ?? "SanitasField";
        var expMinutes = expMinutesOverride ?? int.Parse(_config["Jwt:ExpirationMinutes"] ?? "60");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwtToken = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(expMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(jwtToken);
    }

    /// <summary>Genera JWT + refresh token (para usuarios web).</summary>
    private async Task<(string jwt, string refresh)> GenerarTokensAsync(
        List<Claim> claims, Guid usuarioId, string? ipOrigen)
    {
        var jwt = GenerarJwt(claims);

        // Generar refresh token
        var refreshBytes = RandomNumberGenerator.GetBytes(64);
        var refreshToken = Convert.ToBase64String(refreshBytes);

        var rt = new RefreshToken
        {
            UsuarioId = usuarioId,
            Token = refreshToken,
            ExpiraEn = DateTimeOffset.UtcNow.AddDays(30),
            IpOrigen = ipOrigen
        };
        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync();

        return (jwt, refreshToken);
    }

    private static AuthResult Fail(string error) =>
        new(false, null, null, error, null, null, null, null, null);
}

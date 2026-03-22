using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SgiForm.Domain.Entities;
using SgiForm.Domain.Enums;
using SgiForm.Infrastructure.Persistence;

namespace SgiForm.Infrastructure.Services;

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
/// Genera JWT de corta duración + refresh token rotativo para ambos tipos de sesión.
/// El token móvil ahora es revocable: si se desactiva un operador o pierde su dispositivo,
/// el administrador puede invalidar la sesión sin esperar la expiración del JWT.
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

    // ─── Login usuario web ────────────────────────────────────────────────────
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

        // Reset bloqueo en login exitoso
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.UltimoAcceso = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var claims = BuildUsuarioClaims(usuario);
        var (jwt, refresh) = await GenerarTokensAsync(claims, usuarioId: usuario.Id, operadorId: null, ipOrigen);
        return new AuthResult(true, jwt, refresh, null,
            usuario.Id, usuario.NombreCompleto, usuario.Rol.Codigo,
            usuario.EmpresaId, usuario.Empresa.TenantSlug);
    }

    // ─── Login operador móvil ─────────────────────────────────────────────────
    /// <summary>
    /// Emite JWT de 24 horas + refresh token rotativo de 30 días.
    /// Esto permite revocar el acceso de un operador inmediatamente desde la web.
    /// </summary>
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

        var claims = BuildOperadorClaims(operador, empresa, deviceId ?? operador.DeviceIdRegistrado);
        // JWT de 24h (antes eran 7 días hardcodeados sin refresh)
        var (jwt, refresh) = await GenerarTokensAsync(claims, usuarioId: null, operadorId: operador.Id, ipOrigen: null,
            expMinutesOverride: 60 * 24);

        return new AuthResult(true, jwt, refresh, null,
            operador.Id, operador.NombreCompleto, "operador",
            empresa.Id, empresa.TenantSlug);
    }

    // ─── Refresh Token ────────────────────────────────────────────────────────
    /// <summary>
    /// Renueva el JWT usando el refresh token.
    /// Maneja tanto sesiones de usuarios web como de operadores móviles.
    /// Si el operador fue desactivado o eliminado, el refresh falla inmediatamente.
    /// </summary>
    public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
    {
        var token = await _db.RefreshTokens
            .Include(t => t.Usuario).ThenInclude(u => u!.Empresa)
            .Include(t => t.Usuario).ThenInclude(u => u!.Rol)
            .Include(t => t.Operador).ThenInclude(o => o!.Empresa)
            .FirstOrDefaultAsync(t => t.Token == refreshToken);

        if (token == null || token.Revocado || token.ExpiraEn < DateTimeOffset.UtcNow)
            return Fail("Token inválido o expirado");

        // Rotación de refresh token: revocar el anterior
        token.Revocado = true;
        await _db.SaveChangesAsync();

        // ── Rama operador móvil ──────────────────────────────────────────────
        if (token.OperadorId.HasValue && token.Operador != null)
        {
            var operador = token.Operador;

            // Verificar que el operador sigue activo — si fue desactivado, acceso denegado
            if (operador.DeletedAt != null)
                return Fail("Operador eliminado. Contacte al administrador.");

            if (!operador.Activo)
                return Fail("Operador desactivado. Contacte al administrador.");

            var claims = BuildOperadorClaims(operador, operador.Empresa, operador.DeviceIdRegistrado);
            var (jwt, newRefresh) = await GenerarTokensAsync(claims, usuarioId: null, operadorId: operador.Id, ipOrigen: null,
                expMinutesOverride: 60 * 24);

            return new AuthResult(true, jwt, newRefresh, null,
                operador.Id, operador.NombreCompleto, "operador",
                operador.EmpresaId, operador.Empresa.TenantSlug);
        }

        // ── Rama usuario web ─────────────────────────────────────────────────
        if (token.UsuarioId.HasValue && token.Usuario != null)
        {
            var usuario = token.Usuario;

            if (usuario.DeletedAt != null)
                return Fail("Usuario no encontrado");

            if (usuario.Estado == EstadoUsuario.Bloqueado)
                return Fail("Cuenta bloqueada. Contacte al administrador.");

            if (usuario.BloqueadoHasta.HasValue && usuario.BloqueadoHasta > DateTimeOffset.UtcNow)
                return Fail($"Cuenta bloqueada temporalmente hasta {usuario.BloqueadoHasta:HH:mm}.");

            var claims = BuildUsuarioClaims(usuario);
            var (jwt, newRefresh) = await GenerarTokensAsync(claims, usuarioId: usuario.Id, operadorId: null, ipOrigen: null);
            return new AuthResult(true, jwt, newRefresh, null,
                usuario.Id, usuario.NombreCompleto, usuario.Rol.Codigo,
                usuario.EmpresaId, usuario.Empresa.TenantSlug);
        }

        return Fail("Token inválido: sin sujeto asociado");
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

    // ─── Helpers de construcción de claims ───────────────────────────────────

    private static List<Claim> BuildUsuarioClaims(Usuario usuario) =>
    [
        new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
        new(ClaimTypes.Email, usuario.Email),
        new(ClaimTypes.Name, usuario.NombreCompleto),
        new(ClaimTypes.Role, usuario.Rol.Codigo),
        new("empresa_id", usuario.EmpresaId.ToString()),
        new("tenant_slug", usuario.Empresa.TenantSlug),
        new("tipo_usuario", "web")
    ];

    private static List<Claim> BuildOperadorClaims(Operador operador, Empresa empresa, string? deviceId) =>
    [
        new(ClaimTypes.NameIdentifier, operador.Id.ToString()),
        new(ClaimTypes.Name, operador.NombreCompleto),
        new(ClaimTypes.Role, "operador"),
        new("empresa_id", empresa.Id.ToString()),
        new("tenant_slug", empresa.TenantSlug),
        new("tipo_usuario", "movil"),
        new("operador_id", operador.Id.ToString()),
        new("device_id", deviceId ?? "")
    ];

    // ─── Generación de tokens ─────────────────────────────────────────────────

    private string GenerarJwt(List<Claim> claims, int? expMinutesOverride = null)
    {
        var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key no configurado");
        var jwtIssuer = _config["Jwt:Issuer"] ?? "SgiForm";
        var jwtAudience = _config["Jwt:Audience"] ?? "SgiForm";
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

    private async Task<(string jwt, string refresh)> GenerarTokensAsync(
        List<Claim> claims, Guid? usuarioId, Guid? operadorId, string? ipOrigen,
        int? expMinutesOverride = null)
    {
        var jwt = GenerarJwt(claims, expMinutesOverride);

        var refreshBytes = RandomNumberGenerator.GetBytes(64);
        var refreshToken = Convert.ToBase64String(refreshBytes);

        var rt = new RefreshToken
        {
            UsuarioId = usuarioId,
            OperadorId = operadorId,
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

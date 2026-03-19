using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SanitasField.Infrastructure.Services;

namespace SanitasField.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Login para usuarios web (administrador, supervisor, auditor, etc.)</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var result = await _auth.LoginUsuarioAsync(req.Email, req.Password,
            HttpContext.Connection.RemoteIpAddress?.ToString());

        if (!result.Success)
            return Unauthorized(new { error = result.Error });

        return Ok(new
        {
            access_token = result.Token,
            refresh_token = result.RefreshToken,
            token_type = "bearer",
            nombre = result.Nombre,
            rol = result.Rol,
            empresa_id = result.EmpresaId,
            tenant_slug = result.TenantSlug
        });
    }

    /// <summary>Login para operadores móviles Android</summary>
    [HttpPost("login-movil")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginMovil([FromBody] LoginMovilRequest req)
    {
        var result = await _auth.LoginOperadorAsync(
            req.CodigoOperador, req.EmpresaSlug, req.Password, req.DeviceId);

        if (!result.Success)
            return Unauthorized(new { error = result.Error });

        return Ok(new
        {
            access_token = result.Token,
            refresh_token = result.RefreshToken,
            token_type = "bearer",
            nombre = result.Nombre,
            operador_id = result.UserId,
            empresa_id = result.EmpresaId,
            tenant_slug = result.TenantSlug
        });
    }

    /// <summary>Renovar token JWT usando refresh token</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        var result = await _auth.RefreshTokenAsync(req.RefreshToken);
        if (!result.Success)
            return Unauthorized(new { error = result.Error });

        return Ok(new
        {
            access_token = result.Token,
            refresh_token = result.RefreshToken,
            token_type = "bearer"
        });
    }

    /// <summary>Cerrar sesión (revocar refresh token)</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
    {
        await _auth.RevocarTokenAsync(req.RefreshToken);
        return NoContent();
    }
}

public record LoginRequest(string Email, string Password);
public record LoginMovilRequest(string CodigoOperador, string EmpresaSlug, string Password, string? DeviceId);
public record RefreshRequest(string RefreshToken);

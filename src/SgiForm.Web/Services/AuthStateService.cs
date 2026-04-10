namespace SgiForm.Web.Services;

/// <summary>
/// Servicio de estado de autenticación para Blazor Server.
/// Se registra como Scoped — vive por circuito SignalR (sesión de navegador).
/// Reemplaza el uso de IHttpContextAccessor/Session que no funciona
/// en componentes con render interactivo.
/// </summary>
public class AuthStateService
{
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTimeOffset? TokenExpiry { get; private set; }
    public string? Nombre { get; private set; }
    public string? Rol { get; private set; }
    public Guid? EmpresaId { get; private set; }
    public string? TenantSlug { get; private set; }

    /// <summary>Semáforo para serializar intentos de refresh concurrentes dentro del mismo circuito.</summary>
    public SemaphoreSlim RefreshLock { get; } = new(1, 1);

    public bool EsAutenticado => !string.IsNullOrEmpty(AccessToken);

    /// <summary>
    /// Notifica a los componentes suscritos cuando cambia el estado de auth.
    /// </summary>
    public event Action? OnChange;

    public void SetSession(string accessToken, string? refreshToken,
        DateTimeOffset? tokenExpiry, string nombre, string rol,
        Guid empresaId, string tenantSlug)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        TokenExpiry = tokenExpiry;
        Nombre = nombre;
        Rol = rol;
        EmpresaId = empresaId;
        TenantSlug = tenantSlug;
        NotifyStateChanged();
    }

    /// <summary>Actualiza sólo los tokens tras un refresh exitoso (mantiene datos del usuario).</summary>
    public void UpdateTokens(string accessToken, string? refreshToken, DateTimeOffset? tokenExpiry)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        TokenExpiry = tokenExpiry;
        NotifyStateChanged();
    }

    public void ClearSession()
    {
        AccessToken = null;
        RefreshToken = null;
        TokenExpiry = null;
        Nombre = null;
        Rol = null;
        EmpresaId = null;
        TenantSlug = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

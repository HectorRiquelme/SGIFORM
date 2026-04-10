using System.Text.Json;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace SgiForm.Web.Services;

/// <summary>
/// Servicio de estado de autenticación para Blazor Server.
/// Se registra como Scoped — vive por circuito SignalR (sesión de navegador).
///
/// Soporta persistencia opcional en ProtectedLocalStorage para sobrevivir
/// F5 / nuevas pestañas en el mismo origen. La persistencia solo funciona
/// después del primer render interactivo (no en prerender SSR).
/// </summary>
public class AuthStateService
{
    private const string StorageKey = "sgiform_session_v1";

    private readonly ProtectedLocalStorage _storage;

    public AuthStateService(ProtectedLocalStorage storage)
    {
        _storage = storage;
    }

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
        // Re-persistir los tokens refrescados para que futuras pestañas reciban los vigentes
        // (fire-and-forget: PersistAsync atrapa sus propias excepciones).
        _ = PersistAsync();
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

    /// <summary>
    /// Persiste la sesión actual en ProtectedLocalStorage.
    /// Solo invocable desde OnAfterRenderAsync o handlers de eventos interactivos
    /// (requiere JS interop, no funciona durante prerender SSR).
    /// </summary>
    public async Task PersistAsync()
    {
        if (!EsAutenticado) return;
        try
        {
            var snapshot = new SessionSnapshot(
                AccessToken!, RefreshToken, TokenExpiry,
                Nombre, Rol, EmpresaId, TenantSlug);
            var json = JsonSerializer.Serialize(snapshot);
            await _storage.SetAsync(StorageKey, json);
        }
        catch
        {
            // Si JS no está disponible (prerender) o el usuario bloqueó storage,
            // fallamos silenciosamente — la sesión sigue viva en memoria.
        }
    }

    /// <summary>
    /// Intenta hidratar la sesión desde ProtectedLocalStorage.
    /// Devuelve true si se restauró una sesión válida (no expirada).
    /// Solo invocable desde OnAfterRenderAsync (requiere JS interop).
    /// </summary>
    public async Task<bool> HydrateAsync()
    {
        if (EsAutenticado) return true;
        try
        {
            var result = await _storage.GetAsync<string>(StorageKey);
            if (!result.Success || string.IsNullOrEmpty(result.Value)) return false;

            var snapshot = JsonSerializer.Deserialize<SessionSnapshot>(result.Value);
            if (snapshot == null || string.IsNullOrEmpty(snapshot.AccessToken)) return false;

            // Si el access token ya expiró y no hay refresh token, descartar
            if (snapshot.TokenExpiry.HasValue && snapshot.TokenExpiry.Value < DateTimeOffset.UtcNow
                && string.IsNullOrEmpty(snapshot.RefreshToken))
            {
                await _storage.DeleteAsync(StorageKey);
                return false;
            }

            AccessToken = snapshot.AccessToken;
            RefreshToken = snapshot.RefreshToken;
            TokenExpiry = snapshot.TokenExpiry;
            Nombre = snapshot.Nombre;
            Rol = snapshot.Rol;
            EmpresaId = snapshot.EmpresaId;
            TenantSlug = snapshot.TenantSlug;
            NotifyStateChanged();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Elimina la sesión persistida (usar al cerrar sesión).</summary>
    public async Task ClearPersistedAsync()
    {
        try { await _storage.DeleteAsync(StorageKey); }
        catch { /* ignorar */ }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    private record SessionSnapshot(
        string AccessToken,
        string? RefreshToken,
        DateTimeOffset? TokenExpiry,
        string? Nombre,
        string? Rol,
        Guid? EmpresaId,
        string? TenantSlug);
}

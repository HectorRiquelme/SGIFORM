using System.Net.Http.Json;
using System.Text.Json;

namespace SanitasField.Mobile.Services;

/// <summary>
/// Servicio de autenticación para la app móvil.
/// Almacena el token JWT y datos del operador en SecureStorage de .NET MAUI.
/// El token se cachea en memoria para evitar deadlocks con SecureStorage.GetAsync().Result.
/// </summary>
public class AuthService
{
    private readonly HttpClientHolder _httpHolder;
    private string? _cachedToken;
    private bool _tokenLoaded;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public AuthService(HttpClientHolder httpHolder)
    {
        _httpHolder = httpHolder;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // TOKEN Y SESIÓN (sin deadlocks)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Token cacheado en memoria. Usar InitializeAsync() al inicio.</summary>
    public string? AccessToken => _cachedToken;

    /// <summary>Cargar token desde SecureStorage (llamar una vez al inicio, async).</summary>
    public async Task InitializeAsync()
    {
        if (!_tokenLoaded)
        {
            try
            {
                _cachedToken = await SecureStorage.GetAsync("access_token");
            }
            catch
            {
                _cachedToken = null;
            }
            _tokenLoaded = true;
        }
    }

    /// <summary>Guardar token en SecureStorage y en caché.</summary>
    public async Task SetAccessTokenAsync(string? token)
    {
        _cachedToken = token;
        _tokenLoaded = true;
        if (token == null)
            SecureStorage.Remove("access_token");
        else
            await SecureStorage.SetAsync("access_token", token);
    }

    public string? OperadorId
    {
        get => Preferences.Get("operador_id", null);
        set => Preferences.Set("operador_id", value ?? "");
    }

    public string? OperadorNombre
    {
        get => Preferences.Get("operador_nombre", null);
        set => Preferences.Set("operador_nombre", value ?? "");
    }

    public string? EmpresaId
    {
        get => Preferences.Get("empresa_id", null);
        set => Preferences.Set("empresa_id", value ?? "");
    }

    public string? TenantSlug
    {
        get => Preferences.Get("tenant_slug", null);
        set => Preferences.Set("tenant_slug", value ?? "");
    }

    public DateTimeOffset? UltimaSync
    {
        get
        {
            var str = Preferences.Get("ultima_sync", null);
            if (string.IsNullOrEmpty(str)) return null;
            return DateTimeOffset.TryParse(str, out var dt) ? dt : null;
        }
        set => Preferences.Set("ultima_sync", value?.ToString("O") ?? "");
    }

    public bool EsAutenticado => !string.IsNullOrEmpty(_cachedToken);

    /// <summary>Actualiza la URL base del HttpClient compartido.</summary>
    public void ActualizarBaseUrl(string url)
    {
        try
        {
            _httpHolder.UpdateBaseUrl(url);
        }
        catch
        {
            // Si la URL es inválida, no cambiar
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // LOGIN
    // ──────────────────────────────────────────────────────────────────────────
    public async Task<LoginResult> LoginAsync(string codigoOperador, string empresaSlug, string password)
    {
        try
        {
            var payload = new
            {
                codigo_operador = codigoOperador,
                empresa_slug = empresaSlug,
                password,
                device_id = GetDeviceId()
            };

            var response = await _httpHolder.Client.PostAsJsonAsync(
                "api/v1/auth/login-movil", payload, JsonOpts);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                // Intentar extraer mensaje del servidor
                try
                {
                    var errorData = JsonSerializer.Deserialize<JsonElement>(errorContent);
                    if (errorData.TryGetProperty("error", out var errProp))
                        return new LoginResult(false, errProp.GetString());
                }
                catch { }
                return new LoginResult(false, $"Error del servidor ({(int)response.StatusCode})");
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<LoginResponse>(json, JsonOpts);

            if (data?.AccessToken == null)
                return new LoginResult(false, "Respuesta inválida del servidor");

            // Guardar sesión (async, sin deadlock)
            await SetAccessTokenAsync(data.AccessToken);
            OperadorId = data.OperadorId?.ToString();
            OperadorNombre = data.Nombre;
            EmpresaId = data.EmpresaId?.ToString();
            TenantSlug = data.TenantSlug;

            return new LoginResult(true, null);
        }
        catch (HttpRequestException ex)
        {
            return new LoginResult(false,
                $"No se pudo conectar al servidor. Verifica la URL de la API. ({ex.Message})");
        }
        catch (TaskCanceledException)
        {
            return new LoginResult(false, "Tiempo de espera agotado. Verifica tu conexión.");
        }
        catch (Exception ex)
        {
            return new LoginResult(false, $"Error: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // LOGOUT
    // ──────────────────────────────────────────────────────────────────────────
    public async Task LogoutAsync()
    {
        await SetAccessTokenAsync(null);
        Preferences.Remove("operador_id");
        Preferences.Remove("operador_nombre");
        Preferences.Remove("empresa_id");
        Preferences.Remove("tenant_slug");
        Preferences.Remove("ultima_sync");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // HELPERS
    // ──────────────────────────────────────────────────────────────────────────
    private static string GetDeviceId()
    {
        var deviceId = Preferences.Get("device_id", null);
        if (deviceId == null)
        {
            deviceId = Guid.NewGuid().ToString();
            Preferences.Set("device_id", deviceId);
        }
        return deviceId;
    }

    private record LoginResponse(
        string? AccessToken, string? RefreshToken,
        string? Nombre, Guid? OperadorId,
        Guid? EmpresaId, string? TenantSlug);
}

public record LoginResult(bool Success, string? Error);

/// <summary>
/// Contenedor compartido del HttpClient.
/// Permite que AuthService y SyncService usen siempre la misma instancia,
/// incluso después de cambiar la URL base.
/// </summary>
public class HttpClientHolder
{
    public HttpClient Client { get; private set; }

    public HttpClientHolder(string baseUrl)
    {
        Client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public void UpdateBaseUrl(string url)
    {
        var newUri = new Uri(url);
        // Crear nuevo HttpClient solo si la URL cambió
        if (Client.BaseAddress != newUri)
        {
            Client = new HttpClient
            {
                BaseAddress = newUri,
                Timeout = TimeSpan.FromSeconds(30)
            };
        }
    }
}

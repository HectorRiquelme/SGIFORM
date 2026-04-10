using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace SgiForm.Web.Services;

/// <summary>
/// Cliente HTTP tipado para consumir la API REST de SgiForm.
/// Lee el JWT de AuthStateService (scoped por circuito Blazor).
/// Implementa refresh automático del access token antes de que expire
/// y reintenta una vez cuando la API responde 401 Unauthorized.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly AuthStateService _auth;
    private readonly ILogger<ApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Refresca el token si quedan menos de este umbral para expirar.</summary>
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(2);

    public ApiClient(HttpClient http, AuthStateService auth, ILogger<ApiClient> logger)
    {
        _http = http;
        _auth = auth;
        _logger = logger;
    }

    private void SetAuthHeader()
    {
        if (!string.IsNullOrEmpty(_auth.AccessToken))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _auth.AccessToken);
        else
            _http.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Ejecuta una llamada HTTP con refresh preemptivo y retry en 401.
    /// Centraliza la lógica de auth para todos los verbos.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithAuthAsync(Func<Task<HttpResponseMessage>> send)
    {
        await EnsureValidTokenAsync();
        SetAuthHeader();

        var response = await send();

        // Retry una vez si el token fue rechazado (puede haber expirado entre comprobaciones)
        if (response.StatusCode == HttpStatusCode.Unauthorized &&
            !string.IsNullOrEmpty(_auth.RefreshToken))
        {
            response.Dispose();
            if (await TryRefreshAsync())
            {
                SetAuthHeader();
                response = await send();
            }
        }

        return response;
    }

    /// <summary>Refresca preemptivamente si el token expira dentro de RefreshThreshold.</summary>
    private async Task EnsureValidTokenAsync()
    {
        if (string.IsNullOrEmpty(_auth.AccessToken) || string.IsNullOrEmpty(_auth.RefreshToken))
            return;

        if (!_auth.TokenExpiry.HasValue)
            return;

        if (_auth.TokenExpiry.Value - DateTimeOffset.UtcNow > RefreshThreshold)
            return;

        await TryRefreshAsync();
    }

    /// <summary>Llama al endpoint /auth/refresh; serializa intentos concurrentes por circuito.</summary>
    private async Task<bool> TryRefreshAsync()
    {
        if (string.IsNullOrEmpty(_auth.RefreshToken)) return false;

        await _auth.RefreshLock.WaitAsync();
        try
        {
            // Double-check: otro request en paralelo pudo haber refrescado ya
            if (_auth.TokenExpiry.HasValue &&
                _auth.TokenExpiry.Value - DateTimeOffset.UtcNow > RefreshThreshold)
                return true;

            // Llamada directa sin pasar por SendWithAuthAsync para evitar recursión
            var payload = new { refresh_token = _auth.RefreshToken };
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/refresh")
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };
            // Este endpoint es AllowAnonymous — no mandar el bearer expirado
            request.Headers.Authorization = null;

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Refresh token rechazado ({Status}); cerrando sesión", (int)response.StatusCode);
                _auth.ClearSession();
                return false;
            }

            var data = await response.Content.ReadFromJsonAsync<RefreshResponse>(JsonOptions);
            if (data?.AccessToken == null)
            {
                _auth.ClearSession();
                return false;
            }

            _auth.UpdateTokens(
                data.AccessToken,
                data.RefreshToken ?? _auth.RefreshToken,
                ParseJwtExpiry(data.AccessToken));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refrescando token");
            return false;
        }
        finally
        {
            _auth.RefreshLock.Release();
        }
    }

    /// <summary>Decodifica el claim "exp" del payload JWT (unix seconds → DateTimeOffset).</summary>
    private static DateTimeOffset? ParseJwtExpiry(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var expSec))
                return DateTimeOffset.FromUnixTimeSeconds(expSec);
            return null;
        }
        catch
        {
            return null;
        }
    }

    // ── GET ──────────────────────────────────────────────────────────────────
    public async Task<T?> GetAsync<T>(string url)
    {
        try
        {
            using var response = await SendWithAuthAsync(() => _http.GetAsync(url));
            if (!response.IsSuccessStatusCode) return default;
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET {Url} failed", url);
            return default;
        }
    }

    // ── POST ─────────────────────────────────────────────────────────────────
    public async Task<ApiResult<T>> PostAsync<T>(string url, object data)
    {
        try
        {
            using var response = await SendWithAuthAsync(() =>
                _http.PostAsJsonAsync(url, data, JsonOptions));
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<T>(content, JsonOptions);
                return ApiResult<T>.Ok(result!);
            }
            return ApiResult<T>.Fail($"Error {(int)response.StatusCode}: {content}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Url} failed", url);
            return ApiResult<T>.Fail(ex.Message);
        }
    }

    // ── PUT tipado ────────────────────────────────────────────────────────────
    public async Task<ApiResult<T>> PutAsync<T>(string url, object data)
    {
        try
        {
            using var response = await SendWithAuthAsync(() =>
                _http.PutAsJsonAsync(url, data, JsonOptions));
            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
                return ApiResult<T>.Ok(response.StatusCode == HttpStatusCode.NoContent
                    ? default! : JsonSerializer.Deserialize<T>(content, JsonOptions)!);
            return ApiResult<T>.Fail($"Error {(int)response.StatusCode}: {content}");
        }
        catch (Exception ex) { return ApiResult<T>.Fail(ex.Message); }
    }

    // ── PUT ──────────────────────────────────────────────────────────────────
    public async Task<ApiResult> PutAsync(string url, object data)
    {
        try
        {
            using var response = await SendWithAuthAsync(() =>
                _http.PutAsJsonAsync(url, data, JsonOptions));
            if (response.IsSuccessStatusCode) return ApiResult.Ok();
            var error = await response.Content.ReadAsStringAsync();
            return ApiResult.Fail(error);
        }
        catch (Exception ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    // ── DELETE ───────────────────────────────────────────────────────────────
    public async Task<ApiResult> DeleteAsync(string url)
    {
        try
        {
            using var response = await SendWithAuthAsync(() => _http.DeleteAsync(url));
            if (response.IsSuccessStatusCode) return ApiResult.Ok();
            var error = await response.Content.ReadAsStringAsync();
            return ApiResult.Fail(error);
        }
        catch (Exception ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    // ── POST MULTIPART ───────────────────────────────────────────────────────
    public async Task<ApiResult<T>> PostMultipartAsync<T>(string url, MultipartFormDataContent content)
    {
        try
        {
            using var response = await SendWithAuthAsync(() => _http.PostAsync(url, content));
            var body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
                return ApiResult<T>.Ok(JsonSerializer.Deserialize<T>(body, JsonOptions)!);
            return ApiResult<T>.Fail($"Error {(int)response.StatusCode}: {body}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST multipart {Url} failed", url);
            return ApiResult<T>.Fail(ex.Message);
        }
    }

    // ── LOGIN ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// Llama a POST /api/v1/auth/login, guarda el token en AuthStateService.
    /// </summary>
    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                "api/v1/auth/login",
                new { email, password },
                JsonOptions);

            if (!response.IsSuccessStatusCode)
                return new LoginResult(false, "Email o contraseña incorrectos.", null);

            var data = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
            if (data?.AccessToken == null)
                return new LoginResult(false, "Respuesta inválida del servidor.", null);

            _auth.SetSession(
                data.AccessToken,
                data.RefreshToken,
                ParseJwtExpiry(data.AccessToken),
                data.Nombre ?? "Usuario",
                data.Rol ?? "admin",
                data.EmpresaId ?? Guid.Empty,
                data.TenantSlug ?? "");

            return new LoginResult(true, null, data);
        }
        catch (HttpRequestException)
        {
            return new LoginResult(false, "No se pudo conectar con la API. Verifica que esté corriendo.", null);
        }
        catch (Exception ex)
        {
            return new LoginResult(false, ex.Message, null);
        }
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record LoginResult(bool Success, string? Error, LoginResponse? Data);

public record LoginResponse(
    string? AccessToken,
    string? RefreshToken,
    string? Nombre,
    string? Rol,
    Guid? EmpresaId,
    string? TenantSlug);

public record RefreshResponse(
    string? AccessToken,
    string? RefreshToken,
    string? TokenType);

public class ApiResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }

    public static ApiResult<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResult<T> Fail(string error) => new() { Success = false, Error = error };
}

public class ApiResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static ApiResult Ok() => new() { Success = true };
    public static ApiResult Fail(string error) => new() { Success = false, Error = error };
}

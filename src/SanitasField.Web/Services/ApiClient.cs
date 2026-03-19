using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SanitasField.Web.Services;

/// <summary>
/// Cliente HTTP tipado para consumir la API REST de SanitasField.
/// Lee el JWT de AuthStateService (scoped por circuito Blazor).
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

    // ── GET ──────────────────────────────────────────────────────────────────
    public async Task<T?> GetAsync<T>(string url)
    {
        SetAuthHeader();
        try
        {
            var response = await _http.GetAsync(url);
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
        SetAuthHeader();
        try
        {
            var response = await _http.PostAsJsonAsync(url, data, JsonOptions);
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
        SetAuthHeader();
        try
        {
            var response = await _http.PutAsJsonAsync(url, data, JsonOptions);
            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
                return ApiResult<T>.Ok(response.StatusCode == System.Net.HttpStatusCode.NoContent
                    ? default! : JsonSerializer.Deserialize<T>(content, JsonOptions)!);
            return ApiResult<T>.Fail($"Error {(int)response.StatusCode}: {content}");
        }
        catch (Exception ex) { return ApiResult<T>.Fail(ex.Message); }
    }

    // ── PUT ──────────────────────────────────────────────────────────────────
    public async Task<ApiResult> PutAsync(string url, object data)
    {
        SetAuthHeader();
        try
        {
            var response = await _http.PutAsJsonAsync(url, data, JsonOptions);
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
        SetAuthHeader();
        try
        {
            var response = await _http.DeleteAsync(url);
            if (response.IsSuccessStatusCode) return ApiResult.Ok();
            var error = await response.Content.ReadAsStringAsync();
            return ApiResult.Fail(error);
        }
        catch (Exception ex)
        {
            return ApiResult.Fail(ex.Message);
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

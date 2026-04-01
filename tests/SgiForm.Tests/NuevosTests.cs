using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SgiForm.Tests;

/// <summary>
/// Tests de integración para los módulos nuevos:
/// - Zonas y Localidades (BLOQUE 2)
/// - Preguntas con campos foto (BLOQUE 3)
/// - Georreferencia: Ubicacion + Inspecciones/geo (BLOQUE 4/5)
/// - Importaciones: plantilla Excel (BUG-03)
/// - Reportes: productividad Excel (BUG-04)
/// </summary>

// ═══════════════════════════════════════════════════════════════════════════════
// ZONAS TESTS — CRUD completo
// ═══════════════════════════════════════════════════════════════════════════════
public class ZonasTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _factory;
    public ZonasTests(TestFixture factory) => _factory = factory;

    // ── GET ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_SinZonas_RetornaListaVacia()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.GetFromJsonAsync<JsonElement>("api/v1/zonas");

        resp.ValueKind.Should().Be(JsonValueKind.Array);
        // BD de test sin zonas seed → array vacío
        resp.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetAll_SinAutenticacion_Retorna401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("api/v1/zonas");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── POST ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ConDatosValidos_Retorna201()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.PostAsJsonAsync("api/v1/zonas", new
        {
            codigo = "ZON-NORTE",
            nombre = "Zona Norte",
            descripcion = "Sector norte de la ciudad",
            activo = true
        }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var data = await resp.Content.ReadFromJsonAsync<JsonElement>();
        data.GetProperty("codigo").GetString().Should().Be("ZON-NORTE");
        data.GetProperty("nombre").GetString().Should().Be("Zona Norte");
        data.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Create_CodigoDuplicado_Retorna409()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        // Primera creación
        await client.PostAsJsonAsync("api/v1/zonas", new
            { codigo = "ZON-DUP", nombre = "Zona Duplicada" }, TestFixture.JsonOpts);

        // Segunda con mismo código
        var resp = await client.PostAsJsonAsync("api/v1/zonas", new
            { codigo = "ZON-DUP", nombre = "Otra Zona" }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_SinNombre_Retorna400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.PostAsJsonAsync("api/v1/zonas", new
            { codigo = "ZON-X", nombre = "" }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_SinCodigo_Retorna400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.PostAsJsonAsync("api/v1/zonas", new
            { codigo = "", nombre = "Zona Sin Código" }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PUT ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ZonaExistente_ActualizaDatos()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Crear primero
        var created = await (await client.PostAsJsonAsync("api/v1/zonas", new
            { codigo = "ZON-UPD", nombre = "Original" }, TestFixture.JsonOpts))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString();

        // Actualizar
        var resp = await client.PutAsJsonAsync($"api/v1/zonas/{id}", new
            { codigo = "ZON-UPD", nombre = "Nombre Actualizado", activo = false },
            TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = await resp.Content.ReadFromJsonAsync<JsonElement>();
        data.GetProperty("nombre").GetString().Should().Be("Nombre Actualizado");
        data.GetProperty("activo").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Update_ZonaInexistente_Retorna404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.PutAsJsonAsync(
            $"api/v1/zonas/{Guid.NewGuid()}",
            new { nombre = "X" }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ZonaExistente_Retorna204()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Crear
        var created = await (await client.PostAsJsonAsync("api/v1/zonas", new
            { codigo = "ZON-DEL", nombre = "Para Eliminar" }, TestFixture.JsonOpts))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString();

        // Eliminar
        var del = await client.DeleteAsync($"api/v1/zonas/{id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verificar que ya no aparece
        var get = await client.GetAsync($"api/v1/zonas/{id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ZonaInexistente_Retorna404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.DeleteAsync($"api/v1/zonas/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── FILTRO soloActivas ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_FiltroSoloActivas_ExcluyeInactivas()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Crear activa e inactiva
        await client.PostAsJsonAsync("api/v1/zonas",
            new { codigo = "ZON-ACT", nombre = "Activa", activo = true }, TestFixture.JsonOpts);
        await client.PostAsJsonAsync("api/v1/zonas",
            new { codigo = "ZON-INA", nombre = "Inactiva", activo = false }, TestFixture.JsonOpts);

        var resp = await client.GetFromJsonAsync<JsonElement>("api/v1/zonas?soloActivas=true");
        resp.ValueKind.Should().Be(JsonValueKind.Array);
        // Ninguna debe tener activo=false
        foreach (var item in resp.EnumerateArray())
            item.GetProperty("activo").GetBoolean().Should().BeTrue();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// LOCALIDADES TESTS — CRUD por zona
// ═══════════════════════════════════════════════════════════════════════════════
public class LocalidadesTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _factory;
    public LocalidadesTests(TestFixture factory) => _factory = factory;

    private async Task<(HttpClient client, string zonaId)> CrearZonaAsync(string suffix = "")
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var zona = await (await client.PostAsJsonAsync("api/v1/zonas", new
            { codigo = $"ZON-LOC{suffix}", nombre = $"Zona Localidades {suffix}" },
            TestFixture.JsonOpts)).Content.ReadFromJsonAsync<JsonElement>();
        return (client, zona.GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task GetLocalidades_ZonaSinLocalidades_RetornaVacio()
    {
        var (client, zonaId) = await CrearZonaAsync("A");
        var resp = await client.GetFromJsonAsync<JsonElement>($"api/v1/zonas/{zonaId}/localidades");

        resp.ValueKind.Should().Be(JsonValueKind.Array);
        resp.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task CreateLocalidad_ConDatosValidos_Retorna201()
    {
        var (client, zonaId) = await CrearZonaAsync("B");
        var resp = await client.PostAsJsonAsync($"api/v1/zonas/{zonaId}/localidades", new
        {
            codigo = "LOC-SERENA",
            nombre = "La Serena",
            activo = true
        }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var data = await resp.Content.ReadFromJsonAsync<JsonElement>();
        data.GetProperty("codigo").GetString().Should().Be("LOC-SERENA");
        data.GetProperty("nombre").GetString().Should().Be("La Serena");
        data.GetProperty("zona_id").GetString().Should().Be(zonaId);
    }

    [Fact]
    public async Task CreateLocalidad_CodigoDuplicado_Retorna409()
    {
        var (client, zonaId) = await CrearZonaAsync("C");

        await client.PostAsJsonAsync($"api/v1/zonas/{zonaId}/localidades",
            new { codigo = "LOC-DUP", nombre = "Primera" }, TestFixture.JsonOpts);

        var resp = await client.PostAsJsonAsync($"api/v1/zonas/{zonaId}/localidades",
            new { codigo = "LOC-DUP", nombre = "Segunda" }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateLocalidad_ZonaInexistente_Retorna404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.PostAsJsonAsync(
            $"api/v1/zonas/{Guid.NewGuid()}/localidades",
            new { codigo = "LOC-X", nombre = "Localidad Huerfana" }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetLocalidades_RetornaLocalidadesDeZona()
    {
        var (client, zonaId) = await CrearZonaAsync("D");

        await client.PostAsJsonAsync($"api/v1/zonas/{zonaId}/localidades",
            new { codigo = "LOC-D1", nombre = "Localidad D1" }, TestFixture.JsonOpts);
        await client.PostAsJsonAsync($"api/v1/zonas/{zonaId}/localidades",
            new { codigo = "LOC-D2", nombre = "Localidad D2" }, TestFixture.JsonOpts);

        var resp = await client.GetFromJsonAsync<JsonElement>($"api/v1/zonas/{zonaId}/localidades");
        resp.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task UpdateLocalidad_ActualizaDatos()
    {
        var (client, zonaId) = await CrearZonaAsync("E");
        var loc = await (await client.PostAsJsonAsync($"api/v1/zonas/{zonaId}/localidades",
            new { codigo = "LOC-E1", nombre = "Original E" }, TestFixture.JsonOpts))
            .Content.ReadFromJsonAsync<JsonElement>();
        var locId = loc.GetProperty("id").GetString();

        var resp = await client.PutAsJsonAsync(
            $"api/v1/zonas/localidades/{locId}",
            new { nombre = "Actualizado E", activo = false }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = await resp.Content.ReadFromJsonAsync<JsonElement>();
        data.GetProperty("nombre").GetString().Should().Be("Actualizado E");
        data.GetProperty("activo").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task DeleteLocalidad_SoftDelete_NoAparece()
    {
        var (client, zonaId) = await CrearZonaAsync("F");
        var loc = await (await client.PostAsJsonAsync($"api/v1/zonas/{zonaId}/localidades",
            new { codigo = "LOC-F1", nombre = "Para Borrar F" }, TestFixture.JsonOpts))
            .Content.ReadFromJsonAsync<JsonElement>();
        var locId = loc.GetProperty("id").GetString();

        var del = await client.DeleteAsync($"api/v1/zonas/localidades/{locId}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verificar que no aparece en la lista
        var list = await client.GetFromJsonAsync<JsonElement>($"api/v1/zonas/{zonaId}/localidades");
        list.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetTodasLocalidades_RetornaTodasDeEmpresa()
    {
        var (client, zonaId) = await CrearZonaAsync("G");
        await client.PostAsJsonAsync($"api/v1/zonas/{zonaId}/localidades",
            new { codigo = "LOC-G1", nombre = "Localidad G1" }, TestFixture.JsonOpts);
        await client.PostAsJsonAsync($"api/v1/zonas/{zonaId}/localidades",
            new { codigo = "LOC-G2", nombre = "Localidad G2" }, TestFixture.JsonOpts);

        var resp = await client.GetFromJsonAsync<JsonElement>("api/v1/zonas/localidades");
        resp.ValueKind.Should().Be(JsonValueKind.Array);
        resp.GetArrayLength().Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task GetTodasLocalidades_FiltroSoloActivas_ExcluyeInactivas()
    {
        var (client, zonaId) = await CrearZonaAsync("H");
        await client.PostAsJsonAsync($"api/v1/zonas/{zonaId}/localidades",
            new { codigo = "LOC-H-ACT", nombre = "Activa H", activo = true }, TestFixture.JsonOpts);

        // Crear inactiva directa
        var locInact = await (await client.PostAsJsonAsync($"api/v1/zonas/{zonaId}/localidades",
            new { codigo = "LOC-H-INA", nombre = "Inactiva H", activo = true }, TestFixture.JsonOpts))
            .Content.ReadFromJsonAsync<JsonElement>();
        var locId = locInact.GetProperty("id").GetString();
        await client.PutAsJsonAsync($"api/v1/zonas/localidades/{locId}",
            new { activo = false }, TestFixture.JsonOpts);

        var resp = await client.GetFromJsonAsync<JsonElement>("api/v1/zonas/localidades?soloActivas=true");
        foreach (var item in resp.EnumerateArray())
            item.GetProperty("activo").GetBoolean().Should().BeTrue();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// FLUJOS — PREGUNTAS CON TIPO CONTROL (BUG-02 + BLOQUE 3)
// Cada test crea su propio flujo (auto-genera versión Borrador) y sección.
// No se puede reutilizar el flujo del seed porque está en estado Publicado.
// ═══════════════════════════════════════════════════════════════════════════════
public class FlujoPreguntasTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _factory;
    public FlujoPreguntasTests(TestFixture factory) => _factory = factory;

    /// <summary>
    /// Crea un flujo nuevo (versión borrador) + una sección.
    /// Devuelve (client, flujoId, versionId, seccionId).
    /// </summary>
    private async Task<(HttpClient client, string flujoId, string versionId, string seccionId)>
        CrearFlujoConSeccionAsync(string suffix = "")
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        // POST /flujos → crea flujo con versión borrador automáticamente
        var flujoResp = await client.PostAsJsonAsync("api/v1/flujos", new
        {
            nombre = $"Flujo Test Preguntas {suffix}",
            tipo_inspeccion_id = TestFixture.TipoInsp1Id
        }, TestFixture.JsonOpts);
        flujoResp.StatusCode.Should().Be(HttpStatusCode.Created,
            "la creación del flujo debe funcionar como prerequisito");

        var flujoData = await flujoResp.Content.ReadFromJsonAsync<JsonElement>();
        var flujoId = flujoData.GetProperty("id").GetString()!;
        var versionId = flujoData.GetProperty("version_id").GetString()!;

        // POST /flujos/{id}/versiones/{vId}/secciones → crear sección
        var secResp = await client.PostAsJsonAsync(
            $"api/v1/flujos/{flujoId}/versiones/{versionId}/secciones",
            new { codigo = $"SEC_TEST_{suffix}", titulo = "Sección Test" },
            TestFixture.JsonOpts);
        secResp.StatusCode.Should().Be(HttpStatusCode.Created,
            "la creación de la sección debe funcionar como prerequisito");

        var secData = await secResp.Content.ReadFromJsonAsync<JsonElement>();
        var seccionId = secData.GetProperty("id").GetString()!;

        return (client, flujoId, versionId, seccionId);
    }

    [Fact]
    public async Task CreatePregunta_TipoTextoCorto_Retorna201()
    {
        var (client, flujoId, versionId, seccionId) = await CrearFlujoConSeccionAsync("TXT");
        var resp = await client.PostAsJsonAsync(
            $"api/v1/flujos/{flujoId}/versiones/{versionId}/secciones/{seccionId}/preguntas",
            new
            {
                codigo = "p_test_texto",
                texto = "¿Texto de prueba?",
                tipo_control = "texto_corto",
                obligatorio = false
            }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreatePregunta_TipoSiNo_Retorna201()
    {
        var (client, flujoId, versionId, seccionId) = await CrearFlujoConSeccionAsync("SINO");
        var resp = await client.PostAsJsonAsync(
            $"api/v1/flujos/{flujoId}/versiones/{versionId}/secciones/{seccionId}/preguntas",
            new
            {
                codigo = "p_test_sino",
                texto = "¿Es correcto?",
                tipo_control = "si_no",
                obligatorio = true
            }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreatePregunta_TipoFotoUnica_ConConfiguracionFoto_Retorna201()
    {
        var (client, flujoId, versionId, seccionId) = await CrearFlujoConSeccionAsync("FOTO1");
        var resp = await client.PostAsJsonAsync(
            $"api/v1/flujos/{flujoId}/versiones/{versionId}/secciones/{seccionId}/preguntas",
            new
            {
                codigo = "p_foto_medidor",
                texto = "Foto del medidor",
                tipo_control = "foto_unica",
                obligatorio = true,
                foto_nombre = "foto_medidor",
                foto_obligatoria = true,
                foto_depende_de_respuesta = false
            }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreatePregunta_TipoFotosMultiples_ConNombres_Retorna201()
    {
        var (client, flujoId, versionId, seccionId) = await CrearFlujoConSeccionAsync("FOTOM");
        var resp = await client.PostAsJsonAsync(
            $"api/v1/flujos/{flujoId}/versiones/{versionId}/secciones/{seccionId}/preguntas",
            new
            {
                codigo = "p_fotos_multiples",
                texto = "Fotos del servicio",
                tipo_control = "fotos_multiples",
                obligatorio = false,
                foto_nombres_json = "[\"foto_frente\",\"foto_detalle\",\"foto_sello\"]",
                foto_obligatoria = false
            }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreatePregunta_TipoSeleccionUnica_ConOpciones_Retorna201()
    {
        var (client, flujoId, versionId, seccionId) = await CrearFlujoConSeccionAsync("SEL");
        var resp = await client.PostAsJsonAsync(
            $"api/v1/flujos/{flujoId}/versiones/{versionId}/secciones/{seccionId}/preguntas",
            new
            {
                codigo = "p_estado_medidor",
                texto = "Estado del medidor",
                tipo_control = "seleccion_unica",
                obligatorio = true,
                opciones_respuesta_json = "[{\"texto\":\"Bueno\",\"valor\":\"bueno\"},{\"texto\":\"Malo\",\"valor\":\"malo\"}]"
            }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreatePregunta_TipoInvalido_Retorna400()
    {
        var (client, flujoId, versionId, seccionId) = await CrearFlujoConSeccionAsync("INV");
        var resp = await client.PostAsJsonAsync(
            $"api/v1/flujos/{flujoId}/versiones/{versionId}/secciones/{seccionId}/preguntas",
            new
            {
                codigo = "p_invalid",
                texto = "Pregunta inválida",
                tipo_control = "tipo_que_no_existe",
                obligatorio = false
            }, TestFixture.JsonOpts);

        // Enum desconocido → bad request por deserialización
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePregunta_SinCodigo_Retorna400()
    {
        var (client, flujoId, versionId, seccionId) = await CrearFlujoConSeccionAsync("SCD");
        var resp = await client.PostAsJsonAsync(
            $"api/v1/flujos/{flujoId}/versiones/{versionId}/secciones/{seccionId}/preguntas",
            new
            {
                codigo = "",
                texto = "Pregunta sin código",
                tipo_control = "texto_corto"
            }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePregunta_SeccionInexistente_Retorna404()
    {
        var (client, flujoId, versionId, _) = await CrearFlujoConSeccionAsync("SEC404");
        var resp = await client.PostAsJsonAsync(
            $"api/v1/flujos/{flujoId}/versiones/{versionId}/secciones/{Guid.NewGuid()}/preguntas",
            new
            {
                codigo = "p_huerfana",
                texto = "Pregunta huérfana",
                tipo_control = "texto_corto"
            }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreatePregunta_TodosLosTiposControl_Retorna201()
    {
        var (client, flujoId, versionId, seccionId) = await CrearFlujoConSeccionAsync("ALL");
        var tipos = new[]
        {
            "texto_corto", "texto_largo", "entero", "decimal",
            "fecha", "hora", "si_no", "coordenadas", "firma"
        };

        foreach (var tipo in tipos)
        {
            var resp = await client.PostAsJsonAsync(
                $"api/v1/flujos/{flujoId}/versiones/{versionId}/secciones/{seccionId}/preguntas",
                new
                {
                    codigo = $"p_{tipo.Replace('_', '-')}-{Guid.NewGuid():N}",
                    texto = $"Pregunta tipo {tipo}",
                    tipo_control = tipo,
                    obligatorio = false
                }, TestFixture.JsonOpts);

            resp.StatusCode.Should().Be(HttpStatusCode.Created,
                $"tipo_control='{tipo}' debería ser válido");
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// UBICACION TESTS — BLOQUE 4 (Georreferencia en terreno)
// ═══════════════════════════════════════════════════════════════════════════════
public class UbicacionTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _factory;
    public UbicacionTests(TestFixture factory) => _factory = factory;

    [Fact]
    public async Task GetUbicacionOperadores_RetornaLista()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.GetFromJsonAsync<JsonElement>("api/v1/ubicacion/operadores");

        resp.ValueKind.Should().Be(JsonValueKind.Array);
        // Semilla tiene 2 operadores
        resp.GetArrayLength().Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task GetUbicacionOperadores_CamposRequeridos()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.GetFromJsonAsync<JsonElement>("api/v1/ubicacion/operadores");

        var first = resp.EnumerateArray().First();
        first.TryGetProperty("id", out _).Should().BeTrue();
        first.TryGetProperty("codigo_operador", out _).Should().BeTrue();
        first.TryGetProperty("nombre", out _).Should().BeTrue();
        first.TryGetProperty("tiene_ubicacion", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetUbicacionOperadores_SinInspecciones_TieneUbicacionFalse()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.GetFromJsonAsync<JsonElement>("api/v1/ubicacion/operadores");

        // Sin inspecciones en la BD de test → ningún operador tiene ubicación
        foreach (var op in resp.EnumerateArray())
            op.GetProperty("tiene_ubicacion").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task GetUbicacionOperador_ById_RetornaOperador()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.GetAsync($"api/v1/ubicacion/operador/{TestFixture.Operador1Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = await resp.Content.ReadFromJsonAsync<JsonElement>();
        data.GetProperty("id").GetString().Should().Be(TestFixture.Operador1Id.ToString());
        data.GetProperty("codigo_operador").GetString().Should().Be("OP001");
    }

    [Fact]
    public async Task GetUbicacionOperador_IdInexistente_Retorna404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.GetAsync($"api/v1/ubicacion/operador/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUbicacionOperadores_FiltroLocalidad_FiltraPorLocalidad()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.GetFromJsonAsync<JsonElement>(
            "api/v1/ubicacion/operadores?localidad=La%20Serena");

        resp.ValueKind.Should().Be(JsonValueKind.Array);
        // Sin operadores con localidad "La Serena" en seed → vacío
        resp.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetServiciosGeo_RetornaServicios()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.GetFromJsonAsync<JsonElement>("api/v1/ubicacion/servicios");

        resp.ValueKind.Should().Be(JsonValueKind.Array);
        // Seed tiene 10 servicios pero sin coordenadas → vacío (coordenadas son null en seed)
        // El endpoint filtra WHERE CoordenadaY != null
        resp.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetUbicacionOperadores_SinAuth_Retorna401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("api/v1/ubicacion/operadores");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetInspeccionesHoy_RetornaLista()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.GetFromJsonAsync<JsonElement>("api/v1/ubicacion/inspecciones-hoy");

        resp.ValueKind.Should().Be(JsonValueKind.Array);
        // Sin inspecciones en seed → vacío
        resp.GetArrayLength().Should().Be(0);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// INSPECCIONES GEO — BLOQUE 5 (Georreferencia histórica)
// ═══════════════════════════════════════════════════════════════════════════════
public class InspeccionesGeoTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _factory;
    public InspeccionesGeoTests(TestFixture factory) => _factory = factory;

    [Fact]
    public async Task GetGeo_SinFiltros_RetornaPaginado()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.GetFromJsonAsync<JsonElement>("api/v1/inspecciones/geo");

        resp.TryGetProperty("total", out _).Should().BeTrue();
        resp.TryGetProperty("pagina", out _).Should().BeTrue();
        resp.TryGetProperty("items", out _).Should().BeTrue();
        resp.GetProperty("total").GetInt32().Should().Be(0); // sin inspecciones en seed
    }

    [Fact]
    public async Task GetGeo_ConFiltroFechas_RetornaResultado()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var desde = DateTime.Today.AddDays(-7).ToString("yyyy-MM-dd");
        var hasta = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");

        var resp = await client.GetFromJsonAsync<JsonElement>(
            $"api/v1/inspecciones/geo?desde={desde}&hasta={hasta}");

        resp.TryGetProperty("total", out _).Should().BeTrue();
        resp.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetGeo_ConFiltroOperador_RetornaResultado()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.GetFromJsonAsync<JsonElement>(
            $"api/v1/inspecciones/geo?operadorId={TestFixture.Operador1Id}");

        resp.TryGetProperty("total", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetGeo_PaginacionFunciona()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.GetFromJsonAsync<JsonElement>(
            "api/v1/inspecciones/geo?pagina=1&porPagina=5");

        resp.GetProperty("pagina").GetInt32().Should().Be(1);
        resp.GetProperty("por_pagina").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task GetGeo_SinAuth_Retorna401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("api/v1/inspecciones/geo");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// IMPORTACIONES — BUG-03 (Plantilla Excel + endpoint upload)
// ═══════════════════════════════════════════════════════════════════════════════
public class ImportacionesNuevasTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _factory;
    public ImportacionesNuevasTests(TestFixture factory) => _factory = factory;

    [Fact]
    public async Task PlantillaExcel_RetornaArchivoXlsx()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.GetAsync("api/v1/importaciones/plantilla");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType?.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        resp.Content.Headers.ContentDisposition?.FileName.Should().Contain("plantilla");

        // Verificar que hay bytes (el archivo no está vacío)
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task PlantillaExcel_SinAuth_Retorna401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("api/v1/importaciones/plantilla");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UploadExcel_SinArchivo_RetornaErrorDescriptivo()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        // Enviar multipart vacío
        using var content = new MultipartFormDataContent();
        var resp = await client.PostAsync("api/v1/importaciones/upload", content);

        // Debe rechazar con error (400) — no puede importar sin archivo
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// REPORTES NUEVOS — BUG-04 (Productividad Excel)
// ═══════════════════════════════════════════════════════════════════════════════
public class ReportesNuevosTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _factory;
    public ReportesNuevosTests(TestFixture factory) => _factory = factory;

    [Fact]
    public async Task ProductividadExcel_RetornaArchivoXlsx()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.GetAsync("api/v1/reportes/productividad-excel");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType?.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task ProductividadExcel_SinAuth_Retorna401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("api/v1/reportes/productividad-excel");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProductividadExcel_ConFiltroFechas_RetornaArchivo()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var desde = DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd");
        var hasta = DateTime.Today.ToString("yyyy-MM-dd");
        var resp = await client.GetAsync(
            $"api/v1/reportes/productividad-excel?desde={desde}&hasta={hasta}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProductividadExcel_ConFiltroOperador_RetornaArchivo()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.GetAsync(
            $"api/v1/reportes/productividad-excel?operadorId={TestFixture.Operador1Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// FLUJO NUEVO — BUG-01 (creación de flujo, validaciones)
// ═══════════════════════════════════════════════════════════════════════════════
public class FlujoCreacionTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _factory;
    public FlujoCreacionTests(TestFixture factory) => _factory = factory;

    [Fact]
    public async Task CreateFlujo_ConDatosValidos_Retorna201()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.PostAsJsonAsync("api/v1/flujos", new
        {
            nombre = "Flujo Nuevo BUG01",
            descripcion = "Test de creación",
            tipo_inspeccion_id = TestFixture.TipoInsp1Id
        }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var data = await resp.Content.ReadFromJsonAsync<JsonElement>();
        data.GetProperty("nombre").GetString().Should().Be("Flujo Nuevo BUG01");
        data.TryGetProperty("id", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateFlujo_SinNombre_Retorna400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.PostAsJsonAsync("api/v1/flujos", new
        {
            nombre = "",
            tipo_inspeccion_id = TestFixture.TipoInsp1Id
        }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateFlujo_TipoInspeccionInexistente_Retorna400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.PostAsJsonAsync("api/v1/flujos", new
        {
            nombre = "Flujo Sin Tipo",
            tipo_inspeccion_id = Guid.NewGuid() // no existe
        }, TestFixture.JsonOpts);

        // Debe rechazar porque el tipo no existe
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound,
            HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateFlujo_SinAuth_Retorna401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("api/v1/flujos", new
        {
            nombre = "Flujo Sin Auth",
            tipo_inspeccion_id = TestFixture.TipoInsp1Id
        }, TestFixture.JsonOpts);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// UNIT TESTS — SnakeCaseEnumConverter (cobertura de enums nuevos)
// ═══════════════════════════════════════════════════════════════════════════════
public class EnumConverterNuevosTests
{
    private readonly SgiForm.Infrastructure.Persistence.SnakeCaseEnumConverter<SgiForm.Domain.Enums.TipoControl>
        _tipoControlConverter = new();

    [Fact]
    public void TipoControl_FotoUnica_ConvierteASnakeCase()
    {
        var result = _tipoControlConverter.ConvertToProvider(SgiForm.Domain.Enums.TipoControl.FotoUnica);
        result.Should().Be("foto_unica");
    }

    [Fact]
    public void TipoControl_FotosMultiples_ConvierteASnakeCase()
    {
        var result = _tipoControlConverter.ConvertToProvider(SgiForm.Domain.Enums.TipoControl.FotosMultiples);
        result.Should().Be("fotos_multiples");
    }

    [Fact]
    public void TipoControl_SeleccionUnica_RoundTrip()
    {
        var snake = _tipoControlConverter.ConvertToProvider(SgiForm.Domain.Enums.TipoControl.SeleccionUnica);
        var back = _tipoControlConverter.ConvertFromProvider(snake);
        back.Should().Be(SgiForm.Domain.Enums.TipoControl.SeleccionUnica);
    }

    [Fact]
    public void TipoControl_SiNo_RoundTrip()
    {
        var snake = _tipoControlConverter.ConvertToProvider(SgiForm.Domain.Enums.TipoControl.SiNo);
        snake.Should().Be("si_no");
        var back = _tipoControlConverter.ConvertFromProvider("si_no");
        back.Should().Be(SgiForm.Domain.Enums.TipoControl.SiNo);
    }

    [Fact]
    public void TipoControl_TodosLosValores_RoundTripSinExcepcion()
    {
        foreach (var val in Enum.GetValues<SgiForm.Domain.Enums.TipoControl>())
        {
            var snake = _tipoControlConverter.ConvertToProvider(val) as string;
            snake.Should().NotBeNullOrEmpty($"{val} debe tener representación snake_case");
            snake!.Should().MatchRegex("^[a-z_]+$",
                $"{val} → '{snake}' no debería tener mayúsculas");

            var back = _tipoControlConverter.ConvertFromProvider(snake);
            back.Should().Be(val, $"roundtrip de {val} debe producir el mismo enum");
        }
    }

    [Fact]
    public void EstadoFlujoVersion_TodosLosValores_RoundTrip()
    {
        var conv = new SgiForm.Infrastructure.Persistence.SnakeCaseEnumConverter<SgiForm.Domain.Enums.EstadoFlujoVersion>();
        foreach (var val in Enum.GetValues<SgiForm.Domain.Enums.EstadoFlujoVersion>())
        {
            var snake = conv.ConvertToProvider(val) as string;
            snake.Should().NotBeNullOrEmpty();
            var back = conv.ConvertFromProvider(snake!);
            back.Should().Be(val);
        }
    }

    [Fact]
    public void EstadoInspeccion_TodosLosValores_RoundTrip()
    {
        var conv = new SgiForm.Infrastructure.Persistence.SnakeCaseEnumConverter<SgiForm.Domain.Enums.EstadoInspeccion>();
        foreach (var val in Enum.GetValues<SgiForm.Domain.Enums.EstadoInspeccion>())
        {
            var snake = conv.ConvertToProvider(val) as string;
            snake.Should().NotBeNullOrEmpty();
            var back = conv.ConvertFromProvider(snake!);
            back.Should().Be(val);
        }
    }
}

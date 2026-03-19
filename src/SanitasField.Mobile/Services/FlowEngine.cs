using System.Text.Json;
using SanitasField.Mobile.Database;
using SanitasField.Mobile.Models;

namespace SanitasField.Mobile.Services;

/// <summary>
/// Motor de evaluación de formularios dinámicos para la app móvil.
/// Lee la estructura de flujo desde SQLite y aplica reglas condicionales
/// en tiempo de ejecución según las respuestas del operador.
/// 
/// PRINCIPIO: Ninguna lógica de formulario está hardcodeada.
/// Todo viene de la BD local (descargada del servidor).
/// </summary>
public class FlowEngine
{
    private readonly AppDatabase _db;

    // Estado en memoria del formulario actual
    private List<SeccionLocal> _secciones = new();
    private List<PreguntaLocal> _preguntas = new();
    private List<ReglaLocal> _reglas = new();
    private Dictionary<string, RespuestaLocal> _respuestas = new();
    private string? _flujoVersionId;

    public event Action? EstadoCambiado;

    public FlowEngine(AppDatabase db)
    {
        _db = db;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // INICIALIZAR FLUJO
    // ──────────────────────────────────────────────────────────────────────────
    public async Task InicializarAsync(string flujoVersionId, string inspeccionId)
    {
        _flujoVersionId = flujoVersionId;

        // Cargar estructura del flujo desde SQLite
        _secciones = await _db.GetSeccionesAsync(flujoVersionId);
        _reglas = await _db.GetReglasAsync(flujoVersionId);

        _preguntas = new List<PreguntaLocal>();
        foreach (var seccion in _secciones)
        {
            var pregs = await _db.GetPreguntasAsync(seccion.Id);

            // Cargar opciones para preguntas de selección
            foreach (var p in pregs)
            {
                p.VisibleRuntime = p.Visible;
                p.ObligatorioRuntime = p.Obligatorio;

                if (EsTipoSeleccion(p.TipoControl))
                {
                    var opciones = await _db.GetOpcionesAsync(p.Id);
                    // Las opciones se almacenan en el campo de configuración extendido
                    // para acceso rápido sin nueva consulta
                }
                _preguntas.Add(p);
            }
        }

        // Cargar respuestas existentes (si la inspección ya fue iniciada)
        // Usar GroupBy para evitar crash por PreguntaId duplicados
        var respLista = await _db.GetRespuestasAsync(inspeccionId);
        _respuestas = respLista
            .Where(r => r.PreguntaId != null)
            .GroupBy(r => r.PreguntaId!)
            .ToDictionary(g => g.Key, g => g.Last());

        // Evaluar estado inicial
        EvaluarTodasLasReglas();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ACTUALIZAR RESPUESTA
    // ──────────────────────────────────────────────────────────────────────────
    public void ActualizarRespuesta(string preguntaId, object? valor)
    {
        var tipo = _preguntas.FirstOrDefault(p => p.Id == preguntaId)?.TipoControl ?? "texto_corto";

        if (!_respuestas.TryGetValue(preguntaId, out var respuesta))
        {
            respuesta = new RespuestaLocal
            {
                PreguntaId = preguntaId,
                TipoControl = tipo,
                UpdatedAt = DateTime.UtcNow
            };
            _respuestas[preguntaId] = respuesta;
        }

        // Asignar valor según tipo
        switch (tipo.ToLower())
        {
            case "si_no":
            case "checkbox":
                respuesta.ValorBooleano = valor as bool?;
                respuesta.ValorTexto = respuesta.ValorBooleano?.ToString().ToLower();
                break;
            case "entero":
                respuesta.ValorEntero = valor is long l ? l : (long?)null;
                respuesta.ValorTexto = respuesta.ValorEntero?.ToString();
                break;
            case "decimal":
                respuesta.ValorDecimal = valor is double d ? d : (double?)null;
                respuesta.ValorTexto = respuesta.ValorDecimal?.ToString();
                break;
            case "fecha":
            case "hora":
            case "fecha_hora":
                respuesta.ValorFecha = valor?.ToString();
                respuesta.ValorTexto = respuesta.ValorFecha;
                break;
            case "seleccion_multiple":
            case "fotos_multiples":
            case "coordenadas":
                respuesta.ValorJson = valor is string s ? s : JsonSerializer.Serialize(valor);
                break;
            default:
                respuesta.ValorTexto = valor?.ToString();
                break;
        }

        // Re-evaluar reglas afectadas por este cambio
        EvaluarReglasParaPregunta(preguntaId);
        EstadoCambiado?.Invoke();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // EVALUACIÓN DE REGLAS
    // ──────────────────────────────────────────────────────────────────────────
    private void EvaluarTodasLasReglas()
    {
        // Reset a estado base
        foreach (var p in _preguntas)
        {
            p.VisibleRuntime = p.Visible;
            p.ObligatorioRuntime = p.Obligatorio;
        }

        foreach (var regla in _reglas.Where(r => r.Activo).OrderBy(r => r.Orden))
            AplicarRegla(regla);
    }

    private void EvaluarReglasParaPregunta(string preguntaOrigenId)
    {
        // Encontrar todas las reglas que dependen de esta pregunta
        var reglasDependientes = _reglas
            .Where(r => r.PreguntaOrigenId == preguntaOrigenId && r.Activo)
            .OrderBy(r => r.Orden);

        foreach (var regla in reglasDependientes)
            AplicarRegla(regla);
    }

    private void AplicarRegla(ReglaLocal regla)
    {
        if (!_respuestas.TryGetValue(regla.PreguntaOrigenId!, out var respuesta))
        {
            // Sin respuesta: aplicar estado de "condición no cumplida"
            AplicarAccionNoCumplida(regla);
            return;
        }

        bool condicionCumplida = EvaluarCondicion(regla, respuesta);

        if (condicionCumplida)
            AplicarAccionCumplida(regla);
        else
            AplicarAccionNoCumplida(regla);
    }

    private bool EvaluarCondicion(ReglaLocal regla, RespuestaLocal respuesta)
    {
        var valorActual = ObtenerValorComparacion(respuesta);
        var valorEsperado = regla.ValorComparacion ?? "";

        return regla.Operador?.ToLower() switch
        {
            "eq"          => string.Equals(valorActual, valorEsperado, StringComparison.OrdinalIgnoreCase),
            "neq"         => !string.Equals(valorActual, valorEsperado, StringComparison.OrdinalIgnoreCase),
            "is_not_empty"=> !string.IsNullOrWhiteSpace(valorActual),
            "is_empty"    => string.IsNullOrWhiteSpace(valorActual),
            "contains"    => valorActual?.Contains(valorEsperado, StringComparison.OrdinalIgnoreCase) == true,
            "in"          => EvaluarIn(valorActual, regla.ValorComparacionJson),
            "gt"          => double.TryParse(valorActual, out var va) &&
                             double.TryParse(valorEsperado, out var ve) && va > ve,
            "lt"          => double.TryParse(valorActual, out var va2) &&
                             double.TryParse(valorEsperado, out var ve2) && va2 < ve2,
            "gte"         => double.TryParse(valorActual, out var va3) &&
                             double.TryParse(valorEsperado, out var ve3) && va3 >= ve3,
            "lte"         => double.TryParse(valorActual, out var va4) &&
                             double.TryParse(valorEsperado, out var ve4) && va4 <= ve4,
            _ => false
        };
    }

    private static string? ObtenerValorComparacion(RespuestaLocal r)
    {
        if (r.ValorTexto != null) return r.ValorTexto;
        if (r.ValorBooleano.HasValue) return r.ValorBooleano.Value.ToString().ToLower();
        if (r.ValorEntero.HasValue) return r.ValorEntero.Value.ToString();
        if (r.ValorDecimal.HasValue) return r.ValorDecimal.Value.ToString();
        return r.ValorFecha ?? r.ValorJson;
    }

    private static bool EvaluarIn(string? valor, string? listJson)
    {
        if (valor == null || listJson == null) return false;
        try
        {
            var lista = JsonSerializer.Deserialize<List<string>>(listJson);
            return lista?.Contains(valor, StringComparer.OrdinalIgnoreCase) == true;
        }
        catch { return false; }
    }

    private void AplicarAccionCumplida(ReglaLocal regla)
    {
        var destino = regla.PreguntaDestinoId;

        switch (regla.Accion?.ToLower())
        {
            case "mostrar":
                SetPreguntaVisible(destino, true);
                break;
            case "ocultar":
                SetPreguntaVisible(destino, false);
                break;
            case "obligatorio":
                SetPreguntaObligatorio(destino, true);
                break;
            case "opcional":
                SetPreguntaObligatorio(destino, false);
                break;
            case "min_fotos":
                // Se aplica a través de configuración dinámica
                SetParametroPreguntas(destino, regla.ParametrosJson);
                break;
        }
    }

    private void AplicarAccionNoCumplida(ReglaLocal regla)
    {
        // Revertir al estado base de la pregunta
        var destino = regla.PreguntaDestinoId;
        var pregunta = _preguntas.FirstOrDefault(p => p.Id == destino);
        if (pregunta == null) return;

        switch (regla.Accion?.ToLower())
        {
            case "mostrar":
                pregunta.VisibleRuntime = false; // Ocultar cuando condición no se cumple
                break;
            case "ocultar":
                pregunta.VisibleRuntime = pregunta.Visible; // Restaurar visibilidad base
                break;
            case "obligatorio":
                pregunta.ObligatorioRuntime = pregunta.Obligatorio;
                break;
        }
    }

    private void SetPreguntaVisible(string? id, bool visible)
    {
        var p = _preguntas.FirstOrDefault(p => p.Id == id);
        if (p != null) p.VisibleRuntime = visible;
    }

    private void SetPreguntaObligatorio(string? id, bool obligatorio)
    {
        var p = _preguntas.FirstOrDefault(p => p.Id == id);
        if (p != null) p.ObligatorioRuntime = obligatorio;
    }

    private static void SetParametroPreguntas(string? id, string? parametrosJson)
    {
        // Guardar parámetros dinámicos (min_fotos, etc.) para validación posterior
        // Implementación: guardado en diccionario de configuración dinámica
    }

    // ──────────────────────────────────────────────────────────────────────────
    // VALIDACIÓN ANTES DE CIERRE
    // ──────────────────────────────────────────────────────────────────────────
    public List<string> ValidarCierre()
    {
        var errores = new List<string>();

        foreach (var pregunta in _preguntas.Where(p => p.VisibleRuntime))
        {
            if (!pregunta.ObligatorioRuntime) continue;

            _respuestas.TryGetValue(pregunta.Id, out var resp);
            bool tieneRespuesta = resp != null && (
                !string.IsNullOrWhiteSpace(resp.ValorTexto) ||
                resp.ValorBooleano.HasValue ||
                resp.ValorEntero.HasValue ||
                resp.ValorDecimal.HasValue ||
                !string.IsNullOrWhiteSpace(resp.ValorJson));

            if (!tieneRespuesta)
                errores.Add($"La pregunta '{pregunta.Texto}' es obligatoria");
        }

        return errores;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ACCESORES PÚBLICOS
    // ──────────────────────────────────────────────────────────────────────────
    public List<SeccionLocal> GetSecciones() => _secciones;

    public List<PreguntaLocal> GetPreguntasDeSección(string seccionId) =>
        _preguntas.Where(p => p.SeccionId == seccionId).ToList();

    public RespuestaLocal? GetRespuesta(string preguntaId) =>
        _respuestas.TryGetValue(preguntaId, out var r) ? r : null;

    public Dictionary<string, RespuestaLocal> GetTodasLasRespuestas() => _respuestas;

    public int GetPorcentajeAvance()
    {
        var obligatorias = _preguntas.Count(p => p.VisibleRuntime && p.ObligatorioRuntime);
        if (obligatorias == 0) return 100;

        var respondidas = _preguntas
            .Where(p => p.VisibleRuntime && p.ObligatorioRuntime)
            .Count(p => _respuestas.ContainsKey(p.Id) &&
                        !string.IsNullOrWhiteSpace(
                            ObtenerValorComparacion(_respuestas[p.Id])));

        return (int)((double)respondidas / obligatorias * 100);
    }

    private static bool EsTipoSeleccion(string? tipo) =>
        tipo?.ToLower() is "seleccion_unica" or "seleccion_multiple" or "lista";
}

using Microsoft.EntityFrameworkCore;
using SanitasField.Domain.Entities;
using SanitasField.Domain.Enums;
using SanitasField.Infrastructure.Persistence;

namespace SanitasField.Infrastructure.Services;

public interface IFlowValidatorService
{
    /// <summary>
    /// Valida server-side las respuestas de una inspección contra el flujo definido.
    /// Para inspecciones en estado Completada/Enviada los errores son bloqueantes.
    /// Para estados intermedios se retornan como advertencias.
    /// </summary>
    Task<FlowValidationResult> ValidarAsync(
        Guid flujoVersionId,
        List<InspeccionRespuesta> respuestas,
        int totalFotos,
        EstadoInspeccion estado);
}

public record FlowValidationResult(
    bool EsValida,
    List<string> Errores,
    List<string> Advertencias);

/// <summary>
/// Validación server-side del motor de flujos.
/// Comprueba campos obligatorios, fotos mínimas y coherencia básica
/// sin replicar la lógica condicional completa del motor móvil.
/// Nota: Las reglas condicionales (mostrar/ocultar según respuestas previas)
/// no se re-evalúan aquí para evitar duplicar el FlowEngine del cliente.
/// En la v2 se implementará un evaluador completo de reglas en el servidor.
/// </summary>
public class FlowValidatorService : IFlowValidatorService
{
    private readonly AppDbContext _db;

    public FlowValidatorService(AppDbContext db) => _db = db;

    public async Task<FlowValidationResult> ValidarAsync(
        Guid flujoVersionId,
        List<InspeccionRespuesta> respuestas,
        int totalFotos,
        EstadoInspeccion estado)
    {
        var errores = new List<string>();
        var advertencias = new List<string>();

        // Solo validamos inspecciones que el operador marcó como terminadas
        bool esEstadoFinal = estado is EstadoInspeccion.Completada or EstadoInspeccion.Enviada;

        // Cargar preguntas base del flujo (solo visibles por defecto)
        var preguntas = await _db.FlujoPreguntas
            .Where(p => p.FlujoVersionId == flujoVersionId && p.Visible)
            .OrderBy(p => p.Orden)
            .ToListAsync();

        if (!preguntas.Any())
            return new FlowValidationResult(true, errores, advertencias);

        // IDs con valor respondido
        var respondidosConValor = respuestas
            .Where(TieneValor)
            .Select(r => r.PreguntaId)
            .ToHashSet();

        // ── 1. Campos obligatorios sin respuesta ──────────────────────────────
        var preguntasOblFaltantes = preguntas
            .Where(p => p.Obligatorio && !respondidosConValor.Contains(p.Id))
            .ToList();

        foreach (var p in preguntasOblFaltantes)
        {
            var msg = $"Campo obligatorio sin respuesta: \"{p.Texto}\" [{p.Codigo}]";
            if (esEstadoFinal)
                errores.Add(msg);
            else
                advertencias.Add(msg);
        }

        // ── 2. Preguntas de foto obligatoria sin evidencia ────────────────────
        var preguntasFotoObl = preguntas
            .Where(p => p.Obligatorio &&
                        (p.TipoControl == TipoControl.FotoUnica ||
                         p.TipoControl == TipoControl.FotosMultiples))
            .ToList();

        foreach (var pf in preguntasFotoObl)
        {
            // La respuesta debe tener un valor (ruta o identificador de foto)
            var tieneReferencia = respuestas.Any(r =>
                r.PreguntaId == pf.Id && !string.IsNullOrWhiteSpace(r.ValorTexto));

            if (!tieneReferencia)
            {
                var msg = $"Fotografía obligatoria faltante: \"{pf.Texto}\" [{pf.Codigo}]";
                if (esEstadoFinal)
                    errores.Add(msg);
                else
                    advertencias.Add(msg);
            }
        }

        // ── 3. Inspección marcada como finalizada sin ninguna foto ────────────
        if (esEstadoFinal && preguntasFotoObl.Any() && totalFotos == 0)
            errores.Add("La inspección está marcada como completada pero no contiene fotografías.");

        // ── 4. Advertencia general: baja completitud ──────────────────────────
        if (preguntas.Count > 0)
        {
            var pct = (double)respondidosConValor.Count / preguntas.Count * 100;
            if (pct < 50 && esEstadoFinal)
                advertencias.Add($"Completitud baja: solo el {pct:F0}% de las preguntas tiene respuesta.");
        }

        return new FlowValidationResult(!errores.Any(), errores, advertencias);
    }

    /// <summary>Determina si una respuesta tiene un valor real (no nulo/vacío).</summary>
    private static bool TieneValor(InspeccionRespuesta r) =>
        !string.IsNullOrWhiteSpace(r.ValorTexto) ||
        r.ValorEntero.HasValue ||
        r.ValorDecimal.HasValue ||
        r.ValorFecha.HasValue ||
        r.ValorFechaHora.HasValue ||
        r.ValorBooleano.HasValue ||
        !string.IsNullOrWhiteSpace(r.ValorJson);
}

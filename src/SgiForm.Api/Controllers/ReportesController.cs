using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SgiForm.Domain.Enums;
using SgiForm.Infrastructure.Persistence;

namespace SgiForm.Api.Controllers;

[ApiController]
[Route("api/v1/reportes")]
[Authorize]
public class ReportesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReportesController(AppDbContext db) => _db = db;
    private Guid EmpresaId => Guid.Parse(User.FindFirst("empresa_id")!.Value);

    /// <summary>
    /// Exportar asignaciones/inspecciones a Excel con columnas dinámicas por
    /// pregunta del flujo (pivota respuestas de cada inspección).
    /// </summary>
    [HttpGet("excel")]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] string? estado,
        [FromQuery] Guid? operadorId,
        [FromQuery] string? localidad,
        [FromQuery] string? ruta,
        [FromQuery] Guid? tipoInspeccionId,
        [FromQuery] DateTime? desdeFecha,
        [FromQuery] DateTime? hastaFecha)
    {
        var q = _db.AsignacionesInspeccion
            .Include(a => a.ServicioInspeccion)
            .Include(a => a.Operador)
            .Include(a => a.TipoInspeccion)
            .Include(a => a.Inspeccion)
                .ThenInclude(i => i!.Respuestas)
            .Where(a => a.EmpresaId == EmpresaId && a.DeletedAt == null);

        if (!string.IsNullOrEmpty(estado) && Enum.TryParse<EstadoAsignacion>(estado, true, out var est))
            q = q.Where(a => a.Estado == est);
        if (operadorId.HasValue)
            q = q.Where(a => a.OperadorId == operadorId.Value);
        if (!string.IsNullOrEmpty(localidad))
            q = q.Where(a => a.ServicioInspeccion.Localidad == localidad);
        if (!string.IsNullOrEmpty(ruta))
            q = q.Where(a => a.ServicioInspeccion.Ruta == ruta);
        if (tipoInspeccionId.HasValue)
            q = q.Where(a => a.TipoInspeccionId == tipoInspeccionId.Value);
        if (desdeFecha.HasValue)
        {
            var desde = DateTime.SpecifyKind(desdeFecha.Value.Date, DateTimeKind.Utc);
            q = q.Where(a => a.FechaAsignacion >= desde);
        }
        if (hastaFecha.HasValue)
        {
            var hasta = DateTime.SpecifyKind(hastaFecha.Value.Date.AddDays(1), DateTimeKind.Utc);
            q = q.Where(a => a.FechaAsignacion < hasta);
        }

        var datos = await q.OrderBy(a => a.ServicioInspeccion.Localidad)
            .ThenBy(a => a.ServicioInspeccion.Ruta)
            .ThenBy(a => a.ServicioInspeccion.IdServicio)
            .ToListAsync();

        // Preguntas dinámicas de los flujos involucrados (excluye fotos)
        var flujoVersionIds = datos.Select(a => a.FlujoVersionId).Distinct().ToList();
        var preguntas = await _db.FlujoPreguntas
            .Where(p => flujoVersionIds.Contains(p.FlujoVersionId)
                        && p.TipoControl != TipoControl.FotoUnica
                        && p.TipoControl != TipoControl.FotosMultiples
                        && p.TipoControl != TipoControl.Etiqueta)
            .OrderBy(p => p.Orden)
            .ThenBy(p => p.Codigo)
            .Select(p => new PreguntaColumna
            {
                Id = p.Id,
                Codigo = p.Codigo,
                Texto = p.Texto,
                TipoControl = p.TipoControl,
                Orden = p.Orden
            })
            .ToListAsync();

        // Deduplicar por código (distintas versiones del mismo flujo pueden repetir código)
        var preguntasUnicas = preguntas
            .GroupBy(p => p.Codigo)
            .Select(g => g.First())
            .OrderBy(p => p.Orden)
            .ThenBy(p => p.Codigo)
            .ToList();

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Inspecciones");

        var headersFijos = new[] {
            "ID Servicio", "Nro Medidor", "Marca", "Diámetro",
            "Dirección", "Cliente", "Localidad", "Ruta", "Lote",
            "Operador", "Tipo Inspección", "Estado Asignación",
            "Estado Inspección", "Fecha Asignación", "Fecha Inicio",
            "Fecha Fin", "GPS Lat", "GPS Lon", "Fotos", "Observaciones"
        };

        for (int i = 0; i < headersFijos.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headersFijos[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
        }

        // Headers dinámicos de preguntas
        int colBase = headersFijos.Length;
        for (int i = 0; i < preguntasUnicas.Count; i++)
        {
            var p = preguntasUnicas[i];
            var col = colBase + i + 1;
            ws.Cell(1, col).Value = string.IsNullOrWhiteSpace(p.Texto) ? p.Codigo : p.Texto;
            ws.Cell(1, col).Style.Font.Bold = true;
            ws.Cell(1, col).Style.Fill.BackgroundColor = XLColor.LightGreen;
            ws.Cell(1, col).GetComment().AddText($"Código: {p.Codigo}\nTipo: {p.TipoControl}");
        }

        // Índice para mapear rápidamente código -> col
        var codigoToCol = preguntasUnicas
            .Select((p, idx) => new { p.Codigo, Col = colBase + idx + 1 })
            .ToDictionary(x => x.Codigo, x => x.Col);

        // Índice pregunta_id -> código (para resolver respuestas)
        var preguntaIdToCodigo = preguntas
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.First().Codigo);

        int row = 2;
        foreach (var a in datos)
        {
            var s = a.ServicioInspeccion;
            var insp = a.Inspeccion;
            ws.Cell(row, 1).Value = s.IdServicio;
            ws.Cell(row, 2).Value = s.NumeroMedidor ?? "";
            ws.Cell(row, 3).Value = s.Marca ?? "";
            ws.Cell(row, 4).Value = s.Diametro ?? "";
            ws.Cell(row, 5).Value = s.Direccion ?? "";
            ws.Cell(row, 6).Value = s.NombreCliente ?? "";
            ws.Cell(row, 7).Value = s.Localidad ?? "";
            ws.Cell(row, 8).Value = s.Ruta ?? "";
            ws.Cell(row, 9).Value = s.Lote ?? "";
            ws.Cell(row, 10).Value = a.Operador.Nombre + " " + a.Operador.Apellido;
            ws.Cell(row, 11).Value = a.TipoInspeccion.Nombre;
            ws.Cell(row, 12).Value = a.Estado.ToString();
            ws.Cell(row, 13).Value = insp?.Estado.ToString() ?? "Sin inspección";
            ws.Cell(row, 14).Value = a.FechaAsignacion.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            ws.Cell(row, 15).Value = insp?.FechaInicio?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "";
            ws.Cell(row, 16).Value = insp?.FechaFin?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "";
            ws.Cell(row, 17).Value = insp?.CoordYFin?.ToString() ?? "";
            ws.Cell(row, 18).Value = insp?.CoordXFin?.ToString() ?? "";
            ws.Cell(row, 19).Value = insp?.TotalFotografias ?? 0;
            ws.Cell(row, 20).Value = a.Observaciones ?? "";

            // Respuestas dinámicas
            if (insp?.Respuestas != null)
            {
                foreach (var r in insp.Respuestas)
                {
                    if (!preguntaIdToCodigo.TryGetValue(r.PreguntaId, out var codigo)) continue;
                    if (!codigoToCol.TryGetValue(codigo, out var col)) continue;
                    ws.Cell(row, col).Value = FormatearValorRespuesta(r);
                }
            }

            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"inspecciones_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    /// <summary>Convierte una respuesta al formato string más apropiado según su TipoControl.</summary>
    private static string FormatearValorRespuesta(Domain.Entities.InspeccionRespuesta r)
    {
        return r.TipoControl switch
        {
            TipoControl.TextoCorto or TipoControl.TextoLargo or TipoControl.Lista
                or TipoControl.SeleccionUnica or TipoControl.QrCodigo or TipoControl.Firma
                or TipoControl.Archivo or TipoControl.Calculado
                => r.ValorTexto ?? "",
            TipoControl.Entero       => r.ValorEntero?.ToString() ?? "",
            TipoControl.Decimal      => r.ValorDecimal?.ToString("0.##") ?? "",
            TipoControl.Fecha        => r.ValorFecha?.ToString("dd/MM/yyyy") ?? "",
            TipoControl.Hora         => r.ValorHora?.ToString("HH:mm") ?? "",
            TipoControl.FechaHora    => r.ValorFechaHora?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "",
            TipoControl.SiNo or TipoControl.Checkbox
                => r.ValorBooleano.HasValue ? (r.ValorBooleano.Value ? "Sí" : "No") : "",
            TipoControl.SeleccionMultiple => r.ValorJson ?? "",
            TipoControl.Coordenadas       => r.ValorJson ?? (r.ValorTexto ?? ""),
            _ => r.ValorTexto ?? r.ValorJson ?? ""
        };
    }

    private class PreguntaColumna
    {
        public Guid Id { get; set; }
        public string Codigo { get; set; } = "";
        public string Texto { get; set; } = "";
        public TipoControl TipoControl { get; set; }
        public int Orden { get; set; }
    }

    /// <summary>Exportar productividad por operador a Excel (tabla visible en Reportes)</summary>
    [HttpGet("productividad-excel")]
    public async Task<IActionResult> ExportProductividadExcel(
        [FromQuery] string? estado,
        [FromQuery] Guid? operadorId,
        [FromQuery] string? localidad,
        [FromQuery] string? ruta)
    {
        var q = _db.Operadores
            .Include(o => o.Asignaciones)
            .Where(o => o.EmpresaId == EmpresaId && o.Activo && o.DeletedAt == null);

        if (operadorId.HasValue) q = q.Where(o => o.Id == operadorId.Value);
        if (!string.IsNullOrEmpty(localidad)) q = q.Where(o => o.Localidad == localidad);
        if (!string.IsNullOrEmpty(ruta)) q = q.Where(o => o.Zona == ruta);

        var operadores = await q.OrderBy(o => o.Nombre).ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Productividad");

        var headers = new[] { "Código", "Operador", "Zona", "Pendientes", "En Ejecución", "Finalizadas", "Sincronizadas", "Total", "Última Sync" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
        }

        int row = 2;
        foreach (var o in operadores)
        {
            var asigs = o.Asignaciones.Where(a => a.DeletedAt == null).ToList();
            ws.Cell(row, 1).Value = o.CodigoOperador;
            ws.Cell(row, 2).Value = o.Nombre + " " + o.Apellido;
            ws.Cell(row, 3).Value = o.Zona ?? "";
            ws.Cell(row, 4).Value = asigs.Count(a => a.Estado == EstadoAsignacion.Pendiente);
            ws.Cell(row, 5).Value = asigs.Count(a => a.Estado == EstadoAsignacion.EnEjecucion);
            ws.Cell(row, 6).Value = asigs.Count(a => a.Estado == EstadoAsignacion.Finalizada);
            ws.Cell(row, 7).Value = asigs.Count(a => a.Estado == EstadoAsignacion.Sincronizada);
            ws.Cell(row, 8).Value = asigs.Count;
            ws.Cell(row, 9).Value = o.FechaUltimaSync?.ToString("dd-MM-yyyy HH:mm") ?? "Nunca";
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"reporte_productividad_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    /// <summary>Reporte por operador</summary>
    [HttpGet("por-operador")]
    public async Task<IActionResult> ReportePorOperador()
    {
        var data = await _db.Operadores
            .Where(o => o.EmpresaId == EmpresaId && o.Activo && o.DeletedAt == null)
            .Select(o => new
            {
                o.CodigoOperador,
                nombre = o.Nombre + " " + o.Apellido,
                o.Zona, o.Localidad,
                pendientes    = o.Asignaciones.Count(a => a.Estado == EstadoAsignacion.Pendiente && a.DeletedAt == null),
                en_ejecucion  = o.Asignaciones.Count(a => a.Estado == EstadoAsignacion.EnEjecucion && a.DeletedAt == null),
                finalizadas   = o.Asignaciones.Count(a => a.Estado == EstadoAsignacion.Finalizada && a.DeletedAt == null),
                sincronizadas = o.Asignaciones.Count(a => a.Estado == EstadoAsignacion.Sincronizada && a.DeletedAt == null),
                total         = o.Asignaciones.Count(a => a.DeletedAt == null),
                o.FechaUltimaSync
            })
            .OrderBy(o => o.nombre)
            .ToListAsync();

        return Ok(data);
    }

    /// <summary>Reporte por localidad/ruta/lote</summary>
    [HttpGet("por-localidad")]
    public async Task<IActionResult> ReportePorLocalidad()
    {
        var data = await _db.ServiciosInspeccion
            .Where(s => s.EmpresaId == EmpresaId && s.Activo)
            .GroupBy(s => new { s.Localidad, s.Ruta, s.Lote })
            .Select(g => new
            {
                localidad = g.Key.Localidad ?? "Sin localidad",
                ruta = g.Key.Ruta ?? "Sin ruta",
                lote = g.Key.Lote ?? "Sin lote",
                total_servicios = g.Count(),
                con_asignacion = g.Count(s => s.TieneAsignacion),
                sin_asignar = g.Count(s => !s.TieneAsignacion)
            })
            .OrderBy(g => g.localidad).ThenBy(g => g.ruta).ThenBy(g => g.lote)
            .ToListAsync();

        return Ok(data);
    }
}

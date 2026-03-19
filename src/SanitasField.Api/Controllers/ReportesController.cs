using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SanitasField.Domain.Enums;
using SanitasField.Infrastructure.Persistence;

namespace SanitasField.Api.Controllers;

[ApiController]
[Route("api/v1/reportes")]
[Authorize]
public class ReportesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReportesController(AppDbContext db) => _db = db;
    private Guid EmpresaId => Guid.Parse(User.FindFirst("empresa_id")!.Value);

    /// <summary>Exportar asignaciones/inspecciones a Excel</summary>
    [HttpGet("excel")]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] string? estado,
        [FromQuery] Guid? operadorId,
        [FromQuery] string? localidad,
        [FromQuery] string? ruta)
    {
        var q = _db.AsignacionesInspeccion
            .Include(a => a.ServicioInspeccion)
            .Include(a => a.Operador)
            .Include(a => a.TipoInspeccion)
            .Include(a => a.Inspeccion)
            .Where(a => a.EmpresaId == EmpresaId && a.DeletedAt == null);

        if (!string.IsNullOrEmpty(estado) && Enum.TryParse<EstadoAsignacion>(estado, true, out var est))
            q = q.Where(a => a.Estado == est);
        if (operadorId.HasValue)
            q = q.Where(a => a.OperadorId == operadorId.Value);
        if (!string.IsNullOrEmpty(localidad))
            q = q.Where(a => a.ServicioInspeccion.Localidad == localidad);
        if (!string.IsNullOrEmpty(ruta))
            q = q.Where(a => a.ServicioInspeccion.Ruta == ruta);

        var datos = await q.OrderBy(a => a.ServicioInspeccion.Localidad)
            .ThenBy(a => a.ServicioInspeccion.Ruta)
            .ThenBy(a => a.ServicioInspeccion.IdServicio)
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Inspecciones");

        // Headers
        var headers = new[] {
            "ID Servicio", "Nro Medidor", "Marca", "Diámetro",
            "Dirección", "Cliente", "Localidad", "Ruta", "Lote",
            "Operador", "Tipo Inspección", "Estado Asignación",
            "Estado Inspección", "Fecha Asignación", "Fecha Inicio",
            "Fecha Fin", "GPS Lat", "GPS Lon", "Fotos", "Observaciones"
        };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
        }

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
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"inspecciones_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SgiForm.Infrastructure.Persistence;
using SgiForm.Infrastructure.Services;

namespace SgiForm.Api.Controllers;

/// <summary>
/// Controller para importación de servicios desde archivos Excel.
/// Flujo: upload → preview → confirmar → procesar
/// </summary>
[ApiController]
[Route("api/v1/importaciones")]
[Authorize]
public class ImportacionController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IExcelImportService _importService;
    private readonly IConfiguration _config;

    public ImportacionController(AppDbContext db, IExcelImportService importService, IConfiguration config)
    {
        _db = db;
        _importService = importService;
        _config = config;
    }

    private Guid EmpresaId =>
        Guid.Parse(User.FindFirst("empresa_id")!.Value);

    private Guid UsuarioId =>
        Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    // ──────────────────────────────────────────────────────────────────────────
    // POST /importaciones/upload — subir archivo Excel
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)] // 50 MB máximo
    public async Task<IActionResult> Upload(
        IFormFile archivo,
        [FromForm] Guid? tipoInspeccionId,
        [FromForm] Guid? flujoVersionId)
    {
        if (archivo == null || archivo.Length == 0)
            return BadRequest(new { error = "Debe adjuntar un archivo" });

        var extension = Path.GetExtension(archivo.FileName).ToLower();
        if (extension != ".xlsx" && extension != ".xls")
            return BadRequest(new { error = "Solo se aceptan archivos Excel (.xlsx, .xls)" });

        var maxMb = int.Parse(_config["Storage:MaxPhotoMb"] ?? "50");
        if (archivo.Length > maxMb * 1_000_000)
            return BadRequest(new { error = $"El archivo supera el límite de {maxMb} MB" });

        await using var stream = archivo.OpenReadStream();
        var lote = await _importService.IniciarImportacionAsync(
            EmpresaId, UsuarioId, stream,
            archivo.FileName, tipoInspeccionId, flujoVersionId);

        return CreatedAtAction(nameof(GetLote), new { loteId = lote.Id },
            new
            {
                lote_id = lote.Id,
                nombre_original = lote.NombreOriginal,
                total_filas = lote.TotalFilas,
                estado = lote.Estado.ToString().ToLower(),
                mensaje = "Archivo recibido. Use /preview para ver los datos y /confirmar para procesar."
            });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /importaciones/{loteId}/preview — preview antes de confirmar
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("{loteId:guid}/preview")]
    public async Task<IActionResult> Preview(
        Guid loteId,
        [FromQuery] int pagina = 1,
        [FromQuery] int porPagina = 20)
    {
        // Verificar que el lote pertenece a la empresa
        var loteExiste = await _db.ImportacionLotes.AnyAsync(l =>
            l.Id == loteId && l.EmpresaId == EmpresaId);

        if (!loteExiste) return NotFound();

        var preview = await _importService.GetPreviewAsync(loteId, pagina, porPagina);
        return Ok(preview);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /importaciones/{loteId}/confirmar — procesar lote
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost("{loteId:guid}/confirmar")]
    public async Task<IActionResult> Confirmar(Guid loteId)
    {
        var lote = await _db.ImportacionLotes.FirstOrDefaultAsync(l =>
            l.Id == loteId && l.EmpresaId == EmpresaId);

        if (lote == null) return NotFound();

        if (lote.Estado != Domain.Enums.EstadoImportacion.Pendiente)
            return BadRequest(new { error = $"El lote está en estado '{lote.Estado}' y no puede ser procesado nuevamente. Use /reprocesar si corresponde." });

        var loteActualizado = await _importService.ProcesarLoteAsync(loteId);

        return Ok(new
        {
            lote_id = loteActualizado.Id,
            estado = loteActualizado.Estado.ToString().ToLower(),
            total_filas = loteActualizado.TotalFilas,
            filas_validas = loteActualizado.FilasValidas,
            filas_error = loteActualizado.FilasError,
            error_general = loteActualizado.ErrorGeneral
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /importaciones — listar lotes
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pagina = 1,
        [FromQuery] int porPagina = 25)
    {
        var q = _db.ImportacionLotes
            .Include(l => l.Usuario)
            .Where(l => l.EmpresaId == EmpresaId);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(l => l.CreatedAt)
            .Skip((pagina - 1) * porPagina)
            .Take(porPagina)
            .Select(l => new
            {
                l.Id,
                l.NombreOriginal,
                l.TotalFilas,
                l.FilasValidas,
                l.FilasError,
                estado = l.Estado.ToString().ToLower(),
                l.ProcesadoEn,
                l.CreatedAt,
                usuario = l.Usuario.Nombre + " " + l.Usuario.Apellido
            })
            .ToListAsync();

        return Ok(new { total, pagina, por_pagina = porPagina, items });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /importaciones/{loteId}
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("{loteId:guid}")]
    public async Task<IActionResult> GetLote(Guid loteId)
    {
        var lote = await _db.ImportacionLotes
            .Include(l => l.Usuario)
            .Where(l => l.Id == loteId && l.EmpresaId == EmpresaId)
            .Select(l => new
            {
                l.Id,
                l.NombreOriginal,
                l.HashArchivo,
                l.TotalFilas,
                l.FilasValidas,
                l.FilasError,
                l.FilasOmitidas,
                estado = l.Estado.ToString().ToLower(),
                l.Notas,
                l.ErrorGeneral,
                l.ProcesadoEn,
                l.CreatedAt,
                usuario = l.Usuario.Nombre + " " + l.Usuario.Apellido,
                tipo_inspeccion_id = l.TipoInspeccionId,
                flujo_version_id = l.FlujoVersionId
            })
            .FirstOrDefaultAsync();

        if (lote == null) return NotFound();
        return Ok(lote);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /importaciones/{loteId}/errores
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("{loteId:guid}/errores")]
    public async Task<IActionResult> GetErrores(Guid loteId, [FromQuery] int pagina = 1, [FromQuery] int porPagina = 50)
    {
        var loteExiste = await _db.ImportacionLotes.AnyAsync(l =>
            l.Id == loteId && l.EmpresaId == EmpresaId);
        if (!loteExiste) return NotFound();

        var total = await _db.ImportacionDetalles.CountAsync(d => d.LoteId == loteId && d.Estado == "error");
        var items = await _db.ImportacionDetalles
            .Where(d => d.LoteId == loteId)
            .OrderBy(d => d.NumeroFila)
            .Skip((pagina - 1) * porPagina)
            .Take(porPagina)
            .ToListAsync();

        return Ok(new { total, pagina, por_pagina = porPagina, items });
    }
}

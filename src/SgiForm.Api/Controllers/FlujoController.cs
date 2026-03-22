using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SgiForm.Domain.Entities;
using SgiForm.Domain.Enums;
using SgiForm.Infrastructure.Persistence;

namespace SgiForm.Api.Controllers;

/// <summary>
/// Controller para gestionar flujos de inspección.
/// Un flujo tiene versiones inmutables; una vez publicada una versión, no se modifica.
/// </summary>
[ApiController]
[Route("api/v1/flujos")]
[Authorize]
public class FlujoController : ControllerBase
{
    private readonly AppDbContext _db;

    public FlujoController(AppDbContext db) => _db = db;

    private Guid EmpresaId =>
        Guid.Parse(User.FindFirst("empresa_id")!.Value);

    // ──────────────────────────────────────────────────────────────────────────
    // GET /flujos
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? tipoInspeccionId, [FromQuery] bool? activo)
    {
        var q = _db.Flujos
            .Include(f => f.TipoInspeccion)
            .Include(f => f.Versiones)
            .Where(f => f.EmpresaId == EmpresaId && f.DeletedAt == null);

        if (tipoInspeccionId.HasValue)
            q = q.Where(f => f.TipoInspeccionId == tipoInspeccionId);

        if (activo.HasValue)
            q = q.Where(f => f.Activo == activo.Value);

        var items = await q
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                f.Id,
                f.Nombre,
                f.Descripcion,
                f.Activo,
                tipo_inspeccion = f.TipoInspeccion == null ? null : new { f.TipoInspeccion.Id, f.TipoInspeccion.Nombre },
                versiones = f.Versiones.Select(v => new
                {
                    v.Id, v.NumeroVersion, estado = v.Estado.ToString().ToLower(), v.PublicadoEn
                }).ToList()
            })
            .ToListAsync();

        return Ok(items);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /flujos/{id}
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var flujo = await _db.Flujos
            .Include(f => f.TipoInspeccion)
            .Include(f => f.Versiones)
            .Where(f => f.Id == id && f.EmpresaId == EmpresaId && f.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (flujo == null) return NotFound();

        return Ok(flujo);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /flujos
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Create([FromBody] CreateFlujoRequest req)
    {
        var flujo = new Flujo
        {
            EmpresaId = EmpresaId,
            TipoInspeccionId = req.TipoInspeccionId,
            Nombre = req.Nombre,
            Descripcion = req.Descripcion,
            Activo = true,
            CreatedBy = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value)
        };

        _db.Flujos.Add(flujo);

        // Crear automáticamente la primera versión en borrador
        var version = new FlujoVersion
        {
            FlujoId = flujo.Id,
            NumeroVersion = 1,
            Estado = EstadoFlujoVersion.Borrador,
            DescripcionCambio = "Versión inicial"
        };
        _db.FlujoVersiones.Add(version);

        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = flujo.Id },
            new { flujo.Id, flujo.Nombre, version_id = version.Id });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /flujos/{id}/versiones/{versionId}  — obtiene flujo completo
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("{id:guid}/versiones/{versionId:guid}")]
    public async Task<IActionResult> GetVersion(Guid id, Guid versionId)
    {
        // Verificar que el flujo pertenece a la empresa
        var flujoExiste = await _db.Flujos.AnyAsync(f =>
            f.Id == id && f.EmpresaId == EmpresaId && f.DeletedAt == null);
        if (!flujoExiste) return NotFound();

        var version = await _db.FlujoVersiones
            .Include(v => v.Secciones.OrderBy(s => s.Orden))
                .ThenInclude(s => s.Preguntas.OrderBy(p => p.Orden))
                    .ThenInclude(p => p.Opciones.Where(o => o.Activo).OrderBy(o => o.Orden))
            .Include(v => v.Reglas.Where(r => r.Activo).OrderBy(r => r.Orden))
            .FirstOrDefaultAsync(v => v.Id == versionId && v.FlujoId == id);

        if (version == null) return NotFound();

        return Ok(version);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /flujos/{id}/versiones/{versionId}/publicar
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost("{id:guid}/versiones/{versionId:guid}/publicar")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Publicar(Guid id, Guid versionId)
    {
        var version = await _db.FlujoVersiones
            .Include(v => v.Flujo)
            .FirstOrDefaultAsync(v => v.Id == versionId && v.FlujoId == id
                                    && v.Flujo.EmpresaId == EmpresaId);

        if (version == null) return NotFound();

        if (version.Estado == EstadoFlujoVersion.Archivado)
            return BadRequest(new { error = "No se puede publicar una versión archivada" });

        if (version.Estado == EstadoFlujoVersion.Publicado)
            return BadRequest(new { error = "Esta versión ya está publicada" });

        version.Estado = EstadoFlujoVersion.Publicado;
        version.PublicadoPor = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        version.PublicadoEn = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Versión publicada exitosamente", version_id = version.Id });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /flujos/{id}/versiones/{versionId}/secciones
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost("{id:guid}/versiones/{versionId:guid}/secciones")]
    [Authorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> AddSeccion(Guid id, Guid versionId, [FromBody] CreateSeccionRequest req)
    {
        var version = await GetVersionEditable(id, versionId);
        if (version == null) return NotFound();

        var maxOrden = version.Secciones.Any() ? version.Secciones.Max(s => s.Orden) : 0;

        var seccion = new FlujoSeccion
        {
            FlujoVersionId = versionId,
            Codigo = req.Codigo,
            Titulo = req.Titulo,
            Descripcion = req.Descripcion,
            Orden = req.Orden ?? (maxOrden + 1),
            Visible = true,
            Icono = req.Icono,
            Color = req.Color
        };

        _db.FlujoSecciones.Add(seccion);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetVersion), new { id, versionId }, new { seccion.Id, seccion.Codigo });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /flujos/{id}/versiones/{versionId}/secciones/{seccionId}/preguntas
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost("{id:guid}/versiones/{versionId:guid}/secciones/{seccionId:guid}/preguntas")]
    [Authorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> AddPregunta(
        Guid id, Guid versionId, Guid seccionId, [FromBody] CreatePreguntaRequest req)
    {
        var version = await GetVersionEditable(id, versionId);
        if (version == null) return NotFound();

        var seccion = version.Secciones.FirstOrDefault(s => s.Id == seccionId);
        if (seccion == null) return NotFound(new { error = "Sección no encontrada" });

        // Validar código único en la versión
        if (version.Preguntas.Any(p => p.Codigo == req.Codigo))
            return Conflict(new { error = $"Ya existe una pregunta con código '{req.Codigo}' en esta versión" });

        var pregunta = new FlujoPregunta
        {
            FlujoVersionId = versionId,
            SeccionId = seccionId,
            Codigo = req.Codigo,
            Texto = req.Texto,
            TipoControl = Enum.Parse<TipoControl>(req.TipoControl, true),
            Placeholder = req.Placeholder,
            Ayuda = req.Ayuda,
            Obligatorio = req.Obligatorio,
            Orden = req.Orden ?? (seccion.Preguntas.Any() ? seccion.Preguntas.Max(p => p.Orden) + 1 : 1),
            Visible = req.Visible ?? true,
            Editable = req.Editable ?? true,
            ValorPorDefecto = req.ValorPorDefecto,
            ValidacionesJson = req.ValidacionesJson ?? "{}",
            ConfiguracionJson = req.ConfiguracionJson ?? "{}"
        };

        _db.FlujoPreguntas.Add(pregunta);

        // Agregar opciones si aplica
        if (req.Opciones?.Any() == true)
        {
            var opciones = req.Opciones.Select((o, i) => new FlujoOpcion
            {
                PreguntaId = pregunta.Id,
                Codigo = o.Codigo,
                Texto = o.Texto,
                Orden = o.Orden ?? (i + 1),
                ValorNumerico = o.ValorNumerico
            }).ToList();
            await _db.FlujoOpciones.AddRangeAsync(opciones);
        }

        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetVersion), new { id, versionId },
            new { pregunta.Id, pregunta.Codigo, pregunta.TipoControl });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /flujos/{id}/versiones/{versionId}/reglas
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost("{id:guid}/versiones/{versionId:guid}/reglas")]
    [Authorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> AddRegla(Guid id, Guid versionId, [FromBody] CreateReglaRequest req)
    {
        var version = await GetVersionEditable(id, versionId);
        if (version == null) return NotFound();

        var regla = new FlujoRegla
        {
            FlujoVersionId = versionId,
            Codigo = req.Codigo,
            Descripcion = req.Descripcion,
            PreguntaOrigenId = req.PreguntaOrigenId,
            Operador = Enum.Parse<OperadorRegla>(req.Operador, true),
            ValorComparacion = req.ValorComparacion,
            ValorComparacionJson = req.ValorComparacionJson,
            Accion = Enum.Parse<AccionRegla>(req.Accion, true),
            PreguntaDestinoId = req.PreguntaDestinoId,
            SeccionDestinoId = req.SeccionDestinoId,
            ParametrosJson = req.ParametrosJson ?? "{}",
            Orden = req.Orden ?? 0
        };

        _db.FlujoReglas.Add(regla);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetVersion), new { id, versionId }, new { regla.Id });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helper: obtener versión editable (solo Borrador)
    // ──────────────────────────────────────────────────────────────────────────
    private async Task<FlujoVersion?> GetVersionEditable(Guid flujoId, Guid versionId)
    {
        return await _db.FlujoVersiones
            .Include(v => v.Flujo)
            .Include(v => v.Secciones)
                .ThenInclude(s => s.Preguntas)
            .Include(v => v.Preguntas)
            .FirstOrDefaultAsync(v =>
                v.Id == versionId &&
                v.FlujoId == flujoId &&
                v.Flujo.EmpresaId == EmpresaId &&
                v.Estado == EstadoFlujoVersion.Borrador);
    }
}

// DTOs
public record CreateFlujoRequest(string Nombre, string? Descripcion, Guid? TipoInspeccionId);

public record CreateSeccionRequest(
    string Codigo, string Titulo, string? Descripcion,
    int? Orden, string? Icono, string? Color);

public record CreatePreguntaRequest(
    string Codigo, string Texto, string TipoControl,
    string? Placeholder, string? Ayuda,
    bool Obligatorio = false, int? Orden = null,
    bool? Visible = true, bool? Editable = true,
    string? ValorPorDefecto = null,
    string? ValidacionesJson = null,
    string? ConfiguracionJson = null,
    List<CreateOpcionRequest>? Opciones = null);

public record CreateOpcionRequest(
    string Codigo, string Texto, int? Orden = null, decimal? ValorNumerico = null);

public record CreateReglaRequest(
    Guid PreguntaOrigenId, string Operador, string? ValorComparacion,
    string Accion, Guid? PreguntaDestinoId, Guid? SeccionDestinoId,
    string? Codigo = null, string? Descripcion = null,
    string? ValorComparacionJson = null, string? ParametrosJson = null,
    int? Orden = null);

using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SgiForm.Domain.Entities;
using SgiForm.Domain.Enums;
using SgiForm.Infrastructure.Persistence;
using System.Text.Json;

namespace SgiForm.Infrastructure.Services;

public interface IExcelImportService
{
    Task<ImportacionLote> IniciarImportacionAsync(
        Guid empresaId, Guid usuarioId, Stream excelStream,
        string nombreOriginal, Guid? tipoInspeccionId, Guid? flujoVersionId);

    Task<ImportacionLote> ProcesarLoteAsync(Guid loteId);
    Task<object> GetPreviewAsync(Guid loteId, int pagina = 1, int porPagina = 20);
}

/// <summary>
/// Servicio de importación desde Excel.
/// Valida, previsualiza y persiste los registros del Excel en servicio_inspeccion.
/// Columnas esperadas del Excel (case-insensitive):
///   id_servicio, numero_medidor, marca, diametro, direccion,
///   nombre, coordenadax, coordenaday, lote, localidad,
///   ruta, libreta, observacion_libre
/// </summary>
public class ExcelImportService : IExcelImportService
{
    private readonly AppDbContext _db;
    private readonly string _uploadPath;

    // Columnas obligatorias (lowercase)
    private static readonly HashSet<string> ColsObligatorias = new() { "id_servicio" };

    // Mapeo de nombres de columna posibles → nombre interno.
    // Las claves se normalizan (lowercase, sin acentos, sin espacios ni guiones/_)
    // antes de comparar en NormalizeHeader, así que aquí basta con incluir
    // variantes "canónicas" ya normalizadas.
    private static readonly Dictionary<string, string> MapeoColumnas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["idservicio"]       = "id_servicio",
        ["idsrv"]            = "id_servicio",
        ["servicio"]         = "id_servicio",
        ["codigoservicio"]   = "id_servicio",
        ["numeromedidor"]    = "numero_medidor",
        ["nromedidor"]       = "numero_medidor",
        ["nmedidor"]         = "numero_medidor",
        ["medidor"]          = "numero_medidor",
        ["idmedidor"]        = "numero_medidor",
        ["marca"]            = "marca",
        ["marcamedidor"]     = "marca",
        ["diametro"]         = "diametro",
        ["diam"]             = "diametro",
        ["direccion"]        = "direccion",
        ["domicilio"]        = "direccion",
        ["calle"]            = "direccion",
        ["nombre"]           = "nombre",
        ["nombrecliente"]    = "nombre",
        ["cliente"]          = "nombre",
        ["titular"]          = "nombre",
        ["coordenadax"]      = "coordenadax",
        ["x"]                = "coordenadax",
        ["longitud"]         = "coordenadax",
        ["lng"]              = "coordenadax",
        ["lon"]              = "coordenadax",
        ["coordenaday"]      = "coordenaday",
        ["y"]                = "coordenaday",
        ["latitud"]          = "coordenaday",
        ["lat"]              = "coordenaday",
        ["lote"]             = "lote",
        ["localidad"]        = "localidad",
        ["ciudad"]           = "localidad",
        ["comuna"]           = "localidad",
        ["ruta"]             = "ruta",
        ["zona"]             = "ruta",
        ["libreta"]          = "libreta",
        ["observacionlibre"] = "observacion_libre",
        ["observacion"]      = "observacion_libre",
        ["observaciones"]    = "observacion_libre",
        ["obs"]              = "observacion_libre",
        ["nota"]             = "observacion_libre",
        ["notas"]            = "observacion_libre",
    };

    /// <summary>Normaliza una cabecera: lowercase, sin acentos, sin espacios/guiones/puntos.</summary>
    private static string NormalizeHeader(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var s = raw.Trim().ToLowerInvariant();
        // Quitar acentos
        var normalized = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        // Quitar caracteres no alfanuméricos
        var filtrada = new System.Text.StringBuilder(sb.Length);
        foreach (var c in sb.ToString())
            if (char.IsLetterOrDigit(c)) filtrada.Append(c);
        return filtrada.ToString();
    }

    public ExcelImportService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _uploadPath = config["Storage:UploadPath"] ?? Path.Combine(Path.GetTempPath(), "sgiform", "uploads");
        Directory.CreateDirectory(_uploadPath);
    }

    public async Task<ImportacionLote> IniciarImportacionAsync(
        Guid empresaId, Guid usuarioId, Stream excelStream,
        string nombreOriginal, Guid? tipoInspeccionId, Guid? flujoVersionId)
    {
        // Guardar archivo temporalmente
        var nombreArchivo = $"{Guid.NewGuid()}_{Path.GetFileName(nombreOriginal)}";
        var rutaArchivo = Path.Combine(_uploadPath, nombreArchivo);

        await using (var fs = File.Create(rutaArchivo))
            await excelStream.CopyToAsync(fs);

        // Calcular hash del archivo
        var hash = await CalcularHashAsync(rutaArchivo);

        // Contar filas (sin procesar)
        int totalFilas = 0;
        using (var wb = new XLWorkbook(rutaArchivo))
        {
            var ws = wb.Worksheets.First();
            totalFilas = ws.RowsUsed().Count() - 1; // -1 header
        }

        var lote = new ImportacionLote
        {
            EmpresaId = empresaId,
            NombreArchivo = nombreArchivo,
            NombreOriginal = nombreOriginal,
            HashArchivo = hash,
            TotalFilas = totalFilas,
            Estado = EstadoImportacion.Pendiente,
            TipoInspeccionId = tipoInspeccionId,
            FlujoVersionId = flujoVersionId,
            UsuarioId = usuarioId
        };

        _db.ImportacionLotes.Add(lote);
        await _db.SaveChangesAsync();

        return lote;
    }

    public async Task<ImportacionLote> ProcesarLoteAsync(Guid loteId)
    {
        var lote = await _db.ImportacionLotes
            .FirstOrDefaultAsync(l => l.Id == loteId)
            ?? throw new KeyNotFoundException($"Lote {loteId} no encontrado");

        lote.Estado = EstadoImportacion.Procesando;
        lote.ProcesadoEn = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var rutaArchivo = Path.Combine(_uploadPath, lote.NombreArchivo);
        var errores = new List<ImportacionDetalle>();
        var serviciosNuevos = new List<ServicioInspeccion>();
        int filasOk = 0, filasError = 0;

        try
        {
            using var wb = new XLWorkbook(rutaArchivo);
            var ws = wb.Worksheets.First();
            var rows = ws.RowsUsed().ToList();

            if (rows.Count < 2)
            {
                lote.Estado = EstadoImportacion.Fallido;
                lote.ErrorGeneral = "El archivo no contiene datos (solo encabezado o vacío)";
                await _db.SaveChangesAsync();
                return lote;
            }

            // Mapear cabeceras (normalizando para tolerar mayúsculas, acentos y espacios)
            var headerRow = rows[0];
            var colMap = new Dictionary<string, int>(); // nombre_interno → col index

            foreach (var cell in headerRow.CellsUsed())
            {
                var headerNormalizado = NormalizeHeader(cell.GetString());
                if (string.IsNullOrEmpty(headerNormalizado)) continue;
                if (MapeoColumnas.TryGetValue(headerNormalizado, out var nombreInterno)
                    && !colMap.ContainsKey(nombreInterno))
                    colMap[nombreInterno] = cell.Address.ColumnNumber;
            }

            // Validar columnas obligatorias
            var faltantes = ColsObligatorias.Where(c => !colMap.ContainsKey(c)).ToList();
            if (faltantes.Any())
            {
                lote.Estado = EstadoImportacion.Fallido;
                lote.ErrorGeneral = $"Columnas obligatorias faltantes: {string.Join(", ", faltantes)}";
                await _db.SaveChangesAsync();
                return lote;
            }

            // Procesar filas de datos
            var idsVistosEnArchivo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                var rowErrors = new List<string>();
                var datosOriginales = new Dictionary<string, string>();

                // Extraer valores — tolerante a celdas vacías, tipo numérico, espacios
                string Get(string col)
                {
                    if (!colMap.TryGetValue(col, out var idx)) return "";
                    try
                    {
                        var cell = row.Cell(idx);
                        if (cell == null || cell.IsEmpty()) return "";
                        // GetString funciona para casi todo; para números preserva formato
                        var raw = cell.GetString();
                        return raw?.Trim() ?? "";
                    }
                    catch
                    {
                        return "";
                    }
                }

                // Si todas las celdas están vacías, saltar la fila silenciosamente (filas trailing)
                bool filaVacia = !colMap.Values.Any(idx =>
                {
                    try { var c = row.Cell(idx); return c != null && !c.IsEmpty() && !string.IsNullOrWhiteSpace(c.GetString()); }
                    catch { return false; }
                });
                if (filaVacia) continue;

                var idServicio = Get("id_servicio");
                datosOriginales["id_servicio"] = idServicio;

                if (string.IsNullOrWhiteSpace(idServicio))
                    rowErrors.Add("id_servicio es obligatorio");
                else if (!idsVistosEnArchivo.Add(idServicio))
                    rowErrors.Add($"id_servicio '{idServicio}' duplicado dentro del archivo");

                // Validar coordenadas si se proveen (acepta coma o punto decimal, con/sin signo)
                decimal? coordX = null, coordY = null;
                var sCoordX = Get("coordenadax");
                var sCoordY = Get("coordenaday");
                datosOriginales["coordenadax"] = sCoordX;
                datosOriginales["coordenaday"] = sCoordY;

                decimal? ParseCoord(string raw, string nombre)
                {
                    if (string.IsNullOrWhiteSpace(raw)) return null;
                    var limpio = raw.Replace(" ", "").Replace(",", ".");
                    if (decimal.TryParse(limpio,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var v))
                        return v;
                    rowErrors.Add($"{nombre} inválida: '{raw}'");
                    return null;
                }

                coordX = ParseCoord(sCoordX, "coordenadax");
                coordY = ParseCoord(sCoordY, "coordenaday");

                // Validar rango geográfico razonable (lat ±90, lng ±180)
                if (coordY.HasValue && (coordY < -90 || coordY > 90))
                    rowErrors.Add($"coordenaday (latitud) fuera de rango: {coordY}");
                if (coordX.HasValue && (coordX < -180 || coordX > 180))
                    rowErrors.Add($"coordenadax (longitud) fuera de rango: {coordX}");

                if (rowErrors.Any())
                {
                    filasError++;
                    errores.Add(new ImportacionDetalle
                    {
                        LoteId = loteId,
                        NumeroFila = i + 1,
                        Estado = "error",
                        ErroresJson = JsonSerializer.Serialize(rowErrors),
                        DatosOriginales = JsonSerializer.Serialize(datosOriginales)
                    });
                    continue;
                }

                // Verificar duplicado en empresa
                var existe = await _db.ServiciosInspeccion
                    .AnyAsync(s => s.EmpresaId == lote.EmpresaId && s.IdServicio == idServicio);

                if (existe)
                {
                    filasError++;
                    errores.Add(new ImportacionDetalle
                    {
                        LoteId = loteId,
                        NumeroFila = i + 1,
                        Estado = "omitido",
                        ErroresJson = JsonSerializer.Serialize(new[] { $"id_servicio '{idServicio}' ya existe" }),
                        DatosOriginales = JsonSerializer.Serialize(datosOriginales)
                    });
                    continue;
                }

                serviciosNuevos.Add(new ServicioInspeccion
                {
                    EmpresaId = lote.EmpresaId,
                    ImportacionLoteId = loteId,
                    IdServicio = idServicio,
                    NumeroMedidor = Get("numero_medidor").NullIfEmpty(),
                    Marca = Get("marca").NullIfEmpty(),
                    Diametro = Get("diametro").NullIfEmpty(),
                    Direccion = Get("direccion").NullIfEmpty(),
                    NombreCliente = Get("nombre").NullIfEmpty(),
                    CoordenadaX = coordX,
                    CoordenadaY = coordY,
                    Lote = Get("lote").NullIfEmpty(),
                    Localidad = Get("localidad").NullIfEmpty(),
                    Ruta = Get("ruta").NullIfEmpty(),
                    Libreta = Get("libreta").NullIfEmpty(),
                    ObservacionLibre = Get("observacion_libre").NullIfEmpty(),
                });

                filasOk++;
            }

            // Guardar en batch
            if (serviciosNuevos.Any())
                await _db.ServiciosInspeccion.AddRangeAsync(serviciosNuevos);

            if (errores.Any())
                await _db.ImportacionDetalles.AddRangeAsync(errores);

            lote.FilasValidas = filasOk;
            lote.FilasError = filasError;
            // Si no hay filas válidas, marcamos "Fallido" aunque haya detalles con
            // error — nada se insertó, no tiene sentido decir "Completado".
            lote.Estado = filasOk == 0
                ? EstadoImportacion.Fallido
                : (filasError == 0
                    ? EstadoImportacion.Completado
                    : EstadoImportacion.CompletadoConErrores);

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            lote.Estado = EstadoImportacion.Fallido;
            lote.ErrorGeneral = ex.Message;
            await _db.SaveChangesAsync();
        }

        return lote;
    }

    public async Task<object> GetPreviewAsync(Guid loteId, int pagina = 1, int porPagina = 20)
    {
        var lote = await _db.ImportacionLotes.FindAsync(loteId)
            ?? throw new KeyNotFoundException($"Lote {loteId} no encontrado");

        var rutaArchivo = Path.Combine(_uploadPath, lote.NombreArchivo);
        using var wb = new XLWorkbook(rutaArchivo);
        var ws = wb.Worksheets.First();
        var rows = ws.RowsUsed().ToList();

        if (rows.Count < 2) return new { total = 0, filas = Array.Empty<object>() };

        var headerRow = rows[0];
        var headers = headerRow.CellsUsed().Select(c => c.GetString().Trim()).ToList();

        var dataRows = rows.Skip(1)
            .Skip((pagina - 1) * porPagina)
            .Take(porPagina)
            .Select(row => headers.Zip(row.Cells(1, headers.Count)
                .Select(c => c.GetString()),
                (h, v) => new { h, v })
                .ToDictionary(x => x.h, x => x.v))
            .ToList();

        return new
        {
            total = rows.Count - 1,
            pagina,
            porPagina,
            headers,
            filas = dataRows
        };
    }

    private static async Task<string> CalcularHashAsync(string rutaArchivo)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        await using var fs = File.OpenRead(rutaArchivo);
        var hash = await sha.ComputeHashAsync(fs);
        return Convert.ToHexString(hash).ToLower();
    }
}

internal static class StringExtensions
{
    public static string? NullIfEmpty(this string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}



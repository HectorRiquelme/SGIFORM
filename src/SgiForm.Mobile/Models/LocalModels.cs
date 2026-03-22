using SQLite;

namespace SgiForm.Mobile.Models;

/// <summary>
/// Modelos locales SQLite para la app móvil.
/// Son una versión aplanada/serializada de las entidades del servidor,
/// optimizada para almacenamiento local y acceso offline.
/// </summary>

[Table("asignaciones")]
public class AsignacionLocal
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? EmpresaId { get; set; }
    public string? ServicioId { get; set; }
    public string? TipoInspeccionId { get; set; }
    public string? TipoInspeccionNombre { get; set; }
    public string? FlujoVersionId { get; set; }
    public string Estado { get; set; } = "pendiente";
    public string? Prioridad { get; set; }
    // Datos del servicio (desnormalizados para offline)
    public string? IdServicio { get; set; }
    public string? NumeroMedidor { get; set; }
    public string? Marca { get; set; }
    public string? Diametro { get; set; }
    public string? Direccion { get; set; }
    public string? NombreCliente { get; set; }
    public double? CoordenadaX { get; set; }
    public double? CoordenadaY { get; set; }
    public string? Lote { get; set; }
    public string? Localidad { get; set; }
    public string? Ruta { get; set; }
    public string? Libreta { get; set; }
    public string? ObservacionLibre { get; set; }
    // Timestamps
    public DateTime FechaAsignacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaInicioEsperada { get; set; }
    public DateTime? FechaFinEsperada { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("inspecciones")]
public class InspeccionLocal
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? AsignacionId { get; set; }
    public string? FlujoVersionId { get; set; }
    public string Estado { get; set; } = "borrador";
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public double? CoordXInicio { get; set; }
    public double? CoordYInicio { get; set; }
    public double? CoordXFin { get; set; }
    public double? CoordYFin { get; set; }
    public double? PrecisionGps { get; set; }
    public int TotalPreguntas { get; set; }
    public int TotalRespondidas { get; set; }
    public int TotalFotografias { get; set; }
    public int? DuracionSegundos { get; set; }
    public bool SincronizadoConServidor { get; set; } = false;
    public DateTime? SincronizadoEn { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Ignore]
    public double PorcentajeAvance =>
        TotalPreguntas > 0 ? (double)TotalRespondidas / TotalPreguntas * 100 : 0;
}

[Table("respuestas")]
public class RespuestaLocal
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string? InspeccionId { get; set; }
    public string? PreguntaId { get; set; }
    public string? TipoControl { get; set; }
    public string? ValorTexto { get; set; }
    public long? ValorEntero { get; set; }
    public double? ValorDecimal { get; set; }
    public string? ValorFecha { get; set; }   // ISO 8601
    public bool? ValorBooleano { get; set; }
    public string? ValorJson { get; set; }     // para selección múltiple, coordenadas
    public bool EsValido { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("fotografias")]
public class FotografiaLocal
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? InspeccionId { get; set; }
    public string? PreguntaId { get; set; }
    public string? RutaLocal { get; set; }    // ruta en el filesystem del device
    public string? NombreArchivo { get; set; }
    public long? TamanioBytes { get; set; }
    public double? CoordenadaX { get; set; }
    public double? CoordenadaY { get; set; }
    public bool TieneMarcaAgua { get; set; }
    public string? HashSha256 { get; set; }
    public bool SubidaAlServidor { get; set; } = false;
    public int Orden { get; set; }
    public DateTime CapturaEn { get; set; } = DateTime.UtcNow;
}

[Table("flujo_versiones")]
public class FlujoVersionLocal
{
    [PrimaryKey]
    public string Id { get; set; } = "";
    public string? FlujoId { get; set; }
    public int NumeroVersion { get; set; }
    public string? Configuracion { get; set; }
    public DateTime DescargadoEn { get; set; } = DateTime.UtcNow;
}

[Table("secciones")]
public class SeccionLocal
{
    [PrimaryKey]
    public string Id { get; set; } = "";
    public string? FlujoVersionId { get; set; }
    public string? Codigo { get; set; }
    public string? Titulo { get; set; }
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool Visible { get; set; } = true;
    public string? CondicionalJson { get; set; }
    public string? Icono { get; set; }
}

[Table("preguntas")]
public class PreguntaLocal
{
    [PrimaryKey]
    public string Id { get; set; } = "";
    public string? FlujoVersionId { get; set; }
    public string? SeccionId { get; set; }
    public string? Codigo { get; set; }
    public string? Texto { get; set; }
    public string? TipoControl { get; set; }
    public string? Placeholder { get; set; }
    public string? Ayuda { get; set; }
    public bool Obligatorio { get; set; }
    public int Orden { get; set; }
    public bool Visible { get; set; } = true;
    public bool Editable { get; set; } = true;
    public string? ValorPorDefecto { get; set; }
    public string ValidacionesJson { get; set; } = "{}";
    public string ConfiguracionJson { get; set; } = "{}";
    // Estado en tiempo de ejecución (no persistido, solo en memoria)
    [Ignore] public bool VisibleRuntime { get; set; } = true;
    [Ignore] public bool ObligatorioRuntime { get; set; }
}

[Table("opciones")]
public class OpcionLocal
{
    [PrimaryKey]
    public string Id { get; set; } = "";
    public string? PreguntaId { get; set; }
    public string? Codigo { get; set; }
    public string? Texto { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public double? ValorNumerico { get; set; }
}

[Table("reglas")]
public class ReglaLocal
{
    [PrimaryKey]
    public string Id { get; set; } = "";
    public string? FlujoVersionId { get; set; }
    public string? PreguntaOrigenId { get; set; }
    public string? Operador { get; set; }
    public string? ValorComparacion { get; set; }
    public string? ValorComparacionJson { get; set; }
    public string? Accion { get; set; }
    public string? PreguntaDestinoId { get; set; }
    public string? SeccionDestinoId { get; set; }
    public string ParametrosJson { get; set; } = "{}";
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

[Table("catalogos")]
public class CatalogoLocal
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string? Tipo { get; set; }
    public string? Codigo { get; set; }
    public string? Texto { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>
/// Cola de sincronización persistente.
/// Cuando hay conexión, los items pending se envían al servidor.
/// </summary>
[Table("sync_queue")]
public class SyncQueueItem
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? EntityType { get; set; }   // "inspeccion", "fotografia"
    public string? EntityId { get; set; }
    public string? Operation { get; set; }    // "INSERT", "UPDATE"
    public string? PayloadJson { get; set; }
    public string Estado { get; set; } = "pending"; // pending, sent, error
    public int Intentos { get; set; } = 0;
    public DateTime? UltimoIntento { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

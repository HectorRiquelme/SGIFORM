using SanitasField.Domain.Enums;

namespace SanitasField.Domain.Entities;

public class ImportacionLote : BaseEntity
{
    public Guid EmpresaId { get; set; }
    public string NombreArchivo { get; set; } = null!;
    public string NombreOriginal { get; set; } = null!;
    public string? HashArchivo { get; set; }
    public int? TotalFilas { get; set; }
    public int FilasValidas { get; set; } = 0;
    public int FilasError { get; set; } = 0;
    public int FilasOmitidas { get; set; } = 0;
    public EstadoImportacion Estado { get; set; } = EstadoImportacion.Pendiente;
    public Guid? TipoInspeccionId { get; set; }
    public Guid? FlujoVersionId { get; set; }
    public Guid UsuarioId { get; set; }
    public string? Notas { get; set; }
    public string? ErrorGeneral { get; set; }
    public string ConfiguracionMapeo { get; set; } = "{}";
    public DateTimeOffset? ProcesadoEn { get; set; }

    // Navigation
    public Empresa Empresa { get; set; } = null!;
    public TipoInspeccion? TipoInspeccion { get; set; }
    public FlujoVersion? FlujoVersion { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public ICollection<ImportacionDetalle> Detalles { get; set; } = new List<ImportacionDetalle>();
    public ICollection<ServicioInspeccion> Servicios { get; set; } = new List<ServicioInspeccion>();
}

public class ImportacionDetalle : BaseEntity
{
    public Guid LoteId { get; set; }
    public int NumeroFila { get; set; }
    public string Estado { get; set; } = "error"; // ok, error, omitido
    public string? ErroresJson { get; set; }
    public string? DatosOriginales { get; set; }

    // Navigation
    public ImportacionLote Lote { get; set; } = null!;
}

public class ServicioInspeccion : BaseEntity
{
    public Guid EmpresaId { get; set; }
    public Guid? ImportacionLoteId { get; set; }
    // Campos del Excel
    public string IdServicio { get; set; } = null!;
    public string? NumeroMedidor { get; set; }
    public string? Marca { get; set; }
    public string? Diametro { get; set; }
    public string? Direccion { get; set; }
    public string? NombreCliente { get; set; }
    public decimal? CoordenadaX { get; set; }
    public decimal? CoordenadaY { get; set; }
    public string? Lote { get; set; }
    public string? Localidad { get; set; }
    public string? Ruta { get; set; }
    public string? Libreta { get; set; }
    public string? ObservacionLibre { get; set; }
    // Control
    public bool Activo { get; set; } = true;
    public bool TieneAsignacion { get; set; } = false;

    // Navigation
    public Empresa Empresa { get; set; } = null!;
    public ImportacionLote? ImportacionLote { get; set; }
    public ICollection<AsignacionInspeccion> Asignaciones { get; set; } = new List<AsignacionInspeccion>();
}

public class AsignacionInspeccion : SoftDeleteEntity
{
    public Guid EmpresaId { get; set; }
    public Guid ServicioInspeccionId { get; set; }
    public Guid OperadorId { get; set; }
    public Guid TipoInspeccionId { get; set; }
    public Guid FlujoVersionId { get; set; }
    public EstadoAsignacion Estado { get; set; } = EstadoAsignacion.Pendiente;
    public Prioridad Prioridad { get; set; } = Prioridad.Normal;
    public DateTimeOffset FechaAsignacion { get; set; } = DateTimeOffset.UtcNow;
    public DateOnly? FechaInicioEsperada { get; set; }
    public DateOnly? FechaFinEsperada { get; set; }
    public DateTimeOffset? FechaDescarga { get; set; }
    public DateTimeOffset? FechaInicioEjecucion { get; set; }
    public DateTimeOffset? FechaFinalizacion { get; set; }
    public string? Observaciones { get; set; }
    public Guid AsignadoPor { get; set; }

    // Navigation
    public Empresa Empresa { get; set; } = null!;
    public ServicioInspeccion ServicioInspeccion { get; set; } = null!;
    public Operador Operador { get; set; } = null!;
    public TipoInspeccion TipoInspeccion { get; set; } = null!;
    public FlujoVersion FlujoVersion { get; set; } = null!;
    public Usuario AsignadoPorUsuario { get; set; } = null!;
    public Inspeccion? Inspeccion { get; set; }
}

public class Inspeccion : BaseEntity
{
    public Guid EmpresaId { get; set; }
    public Guid AsignacionId { get; set; }
    public Guid OperadorId { get; set; }
    public Guid ServicioInspeccionId { get; set; }
    public Guid FlujoVersionId { get; set; }
    public EstadoInspeccion Estado { get; set; } = EstadoInspeccion.Borrador;
    public DateTimeOffset? FechaInicio { get; set; }
    public DateTimeOffset? FechaFin { get; set; }
    public int? DuracionSegundos { get; set; }
    public decimal? CoordXInicio { get; set; }
    public decimal? CoordYInicio { get; set; }
    public decimal? CoordXFin { get; set; }
    public decimal? CoordYFin { get; set; }
    public decimal? PrecisionGps { get; set; }
    public string? DeviceId { get; set; }
    public string? AppVersion { get; set; }
    public DateTimeOffset? SincronizadoEn { get; set; }
    public Guid? RevisionPor { get; set; }
    public DateTimeOffset? RevisionEn { get; set; }
    public string? RevisionObservacion { get; set; }
    public int TotalPreguntas { get; set; } = 0;
    public int TotalRespondidas { get; set; } = 0;
    public int TotalFotografias { get; set; } = 0;

    // Navigation
    public Empresa Empresa { get; set; } = null!;
    public AsignacionInspeccion Asignacion { get; set; } = null!;
    public Operador Operador { get; set; } = null!;
    public ServicioInspeccion ServicioInspeccion { get; set; } = null!;
    public FlujoVersion FlujoVersion { get; set; } = null!;
    public Usuario? Revisor { get; set; }
    public ICollection<InspeccionRespuesta> Respuestas { get; set; } = new List<InspeccionRespuesta>();
    public ICollection<InspeccionFotografia> Fotografias { get; set; } = new List<InspeccionFotografia>();
    public ICollection<InspeccionHistorial> Historial { get; set; } = new List<InspeccionHistorial>();
}

public class InspeccionRespuesta : BaseEntity
{
    public Guid InspeccionId { get; set; }
    public Guid PreguntaId { get; set; }
    public TipoControl TipoControl { get; set; }
    public string? ValorTexto { get; set; }
    public long? ValorEntero { get; set; }
    public decimal? ValorDecimal { get; set; }
    public DateOnly? ValorFecha { get; set; }
    public TimeOnly? ValorHora { get; set; }
    public DateTimeOffset? ValorFechaHora { get; set; }
    public bool? ValorBooleano { get; set; }
    public string? ValorJson { get; set; }
    public bool EsValido { get; set; } = true;
    public string? ErroresValidacion { get; set; }
    public DateTimeOffset RespondidaEn { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public Inspeccion Inspeccion { get; set; } = null!;
    public FlujoPregunta Pregunta { get; set; } = null!;
}

public class InspeccionFotografia : BaseEntity
{
    public Guid InspeccionId { get; set; }
    public Guid? PreguntaId { get; set; }
    public string NombreArchivo { get; set; } = null!;
    public string RutaAlmacenamiento { get; set; } = null!;
    public string? UrlPublica { get; set; }
    public int? TamanioBytes { get; set; }
    public int? Ancho { get; set; }
    public int? Alto { get; set; }
    public string Formato { get; set; } = "jpg";
    public decimal? CoordenadaX { get; set; }
    public decimal? CoordenadaY { get; set; }
    public decimal? PrecisionGps { get; set; }
    public bool TieneMarcaAgua { get; set; } = false;
    public string? HashSha256 { get; set; }
    public int Orden { get; set; } = 0;
    public string MetadataJson { get; set; } = "{}";

    // Navigation
    public Inspeccion Inspeccion { get; set; } = null!;
    public FlujoPregunta? Pregunta { get; set; }
}

public class InspeccionHistorial : BaseEntity
{
    public Guid InspeccionId { get; set; }
    public Guid? UsuarioId { get; set; }
    public Guid? OperadorId { get; set; }
    public string Accion { get; set; } = null!;
    public EstadoInspeccion? EstadoAnterior { get; set; }
    public EstadoInspeccion EstadoNuevo { get; set; }
    public string? Observacion { get; set; }
    public string? MetadataJson { get; set; }

    // Navigation
    public Inspeccion Inspeccion { get; set; } = null!;
    public Usuario? Usuario { get; set; }
    public Operador? Operador { get; set; }
}

public class SincronizacionLog : BaseEntity
{
    public Guid EmpresaId { get; set; }
    public Guid OperadorId { get; set; }
    public string? DeviceId { get; set; }
    public TipoSync Tipo { get; set; }
    public string? PayloadHash { get; set; }
    public int RegistrosEnviados { get; set; } = 0;
    public int RegistrosRecibidos { get; set; } = 0;
    public int FotosEnviadas { get; set; } = 0;
    public long BytesTransferidos { get; set; } = 0;
    public int? DuracionMs { get; set; }
    public bool Exitoso { get; set; } = true;
    public string? ErroresJson { get; set; }
    public string? IpOrigen { get; set; }

    // Navigation
    public Empresa Empresa { get; set; } = null!;
    public Operador Operador { get; set; } = null!;
}

public class Catalogo : BaseEntity
{
    public Guid? EmpresaId { get; set; }
    public string Tipo { get; set; } = null!;
    public string Codigo { get; set; } = null!;
    public string Texto { get; set; } = null!;
    public int Orden { get; set; } = 0;
    public bool Activo { get; set; } = true;
    public string? Metadata { get; set; }

    // Navigation
    public Empresa? Empresa { get; set; }
}

public class Auditoria : BaseEntity
{
    public Guid? EmpresaId { get; set; }
    public Guid? UsuarioId { get; set; }
    public Guid? OperadorId { get; set; }
    public string Entidad { get; set; } = null!;
    public Guid? EntidadId { get; set; }
    public string Accion { get; set; } = null!;
    public string? DatosAnteriores { get; set; }
    public string? DatosNuevos { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}

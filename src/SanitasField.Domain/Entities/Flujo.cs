using SanitasField.Domain.Enums;

namespace SanitasField.Domain.Entities;

public class TipoInspeccion : SoftDeleteEntity
{
    public Guid EmpresaId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public Guid? FlujoVersionIdDef { get; set; }
    public string? Icono { get; set; }
    public string? Color { get; set; }

    // Navigation
    public Empresa Empresa { get; set; } = null!;
    public FlujoVersion? FlujoVersionDefecto { get; set; }
    public ICollection<Flujo> Flujos { get; set; } = new List<Flujo>();
}

public class Flujo : SoftDeleteEntity
{
    public Guid EmpresaId { get; set; }
    public Guid? TipoInspeccionId { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public Guid? CreatedBy { get; set; }

    // Navigation
    public Empresa Empresa { get; set; } = null!;
    public TipoInspeccion? TipoInspeccion { get; set; }
    public ICollection<FlujoVersion> Versiones { get; set; } = new List<FlujoVersion>();
}

public class FlujoVersion : BaseEntity
{
    public Guid FlujoId { get; set; }
    public int NumeroVersion { get; set; } = 1;
    public EstadoFlujoVersion Estado { get; set; } = EstadoFlujoVersion.Borrador;
    public string? DescripcionCambio { get; set; }
    public Guid? PublicadoPor { get; set; }
    public DateTimeOffset? PublicadoEn { get; set; }
    public string Configuracion { get; set; } = "{}";

    // Navigation
    public Flujo Flujo { get; set; } = null!;
    public ICollection<FlujoSeccion> Secciones { get; set; } = new List<FlujoSeccion>();
    public ICollection<FlujoPregunta> Preguntas { get; set; } = new List<FlujoPregunta>();
    public ICollection<FlujoRegla> Reglas { get; set; } = new List<FlujoRegla>();
}

public class FlujoSeccion : BaseEntity
{
    public Guid FlujoVersionId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Titulo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int Orden { get; set; } = 0;
    public bool Visible { get; set; } = true;
    public string? CondicionalJson { get; set; }
    public string? Icono { get; set; }
    public string? Color { get; set; }

    // Navigation
    public FlujoVersion FlujoVersion { get; set; } = null!;
    public ICollection<FlujoPregunta> Preguntas { get; set; } = new List<FlujoPregunta>();
}

public class FlujoPregunta : BaseEntity
{
    public Guid FlujoVersionId { get; set; }
    public Guid SeccionId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Texto { get; set; } = null!;
    public TipoControl TipoControl { get; set; }
    public string? Placeholder { get; set; }
    public string? Ayuda { get; set; }
    public bool Obligatorio { get; set; } = false;
    public int Orden { get; set; } = 0;
    public bool Visible { get; set; } = true;
    public bool Editable { get; set; } = true;
    public string? ValorPorDefecto { get; set; }
    public string ValidacionesJson { get; set; } = "{}";
    public string ConfiguracionJson { get; set; } = "{}";

    // Navigation
    public FlujoVersion FlujoVersion { get; set; } = null!;
    public FlujoSeccion Seccion { get; set; } = null!;
    public ICollection<FlujoOpcion> Opciones { get; set; } = new List<FlujoOpcion>();
    public ICollection<FlujoRegla> ReglasOrigen { get; set; } = new List<FlujoRegla>();
    public ICollection<FlujoRegla> ReglasDestino { get; set; } = new List<FlujoRegla>();
}

public class FlujoOpcion : BaseEntity
{
    public Guid PreguntaId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Texto { get; set; } = null!;
    public int Orden { get; set; } = 0;
    public bool Activo { get; set; } = true;
    public decimal? ValorNumerico { get; set; }
    public string? MetadataJson { get; set; }

    // Navigation
    public FlujoPregunta Pregunta { get; set; } = null!;
}

public class FlujoRegla : BaseEntity
{
    public Guid FlujoVersionId { get; set; }
    public string? Codigo { get; set; }
    public string? Descripcion { get; set; }
    public Guid PreguntaOrigenId { get; set; }
    public OperadorRegla Operador { get; set; }
    public string? ValorComparacion { get; set; }
    public string? ValorComparacionJson { get; set; }
    public AccionRegla Accion { get; set; }
    public Guid? PreguntaDestinoId { get; set; }
    public Guid? SeccionDestinoId { get; set; }
    public string ParametrosJson { get; set; } = "{}";
    public int Orden { get; set; } = 0;
    public bool Activo { get; set; } = true;

    // Navigation
    public FlujoVersion FlujoVersion { get; set; } = null!;
    public FlujoPregunta PreguntaOrigen { get; set; } = null!;
    public FlujoPregunta? PreguntaDestino { get; set; }
    public FlujoSeccion? SeccionDestino { get; set; }
}

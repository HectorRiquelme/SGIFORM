namespace SanitasField.Domain.Entities;

public class Empresa : SoftDeleteEntity
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Rut { get; set; }
    public string? LogoUrl { get; set; }
    public string TenantSlug { get; set; } = null!;
    public bool Activo { get; set; } = true;
    public string Plan { get; set; } = "standard";
    public string Configuracion { get; set; } = "{}";

    // Navigation
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<Operador> Operadores { get; set; } = new List<Operador>();
    public ICollection<TipoInspeccion> TiposInspeccion { get; set; } = new List<TipoInspeccion>();
    public ICollection<Flujo> Flujos { get; set; } = new List<Flujo>();
}

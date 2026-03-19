namespace SanitasField.Domain.Entities;

public class Operador : SoftDeleteEntity
{
    public Guid EmpresaId { get; set; }
    public Guid? UsuarioId { get; set; }
    public string CodigoOperador { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Apellido { get; set; } = null!;
    public string? Rut { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Zona { get; set; }
    public string? Localidad { get; set; }
    public string PasswordHash { get; set; } = null!;
    public bool Activo { get; set; } = true;
    public DateTimeOffset? FechaUltimaSync { get; set; }
    public string? DeviceIdRegistrado { get; set; }
    public string? AppVersionUltima { get; set; }

    // Navigation
    public Empresa Empresa { get; set; } = null!;
    public Usuario? Usuario { get; set; }
    public ICollection<AsignacionInspeccion> Asignaciones { get; set; } = new List<AsignacionInspeccion>();

    public string NombreCompleto => $"{Nombre} {Apellido}";
}

using SanitasField.Domain.Enums;

namespace SanitasField.Domain.Entities;

public class Usuario : SoftDeleteEntity
{
    public Guid EmpresaId { get; set; }
    public Guid RolId { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Apellido { get; set; } = null!;
    public string? Telefono { get; set; }
    public string? AvatarUrl { get; set; }
    public EstadoUsuario Estado { get; set; } = EstadoUsuario.Activo;
    public DateTimeOffset? UltimoAcceso { get; set; }
    public int IntentosFallidos { get; set; } = 0;
    public DateTimeOffset? BloqueadoHasta { get; set; }

    // Navigation
    public Empresa Empresa { get; set; } = null!;
    public Rol Rol { get; set; } = null!;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public string NombreCompleto => $"{Nombre} {Apellido}";
}

public class Rol : BaseEntity
{
    public Guid EmpresaId { get; set; }
    public string Nombre { get; set; } = null!;
    public string Codigo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool EsSistema { get; set; } = false;
    public bool Activo { get; set; } = true;

    // Navigation
    public Empresa Empresa { get; set; } = null!;
    public ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}

public class Permiso : BaseEntity
{
    public string Modulo { get; set; } = null!;
    public string Accion { get; set; } = null!;
    public string? Descripcion { get; set; }

    // Navigation
    public ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();
}

public class RolPermiso
{
    public Guid RolId { get; set; }
    public Guid PermisoId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public Rol Rol { get; set; } = null!;
    public Permiso Permiso { get; set; } = null!;
}

public class RefreshToken : BaseEntity
{
    /// <summary>FK a usuario web. Nulo si el token pertenece a un operador móvil.</summary>
    public Guid? UsuarioId { get; set; }
    /// <summary>FK a operador móvil. Nulo si el token pertenece a un usuario web.</summary>
    public Guid? OperadorId { get; set; }
    public string Token { get; set; } = null!;
    public DateTimeOffset ExpiraEn { get; set; }
    public bool Revocado { get; set; } = false;
    public string? IpOrigen { get; set; }
    public string? UserAgent { get; set; }

    // Navigation
    public Usuario? Usuario { get; set; }
    public Operador? Operador { get; set; }
}

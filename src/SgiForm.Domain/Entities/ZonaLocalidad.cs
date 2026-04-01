namespace SgiForm.Domain.Entities;

public class Zona : SoftDeleteEntity
{
    public Guid EmpresaId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;

    // Navigation
    public Empresa Empresa { get; set; } = null!;
    public ICollection<Localidad> Localidades { get; set; } = new List<Localidad>();
}

public class Localidad : SoftDeleteEntity
{
    public Guid EmpresaId { get; set; }
    public Guid? ZonaId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public bool Activo { get; set; } = true;

    // Navigation
    public Empresa Empresa { get; set; } = null!;
    public Zona? Zona { get; set; }
}

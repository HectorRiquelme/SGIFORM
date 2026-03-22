using Microsoft.EntityFrameworkCore;

using SanitasField.Domain.Entities;
using SanitasField.Domain.Enums;

namespace SanitasField.Infrastructure.Persistence;

/// <summary>
/// DbContext principal de SanitasField.
/// Usa el schema 'sf' de PostgreSQL y mapea todas las entidades del dominio.
/// Los enums de C# se almacenan como strings (snake_case) para compatibilidad
/// con los tipos ENUM nativos de PostgreSQL definidos en 01_schema.sql.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // IAM
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Operador> Operadores => Set<Operador>();

    // Flujos
    public DbSet<TipoInspeccion> TiposInspeccion => Set<TipoInspeccion>();
    public DbSet<Flujo> Flujos => Set<Flujo>();
    public DbSet<FlujoVersion> FlujoVersiones => Set<FlujoVersion>();
    public DbSet<FlujoSeccion> FlujoSecciones => Set<FlujoSeccion>();
    public DbSet<FlujoPregunta> FlujoPreguntas => Set<FlujoPregunta>();
    public DbSet<FlujoOpcion> FlujoOpciones => Set<FlujoOpcion>();
    public DbSet<FlujoRegla> FlujoReglas => Set<FlujoRegla>();

    // Importación
    public DbSet<ImportacionLote> ImportacionLotes => Set<ImportacionLote>();
    public DbSet<ImportacionDetalle> ImportacionDetalles => Set<ImportacionDetalle>();

    // Inspecciones
    public DbSet<ServicioInspeccion> ServiciosInspeccion => Set<ServicioInspeccion>();
    public DbSet<AsignacionInspeccion> AsignacionesInspeccion => Set<AsignacionInspeccion>();
    public DbSet<Inspeccion> Inspecciones => Set<Inspeccion>();
    public DbSet<InspeccionRespuesta> InspeccionRespuestas => Set<InspeccionRespuesta>();
    public DbSet<InspeccionFotografia> InspeccionFotografias => Set<InspeccionFotografia>();
    public DbSet<InspeccionHistorial> InspeccionHistoriales => Set<InspeccionHistorial>();

    // Operaciones
    public DbSet<SincronizacionLog> SincronizacionLogs => Set<SincronizacionLog>();
    public DbSet<Catalogo> Catalogos => Set<Catalogo>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Schema principal
        modelBuilder.HasDefaultSchema("sf");

        // Tabla de historial de migraciones en el mismo schema
        modelBuilder.HasAnnotation("Relational:MigrationsHistoryTable", "__EFMigrationsHistory");

        // ── MAPEO DE ENTIDADES EXPLÍCITO ──────────────────────────────────────
        // Necesario para alinear nombres C# (PascalCase) con tablas SQL (snake_case)

        // empresa
        modelBuilder.Entity<Empresa>(e =>
        {
            e.ToTable("empresa");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Codigo).HasColumnName("codigo");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.Rut).HasColumnName("rut");
            e.Property(x => x.LogoUrl).HasColumnName("logo_url");
            e.Property(x => x.TenantSlug).HasColumnName("tenant_slug");
            e.Property(x => x.Activo).HasColumnName("activo");
            e.Property(x => x.Plan).HasColumnName("plan");
            e.Property(x => x.Configuracion).HasColumnName("configuracion");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        });

        // rol
        modelBuilder.Entity<Rol>(e =>
        {
            e.ToTable("rol");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.Codigo).HasColumnName("codigo");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.EsSistema).HasColumnName("es_sistema");
            e.Property(x => x.Activo).HasColumnName("activo");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // permiso
        modelBuilder.Entity<Permiso>(e =>
        {
            e.ToTable("permiso");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Modulo).HasColumnName("modulo");
            e.Property(x => x.Accion).HasColumnName("accion");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // rol_permiso (tabla de unión sin PK autonumérica)
        modelBuilder.Entity<RolPermiso>(e =>
        {
            e.ToTable("rol_permiso");
            e.HasKey(x => new { x.RolId, x.PermisoId });
            e.Property(x => x.RolId).HasColumnName("rol_id");
            e.Property(x => x.PermisoId).HasColumnName("permiso_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // usuario
        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("usuario");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            e.Property(x => x.RolId).HasColumnName("rol_id");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.PasswordHash).HasColumnName("password_hash");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.Apellido).HasColumnName("apellido");
            e.Property(x => x.Telefono).HasColumnName("telefono");
            e.Property(x => x.AvatarUrl).HasColumnName("avatar_url");
            e.Property(x => x.Estado)
                .HasColumnName("estado")
                .HasConversion(new SnakeCaseEnumConverter<EstadoUsuario>());
            e.Property(x => x.UltimoAcceso).HasColumnName("ultimo_acceso");
            e.Property(x => x.IntentosFallidos).HasColumnName("intentos_fallidos");
            e.Property(x => x.BloqueadoHasta).HasColumnName("bloqueado_hasta");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            e.Ignore(x => x.NombreCompleto);
        });

        // refresh_token
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_token");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            // usuario_id y operador_id son mutuamente excluyentes (uno de los dos es no nulo)
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired(false);
            e.Property(x => x.OperadorId).HasColumnName("operador_id").IsRequired(false);
            e.Property(x => x.Token).HasColumnName("token");
            e.Property(x => x.ExpiraEn).HasColumnName("expira_en");
            e.Property(x => x.Revocado).HasColumnName("revocado");
            // IpOrigen es INET en PostgreSQL — se almacena como text en el modelo
            e.Property(x => x.IpOrigen).HasColumnName("ip_origen").HasColumnType("text");
            e.Property(x => x.UserAgent).HasColumnName("user_agent");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            // La tabla refresh_token no tiene updated_at — ignorar esta propiedad heredada
            e.Ignore(x => x.UpdatedAt);
            // Relación con Usuario (nullable — tokens de usuarios web)
            e.HasOne(x => x.Usuario)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(x => x.UsuarioId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
            // Relación con Operador (nullable — tokens de operadores móviles)
            e.HasOne(x => x.Operador)
                .WithMany(o => o.RefreshTokens)
                .HasForeignKey(x => x.OperadorId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // operador
        modelBuilder.Entity<Operador>(e =>
        {
            e.ToTable("operador");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
            e.Property(x => x.CodigoOperador).HasColumnName("codigo_operador");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.Apellido).HasColumnName("apellido");
            e.Property(x => x.Rut).HasColumnName("rut");
            e.Property(x => x.Telefono).HasColumnName("telefono");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.Zona).HasColumnName("zona");
            e.Property(x => x.Localidad).HasColumnName("localidad");
            e.Property(x => x.PasswordHash).HasColumnName("password_hash");
            e.Property(x => x.Activo).HasColumnName("activo");
            e.Property(x => x.FechaUltimaSync).HasColumnName("fecha_ultima_sync");
            e.Property(x => x.DeviceIdRegistrado).HasColumnName("device_id_registrado");
            e.Property(x => x.AppVersionUltima).HasColumnName("app_version_ultima");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            e.Ignore(x => x.NombreCompleto);
        });

        // tipo_inspeccion
        modelBuilder.Entity<TipoInspeccion>(e =>
        {
            e.ToTable("tipo_inspeccion");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            e.Property(x => x.Codigo).HasColumnName("codigo");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Activo).HasColumnName("activo");
            e.Property(x => x.FlujoVersionIdDef).HasColumnName("flujo_version_id_def");
            e.Property(x => x.Icono).HasColumnName("icono");
            e.Property(x => x.Color).HasColumnName("color");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        });

        // flujo
        modelBuilder.Entity<Flujo>(e =>
        {
            e.ToTable("flujo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            e.Property(x => x.TipoInspeccionId).HasColumnName("tipo_inspeccion_id");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Activo).HasColumnName("activo");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        });

        // flujo_version
        modelBuilder.Entity<FlujoVersion>(e =>
        {
            e.ToTable("flujo_version");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FlujoId).HasColumnName("flujo_id");
            e.Property(x => x.NumeroVersion).HasColumnName("numero_version");
            e.Property(x => x.Estado)
                .HasColumnName("estado")
                .HasConversion(new SnakeCaseEnumConverter<EstadoFlujoVersion>());
            e.Property(x => x.DescripcionCambio).HasColumnName("descripcion_cambio");
            e.Property(x => x.PublicadoPor).HasColumnName("publicado_por");
            e.Property(x => x.PublicadoEn).HasColumnName("publicado_en");
            e.Property(x => x.Configuracion).HasColumnName("configuracion");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // flujo_seccion
        modelBuilder.Entity<FlujoSeccion>(e =>
        {
            e.ToTable("flujo_seccion");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FlujoVersionId).HasColumnName("flujo_version_id");
            e.Property(x => x.Codigo).HasColumnName("codigo");
            e.Property(x => x.Titulo).HasColumnName("titulo");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Orden).HasColumnName("orden");
            e.Property(x => x.Visible).HasColumnName("visible");
            e.Property(x => x.CondicionalJson).HasColumnName("condicional_json");
            e.Property(x => x.Icono).HasColumnName("icono");
            e.Property(x => x.Color).HasColumnName("color");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // flujo_pregunta
        modelBuilder.Entity<FlujoPregunta>(e =>
        {
            e.ToTable("flujo_pregunta");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FlujoVersionId).HasColumnName("flujo_version_id");
            e.Property(x => x.SeccionId).HasColumnName("seccion_id");
            e.Property(x => x.Codigo).HasColumnName("codigo");
            e.Property(x => x.Texto).HasColumnName("texto");
            e.Property(x => x.TipoControl)
                .HasColumnName("tipo_control")
                .HasConversion(new SnakeCaseEnumConverter<TipoControl>());
            e.Property(x => x.Placeholder).HasColumnName("placeholder");
            e.Property(x => x.Ayuda).HasColumnName("ayuda");
            e.Property(x => x.Obligatorio).HasColumnName("obligatorio");
            e.Property(x => x.Orden).HasColumnName("orden");
            e.Property(x => x.Visible).HasColumnName("visible");
            e.Property(x => x.Editable).HasColumnName("editable");
            e.Property(x => x.ValorPorDefecto).HasColumnName("valor_por_defecto");
            e.Property(x => x.ValidacionesJson).HasColumnName("validaciones_json");
            e.Property(x => x.ConfiguracionJson).HasColumnName("configuracion_json");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            // Relaciones hacia FlujoRegla son configuradas en FlujoRegla
            // Se ignoran las colecciones inversas para evitar ambigüedad
            e.Ignore(x => x.ReglasOrigen);
            e.Ignore(x => x.ReglasDestino);
        });

        // flujo_opcion
        modelBuilder.Entity<FlujoOpcion>(e =>
        {
            e.ToTable("flujo_opcion");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PreguntaId).HasColumnName("pregunta_id");
            e.Property(x => x.Codigo).HasColumnName("codigo");
            e.Property(x => x.Texto).HasColumnName("texto");
            e.Property(x => x.Orden).HasColumnName("orden");
            e.Property(x => x.Activo).HasColumnName("activo");
            e.Property(x => x.ValorNumerico).HasColumnName("valor_numerico");
            e.Property(x => x.MetadataJson).HasColumnName("metadata_json");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // flujo_regla
        modelBuilder.Entity<FlujoRegla>(e =>
        {
            e.ToTable("flujo_regla");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FlujoVersionId).HasColumnName("flujo_version_id");
            e.Property(x => x.Codigo).HasColumnName("codigo");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.PreguntaOrigenId).HasColumnName("pregunta_origen_id");
            e.Property(x => x.Operador)
                .HasColumnName("operador")
                .HasConversion(new SnakeCaseEnumConverter<OperadorRegla>());
            e.Property(x => x.ValorComparacion).HasColumnName("valor_comparacion");
            e.Property(x => x.ValorComparacionJson).HasColumnName("valor_comparacion_json");
            e.Property(x => x.Accion)
                .HasColumnName("accion")
                .HasConversion(new SnakeCaseEnumConverter<AccionRegla>());
            e.Property(x => x.PreguntaDestinoId).HasColumnName("pregunta_destino_id");
            e.Property(x => x.SeccionDestinoId).HasColumnName("seccion_destino_id");
            e.Property(x => x.ParametrosJson).HasColumnName("parametros_json");
            e.Property(x => x.Orden).HasColumnName("orden");
            e.Property(x => x.Activo).HasColumnName("activo");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            // FK explícitas para evitar ambigüedad en relaciones auto-referenciadas
            e.HasOne(x => x.PreguntaOrigen)
                .WithMany()
                .HasForeignKey(x => x.PreguntaOrigenId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PreguntaDestino)
                .WithMany()
                .HasForeignKey(x => x.PreguntaDestinoId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SeccionDestino)
                .WithMany()
                .HasForeignKey(x => x.SeccionDestinoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // importacion_lote
        modelBuilder.Entity<ImportacionLote>(e =>
        {
            e.ToTable("importacion_lote");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            e.Property(x => x.NombreArchivo).HasColumnName("nombre_archivo");
            e.Property(x => x.NombreOriginal).HasColumnName("nombre_original");
            e.Property(x => x.HashArchivo).HasColumnName("hash_archivo");
            e.Property(x => x.TotalFilas).HasColumnName("total_filas");
            e.Property(x => x.FilasValidas).HasColumnName("filas_validas");
            e.Property(x => x.FilasError).HasColumnName("filas_error");
            e.Property(x => x.FilasOmitidas).HasColumnName("filas_omitidas");
            e.Property(x => x.Estado)
                .HasColumnName("estado")
                .HasConversion(new SnakeCaseEnumConverter<EstadoImportacion>());
            e.Property(x => x.TipoInspeccionId).HasColumnName("tipo_inspeccion_id");
            e.Property(x => x.FlujoVersionId).HasColumnName("flujo_version_id");
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
            e.Property(x => x.Notas).HasColumnName("notas");
            e.Property(x => x.ErrorGeneral).HasColumnName("error_general");
            e.Property(x => x.ConfiguracionMapeo).HasColumnName("configuracion_mapeo");
            e.Property(x => x.ProcesadoEn).HasColumnName("procesado_en");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // importacion_detalle
        modelBuilder.Entity<ImportacionDetalle>(e =>
        {
            e.ToTable("importacion_detalle");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.LoteId).HasColumnName("lote_id");
            e.Property(x => x.NumeroFila).HasColumnName("numero_fila");
            e.Property(x => x.Estado).HasColumnName("estado");
            e.Property(x => x.ErroresJson).HasColumnName("errores_json");
            e.Property(x => x.DatosOriginales).HasColumnName("datos_originales");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // servicio_inspeccion
        modelBuilder.Entity<ServicioInspeccion>(e =>
        {
            e.ToTable("servicio_inspeccion");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            e.Property(x => x.ImportacionLoteId).HasColumnName("importacion_lote_id");
            e.Property(x => x.IdServicio).HasColumnName("id_servicio");
            e.Property(x => x.NumeroMedidor).HasColumnName("numero_medidor");
            e.Property(x => x.Marca).HasColumnName("marca");
            e.Property(x => x.Diametro).HasColumnName("diametro");
            e.Property(x => x.Direccion).HasColumnName("direccion");
            e.Property(x => x.NombreCliente).HasColumnName("nombre_cliente");
            e.Property(x => x.CoordenadaX).HasColumnName("coordenada_x");
            e.Property(x => x.CoordenadaY).HasColumnName("coordenada_y");
            e.Property(x => x.Lote).HasColumnName("lote");
            e.Property(x => x.Localidad).HasColumnName("localidad");
            e.Property(x => x.Ruta).HasColumnName("ruta");
            e.Property(x => x.Libreta).HasColumnName("libreta");
            e.Property(x => x.ObservacionLibre).HasColumnName("observacion_libre");
            e.Property(x => x.Activo).HasColumnName("activo");
            e.Property(x => x.TieneAsignacion).HasColumnName("tiene_asignacion");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // asignacion_inspeccion
        modelBuilder.Entity<AsignacionInspeccion>(e =>
        {
            e.ToTable("asignacion_inspeccion");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            e.Property(x => x.ServicioInspeccionId).HasColumnName("servicio_inspeccion_id");
            e.Property(x => x.OperadorId).HasColumnName("operador_id");
            e.Property(x => x.TipoInspeccionId).HasColumnName("tipo_inspeccion_id");
            e.Property(x => x.FlujoVersionId).HasColumnName("flujo_version_id");
            e.Property(x => x.Estado)
                .HasColumnName("estado")
                .HasConversion(new SnakeCaseEnumConverter<EstadoAsignacion>());
            e.Property(x => x.Prioridad)
                .HasColumnName("prioridad")
                .HasConversion(new SnakeCaseEnumConverter<Prioridad>());
            e.Property(x => x.FechaAsignacion).HasColumnName("fecha_asignacion");
            e.Property(x => x.FechaInicioEsperada).HasColumnName("fecha_inicio_esperada");
            e.Property(x => x.FechaFinEsperada).HasColumnName("fecha_fin_esperada");
            e.Property(x => x.FechaDescarga).HasColumnName("fecha_descarga");
            e.Property(x => x.FechaInicioEjecucion).HasColumnName("fecha_inicio_ejecucion");
            e.Property(x => x.FechaFinalizacion).HasColumnName("fecha_finalizacion");
            e.Property(x => x.Observaciones).HasColumnName("observaciones");
            e.Property(x => x.AsignadoPor).HasColumnName("asignado_por");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            // FK explícita: AsignadoPorUsuario usa la columna asignado_por
            e.HasOne(x => x.AsignadoPorUsuario)
                .WithMany()
                .HasForeignKey(x => x.AsignadoPor)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // inspeccion
        modelBuilder.Entity<Inspeccion>(e =>
        {
            e.ToTable("inspeccion");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            e.Property(x => x.AsignacionId).HasColumnName("asignacion_id");
            e.Property(x => x.OperadorId).HasColumnName("operador_id");
            e.Property(x => x.ServicioInspeccionId).HasColumnName("servicio_inspeccion_id");
            e.Property(x => x.FlujoVersionId).HasColumnName("flujo_version_id");
            e.Property(x => x.Estado)
                .HasColumnName("estado")
                .HasConversion(new SnakeCaseEnumConverter<EstadoInspeccion>());
            e.Property(x => x.FechaInicio).HasColumnName("fecha_inicio");
            e.Property(x => x.FechaFin).HasColumnName("fecha_fin");
            e.Property(x => x.DuracionSegundos).HasColumnName("duracion_segundos");
            e.Property(x => x.CoordXInicio).HasColumnName("coord_x_inicio");
            e.Property(x => x.CoordYInicio).HasColumnName("coord_y_inicio");
            e.Property(x => x.CoordXFin).HasColumnName("coord_x_fin");
            e.Property(x => x.CoordYFin).HasColumnName("coord_y_fin");
            e.Property(x => x.PrecisionGps).HasColumnName("precision_gps");
            e.Property(x => x.DeviceId).HasColumnName("device_id");
            e.Property(x => x.AppVersion).HasColumnName("app_version");
            e.Property(x => x.SincronizadoEn).HasColumnName("sincronizado_en");
            e.Property(x => x.RevisionPor).HasColumnName("revision_por");
            e.Property(x => x.RevisionEn).HasColumnName("revision_en");
            e.Property(x => x.RevisionObservacion).HasColumnName("revision_observacion");
            e.Property(x => x.TotalPreguntas).HasColumnName("total_preguntas");
            e.Property(x => x.TotalRespondidas).HasColumnName("total_respondidas");
            e.Property(x => x.TotalFotografias).HasColumnName("total_fotografias");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            // FK explícita: Revisor usa la columna revision_por
            e.HasOne(x => x.Revisor)
                .WithMany()
                .HasForeignKey(x => x.RevisionPor)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // inspeccion_respuesta
        modelBuilder.Entity<InspeccionRespuesta>(e =>
        {
            e.ToTable("inspeccion_respuesta");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.InspeccionId).HasColumnName("inspeccion_id");
            e.Property(x => x.PreguntaId).HasColumnName("pregunta_id");
            e.Property(x => x.TipoControl)
                .HasColumnName("tipo_control")
                .HasConversion(new SnakeCaseEnumConverter<TipoControl>());
            e.Property(x => x.ValorTexto).HasColumnName("valor_texto");
            e.Property(x => x.ValorEntero).HasColumnName("valor_entero");
            e.Property(x => x.ValorDecimal).HasColumnName("valor_decimal");
            e.Property(x => x.ValorFecha).HasColumnName("valor_fecha");
            e.Property(x => x.ValorHora).HasColumnName("valor_hora");
            e.Property(x => x.ValorFechaHora).HasColumnName("valor_fecha_hora");
            e.Property(x => x.ValorBooleano).HasColumnName("valor_booleano");
            e.Property(x => x.ValorJson).HasColumnName("valor_json");
            e.Property(x => x.EsValido).HasColumnName("es_valido");
            e.Property(x => x.ErroresValidacion).HasColumnName("errores_validacion");
            e.Property(x => x.RespondidaEn).HasColumnName("respondida_en");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // inspeccion_fotografia
        modelBuilder.Entity<InspeccionFotografia>(e =>
        {
            e.ToTable("inspeccion_fotografia");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.InspeccionId).HasColumnName("inspeccion_id");
            e.Property(x => x.PreguntaId).HasColumnName("pregunta_id");
            e.Property(x => x.NombreArchivo).HasColumnName("nombre_archivo");
            e.Property(x => x.RutaAlmacenamiento).HasColumnName("ruta_almacenamiento");
            e.Property(x => x.UrlPublica).HasColumnName("url_publica");
            e.Property(x => x.TamanioBytes).HasColumnName("tamanio_bytes");
            e.Property(x => x.Ancho).HasColumnName("ancho");
            e.Property(x => x.Alto).HasColumnName("alto");
            e.Property(x => x.Formato).HasColumnName("formato");
            e.Property(x => x.CoordenadaX).HasColumnName("coordenada_x");
            e.Property(x => x.CoordenadaY).HasColumnName("coordenada_y");
            e.Property(x => x.PrecisionGps).HasColumnName("precision_gps");
            e.Property(x => x.TieneMarcaAgua).HasColumnName("tiene_marca_agua");
            e.Property(x => x.HashSha256).HasColumnName("hash_sha256");
            e.Property(x => x.Orden).HasColumnName("orden");
            e.Property(x => x.MetadataJson).HasColumnName("metadata_json");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // inspeccion_historial
        modelBuilder.Entity<InspeccionHistorial>(e =>
        {
            e.ToTable("inspeccion_historial");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.InspeccionId).HasColumnName("inspeccion_id");
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
            e.Property(x => x.OperadorId).HasColumnName("operador_id");
            e.Property(x => x.Accion).HasColumnName("accion");
            e.Property(x => x.EstadoAnterior)
                .HasColumnName("estado_anterior")
                .HasConversion(new SnakeCaseEnumConverter<EstadoInspeccion>());
            e.Property(x => x.EstadoNuevo)
                .HasColumnName("estado_nuevo")
                .HasConversion(new SnakeCaseEnumConverter<EstadoInspeccion>());
            e.Property(x => x.Observacion).HasColumnName("observacion");
            e.Property(x => x.MetadataJson).HasColumnName("metadata_json");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // sincronizacion_log
        modelBuilder.Entity<SincronizacionLog>(e =>
        {
            e.ToTable("sincronizacion_log");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            e.Property(x => x.OperadorId).HasColumnName("operador_id");
            e.Property(x => x.DeviceId).HasColumnName("device_id");
            e.Property(x => x.Tipo)
                .HasColumnName("tipo")
                .HasConversion(new SnakeCaseEnumConverter<TipoSync>());
            e.Property(x => x.PayloadHash).HasColumnName("payload_hash");
            e.Property(x => x.RegistrosEnviados).HasColumnName("registros_enviados");
            e.Property(x => x.RegistrosRecibidos).HasColumnName("registros_recibidos");
            e.Property(x => x.FotosEnviadas).HasColumnName("fotos_enviadas");
            e.Property(x => x.BytesTransferidos).HasColumnName("bytes_transferidos");
            e.Property(x => x.DuracionMs).HasColumnName("duracion_ms");
            e.Property(x => x.Exitoso).HasColumnName("exitoso");
            e.Property(x => x.ErroresJson).HasColumnName("errores_json");
            e.Property(x => x.IpOrigen).HasColumnName("ip_origen").HasColumnType("text");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // catalogo
        modelBuilder.Entity<Catalogo>(e =>
        {
            e.ToTable("catalogo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            e.Property(x => x.Tipo).HasColumnName("tipo");
            e.Property(x => x.Codigo).HasColumnName("codigo");
            e.Property(x => x.Texto).HasColumnName("texto");
            e.Property(x => x.Orden).HasColumnName("orden");
            e.Property(x => x.Activo).HasColumnName("activo");
            e.Property(x => x.Metadata).HasColumnName("metadata");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // auditoria
        modelBuilder.Entity<Auditoria>(e =>
        {
            e.ToTable("auditoria");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
            e.Property(x => x.OperadorId).HasColumnName("operador_id");
            e.Property(x => x.Entidad).HasColumnName("entidad");
            e.Property(x => x.EntidadId).HasColumnName("entidad_id");
            e.Property(x => x.Accion).HasColumnName("accion");
            e.Property(x => x.DatosAnteriores).HasColumnName("datos_anteriores");
            e.Property(x => x.DatosNuevos).HasColumnName("datos_nuevos");
            e.Property(x => x.Ip).HasColumnName("ip").HasColumnType("text");
            e.Property(x => x.UserAgent).HasColumnName("user_agent");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity &&
                        (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var entity = (BaseEntity)entry.Entity;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            if (entry.State == EntityState.Added)
                entity.CreatedAt = DateTimeOffset.UtcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}


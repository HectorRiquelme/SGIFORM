using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SgiForm.Domain.Entities;
using SgiForm.Domain.Enums;
using SgiForm.Infrastructure.Persistence;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SgiForm.Tests;

/// <summary>
/// WebApplicationFactory configurada con BD InMemory para tests de integración.
/// Carga datos semilla y provee métodos helper para autenticación.
/// </summary>
public class TestFixture : WebApplicationFactory<Program>
{
    // IDs fijos de datos semilla
    public static readonly Guid EmpresaId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid AdminId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid RolAdminId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid RolSupervisorId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid Operador1Id = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid Operador2Id = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    public static readonly Guid TipoInsp1Id = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid FlujoId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    public static readonly Guid FlujoVersionId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    public static readonly Guid Seccion1Id = Guid.Parse("60000000-0000-0000-0000-000000000001");

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Quitar TODOS los servicios relacionados a DbContext (EF Core 9+ registra más descriptores)
            var dbDescriptors = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") == true ||
                    d.ServiceType == typeof(AppDbContext))
                .ToList();
            foreach (var d in dbDescriptors) services.Remove(d);

            // Quitar health checks de PostgreSQL (no hay BD real en tests)
            var healthDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("HealthCheck") == true)
                .ToList();
            foreach (var hd in healthDescriptors) services.Remove(hd);
            services.AddHealthChecks(); // health checks vacíos

            // Reemplazar con InMemory (nombre único para aislar cada fixture)
            var dbName = "SgiFormTests_" + Guid.NewGuid();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(dbName));
        });
    }

    // Sobrescribir para sembrar datos después de que se construya el host
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        // Solo sembrar si está vacío
        if (!db.Empresas.Any())
            SeedTestData(db);

        return host;
    }

    private static void SeedTestData(AppDbContext db)
    {
        // Empresa
        db.Empresas.Add(new Empresa
        {
            Id = EmpresaId, Codigo = "TEST", Nombre = "Empresa Test",
            TenantSlug = "test", Plan = "enterprise"
        });

        // Roles
        db.Roles.Add(new Rol { Id = RolAdminId, EmpresaId = EmpresaId, Nombre = "Administrador", Codigo = "admin" });
        db.Roles.Add(new Rol { Id = RolSupervisorId, EmpresaId = EmpresaId, Nombre = "Supervisor", Codigo = "supervisor" });

        // Permisos
        var permiso = new Permiso { Modulo = "admin", Accion = "full", Descripcion = "Full access" };
        db.Permisos.Add(permiso);
        db.RolPermisos.Add(new RolPermiso { RolId = RolAdminId, PermisoId = permiso.Id });

        // Usuario admin
        db.Usuarios.Add(new Usuario
        {
            Id = AdminId, EmpresaId = EmpresaId, RolId = RolAdminId,
            Email = "admin@test.cl",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123"),
            Nombre = "Admin", Apellido = "Test", Estado = EstadoUsuario.Activo
        });

        // Operadores
        db.Operadores.Add(new Operador
        {
            Id = Operador1Id, EmpresaId = EmpresaId, CodigoOperador = "OP001",
            Nombre = "Carlos", Apellido = "Test", Activo = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Op@123")
        });
        db.Operadores.Add(new Operador
        {
            Id = Operador2Id, EmpresaId = EmpresaId, CodigoOperador = "OP002",
            Nombre = "Ana", Apellido = "Test", Activo = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Op@123")
        });

        // Tipo inspección
        db.TiposInspeccion.Add(new TipoInspeccion
        {
            Id = TipoInsp1Id, EmpresaId = EmpresaId, Codigo = "INSP-MED",
            Nombre = "Inspección de Medidor", Activo = true
        });

        // Flujo con versión publicada
        db.Flujos.Add(new Flujo
        {
            Id = FlujoId, EmpresaId = EmpresaId, TipoInspeccionId = TipoInsp1Id,
            Nombre = "Flujo Test", Activo = true
        });
        db.FlujoVersiones.Add(new FlujoVersion
        {
            Id = FlujoVersionId, FlujoId = FlujoId, NumeroVersion = 1,
            Estado = EstadoFlujoVersion.Publicado
        });

        // Sección + Preguntas
        db.FlujoSecciones.Add(new FlujoSeccion
        {
            Id = Seccion1Id, FlujoVersionId = FlujoVersionId,
            Codigo = "SEC_01", Titulo = "Acceso", Orden = 1
        });

        var pregAcceso = new FlujoPregunta
        {
            Id = Guid.Parse("70000000-0000-0000-0000-000000000001"),
            FlujoVersionId = FlujoVersionId, SeccionId = Seccion1Id,
            Codigo = "p_acceso", Texto = "¿Acceso al domicilio?",
            TipoControl = TipoControl.SiNo, Obligatorio = true, Orden = 1
        };
        var pregMotivo = new FlujoPregunta
        {
            Id = Guid.Parse("70000000-0000-0000-0000-000000000002"),
            FlujoVersionId = FlujoVersionId, SeccionId = Seccion1Id,
            Codigo = "p_motivo", Texto = "Motivo de no acceso",
            TipoControl = TipoControl.SeleccionUnica, Obligatorio = false,
            Orden = 2, Visible = false
        };
        db.FlujoPreguntas.AddRange(pregAcceso, pregMotivo);

        // Regla: si acceso=false → mostrar motivo
        db.FlujoReglas.Add(new FlujoRegla
        {
            FlujoVersionId = FlujoVersionId,
            PreguntaOrigenId = pregAcceso.Id, Operador = OperadorRegla.Eq,
            ValorComparacion = "false", Accion = AccionRegla.Mostrar,
            PreguntaDestinoId = pregMotivo.Id, Orden = 1
        });

        // Servicios
        for (int i = 1; i <= 10; i++)
        {
            db.ServiciosInspeccion.Add(new ServicioInspeccion
            {
                EmpresaId = EmpresaId,
                IdServicio = $"SRV-{i:D4}",
                NumeroMedidor = $"{100000 + i}",
                Marca = "Actaris",
                Diametro = "13",
                Direccion = $"Calle Test {i * 100}",
                NombreCliente = $"Cliente {i}",
                Localidad = i <= 5 ? "La Serena" : "Coquimbo",
                Ruta = $"R0{(i % 3) + 1}",
                Lote = "L2024-001",
                Activo = true
            });
        }

        db.SaveChanges();
    }

    /// <summary>Login como admin y devuelve HttpClient con JWT configurado.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.PostAsJsonAsync("api/v1/auth/login",
            new { email = "admin@test.cl", password = "Test@123" }, JsonOpts);

        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = data.GetProperty("access_token").GetString()!;

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

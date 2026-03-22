using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using SgiForm.Infrastructure.Persistence;
using SgiForm.Infrastructure.Services;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ─── Logging ──────────────────────────────────────────────────────────────
    if (builder.Environment.EnvironmentName != "Testing")
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        builder.Host.UseSerilog((ctx, lc) =>
            lc.ReadFrom.Configuration(ctx.Configuration));
    }

    // ─── Base de datos PostgreSQL + EF Core ───────────────────────────────────
    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseNpgsql(
            builder.Configuration.GetConnectionString("Default"),
            npgsql => npgsql.MigrationsAssembly("SgiForm.Infrastructure")
        )
        // Columnas mapeadas explícitamente en AppDbContext.OnModelCreating
    );

    // ─── JWT Authentication ───────────────────────────────────────────────────
    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("Jwt:Key no configurado");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opt =>
        {
            opt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });

    builder.Services.AddAuthorization();

    // ─── CORS ─────────────────────────────────────────────────────────────────
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    builder.Services.AddCors(opt => opt.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()));

    // ─── Rate Limiting ────────────────────────────────────────────────────────
    // Usa el middleware nativo de ASP.NET Core 8 (sin NuGet adicional).
    // En Testing se usan límites permisivos para no interferir con los tests.
    bool isTesting = builder.Environment.EnvironmentName == "Testing";
    var rl = builder.Configuration.GetSection("RateLimiting");

    builder.Services.AddRateLimiter(opt =>
    {
        opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        opt.OnRejected = async (ctx, _) =>
        {
            ctx.HttpContext.Response.ContentType = "application/json";
            await ctx.HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Demasiadas solicitudes. Espere un momento antes de reintentar.",
                retry_after_seconds = 60
            });
        };

        // Política "auth": para login y login-movil
        // Límite: 10 req/min por IP en producción (1000 en Testing)
        opt.AddPolicy("auth", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anon",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit        = isTesting ? 1000 : rl.GetValue("AuthPermitLimit", 10),
                    Window             = TimeSpan.FromSeconds(rl.GetValue("AuthWindowSeconds", 60)),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit         = 0
                }));

        // Política "api": para endpoints generales autenticados
        // Límite: 120 req/min por IP (2 req/seg) en producción
        opt.AddPolicy("api", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anon",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit        = isTesting ? 5000 : rl.GetValue("ApiPermitLimit", 120),
                    Window             = TimeSpan.FromSeconds(rl.GetValue("ApiWindowSeconds", 60)),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit         = 5
                }));

        // Política "sync": para uploads de fotos (más permisiva, son operadores en campo)
        opt.AddPolicy("sync", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.User.FindFirst("operador_id")?.Value
                              ?? context.Connection.RemoteIpAddress?.ToString()
                              ?? "anon",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit        = isTesting ? 5000 : rl.GetValue("SyncPermitLimit", 30),
                    Window             = TimeSpan.FromSeconds(rl.GetValue("SyncWindowSeconds", 60)),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit         = 10
                }));
    });

    // ─── Servicios de aplicación ──────────────────────────────────────────────
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IExcelImportService, ExcelImportService>();
    builder.Services.AddScoped<IFlowValidatorService, FlowValidatorService>();

    // ─── Controllers + JSON ───────────────────────────────────────────────────
    builder.Services.AddControllers()
        .AddJsonOptions(opt =>
        {
            opt.JsonSerializerOptions.PropertyNamingPolicy =
                System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
            opt.JsonSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

    // ─── Swagger / OpenAPI ────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "SgiForm API",
            Version = "v1",
            Description = "API REST para el sistema de inspecciones técnicas en terreno SgiForm"
        });

        // Habilitar JWT en Swagger UI
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Ingrese el token JWT: Bearer {token}"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // ─── Health checks ────────────────────────────────────────────────────────
    var healthBuilder = builder.Services.AddHealthChecks();
    var connStr = builder.Configuration.GetConnectionString("Default");
    if (!string.IsNullOrEmpty(connStr) && builder.Environment.EnvironmentName != "Testing")
        healthBuilder.AddNpgSql(connStr);

    var app = builder.Build();

    // ─── Middleware pipeline ──────────────────────────────────────────────────
    if (app.Environment.EnvironmentName != "Testing")
        app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "SgiForm API v1");
            c.RoutePrefix = string.Empty; // Swagger en raíz
        });
    }

    app.UseHttpsRedirection();
    app.UseCors();
    app.UseRateLimiter();       // ← Rate limiting antes de auth
    app.UseAuthentication();
    app.UseAuthorization();

    // Middleware de errores global
    app.UseExceptionHandler(errApp =>
    {
        errApp.Run(async ctx =>
        {
            ctx.Response.StatusCode = 500;
            ctx.Response.ContentType = "application/json";
            var err = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
            if (err != null)
            {
                Log.Error(err.Error, "Unhandled exception");
                await ctx.Response.WriteAsJsonAsync(new
                {
                    error = "Error interno del servidor",
                    detail = app.Environment.IsDevelopment() ? err.Error.Message : null,
                    inner = app.Environment.IsDevelopment() ? err.Error.InnerException?.Message : null,
                    inner2 = app.Environment.IsDevelopment() ? err.Error.InnerException?.InnerException?.Message : null
                });
            }
        });
    });

    app.MapControllers();
    // Health check restringido a red local (no expuesto públicamente)
    // Nota: ::1 (IPv6) no se usa en RequireHost — localhost cubre ambos en la mayoría de entornos
    app.MapHealthChecks("/health").RequireHost("localhost", "127.0.0.1");

    // ─── Auto-run migrations en desarrollo ────────────────────────────────────
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Comentar en producción - usar migraciones manuales
        // await db.Database.MigrateAsync();
    }

    Log.Information("SgiForm API iniciando en {Env}", app.Environment.EnvironmentName);
    await app.RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"API fatal error: {ex.Message}");
}

// Necesario para WebApplicationFactory<Program> en tests de integración
public partial class Program { }

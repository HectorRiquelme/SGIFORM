# CLAUDE.md — Guía de contexto para Claude Code

> Este archivo es la fuente de verdad para que cualquier instancia de Claude Code entienda el proyecto sin requerir explicación adicional del usuario.

---

## Identidad del proyecto

| Campo | Valor |
|-------|-------|
| **Nombre comercial** | SGI-FORM |
| **Nombre técnico** | SgiForm |
| **Namespaces C#** | `SgiForm.*` |
| **Solución** | `SgiForm.sln` |
| **Repositorio** | https://github.com/HectorRiquelme/SGIFORM |
| **Versión actual** | 1.0.0 |
| **SDK** | .NET 8.0.319 (fijado en `global.json`) |

> **Regla crítica de naming**: El branding comercial es **SGI-FORM**. Los namespaces, carpetas y solución usan `SgiForm`. La conversión SgiForm → SgiForm ya fue completada en todos los archivos. No renombrar nuevamente.

---

## Stack tecnológico exacto

| Componente | Tecnología | Versión |
|---|---|---|
| API REST | ASP.NET Core Web API | 8.0 |
| ORM | Entity Framework Core + Npgsql | 8.0 |
| Base de datos | PostgreSQL | 16 (producción, servicio `postgresql-x64-16`) / 17 (desarrollo Docker) |
| Frontend admin | Blazor Server | 8.0 |
| App móvil | .NET MAUI Android | 8.0 |
| BD móvil | SQLite (sqlite-net-pcl 1.9.172) | — |
| Auth | JWT Bearer + BCrypt.Net | HS256, 60 min |
| Excel | ClosedXML | 0.102.1 |
| Logging | Serilog | 8.0 |
| Tests | xUnit + FluentAssertions + WebApplicationFactory | — |
| Rate limiting | ASP.NET Core 8 nativo | — |

---

## Estructura de proyectos

```
src/
  SgiForm.Domain/         # Entidades, enums — sin dependencias externas
  SgiForm.Application/    # Placeholder — DTOs, interfaces (mayormente vacío)
  SgiForm.Infrastructure/ # EF Core, AppDbContext, AuthService, ExcelImportService, FlowValidatorService
  SgiForm.Api/            # 12 controllers REST, Program.cs, appsettings
  SgiForm.Web/            # Blazor Server — 13 páginas, ApiClient, AuthStateService
  SgiForm.Mobile/         # MAUI Android — MVVM, SQLite offline, SyncService, FlowEngine
shared/
  SgiForm.Contracts/      # Placeholder DTOs compartidos (vacío)
tests/
  SgiForm.Tests/          # 46 tests de integración con WebApplicationFactory + InMemory DB
database/
  01_schema.sql           # DDL completo, schema `sf`, 21 tablas
  02_seed.sql             # Datos iniciales: empresa demo, admin, tipos inspección
  03_operador_refresh_token.sql  # Migración: columna operador_id en refresh_token
deploy/
  deploy-server.ps1       # Automatización completa de despliegue
  validate-deployment.ps1 # 26 checks PASS/WARN/FAIL
  rollback.ps1            # Rollback interactivo o dirigido
  DEPLOY_MANUAL.md        # Manual paso a paso (13 secciones)
```

---

## Convenciones de código

### C# / .NET
- **JSON**: snake_case (`PropertyNamingPolicy.SnakeCaseLower`), nulls ignorados
- **Rutas API**: `/api/v1/{recurso}` en plural
- **Enums en BD**: `VARCHAR` con `SnakeCaseEnumConverter` (ej: `EnEjecucion` → `"en_ejecucion"`)
- **PKs**: UUID v4 en todas las entidades
- **Soft delete**: campo `DeletedAt` en entidades que extienden `SoftDeleteEntity`
- **Multitenant**: filtro por `empresa_id` en TODOS los queries
- **Entorno Testing**: `ASPNETCORE_ENVIRONMENT = "Testing"` desactiva Serilog y PostgreSQL health check

### PostgreSQL / SQL
- Schema: `sf` (todas las tablas viven aquí)
- Nombres de tablas y columnas: snake_case
- Extensiones requeridas: `uuid-ossp`, `pg_trgm`, `unaccent`
- BD desarrollo (Docker): puerto `5434`, DB `sgiform`, user `sgiform`, pwd `SgiForm2024!`
- BD producción (nativo): puerto `5432`, mismas BD/user, password en variable de entorno

### Seguridad
- **NUNCA** secretos en código ni appsettings.json commiteado con valores reales
- Secretos de producción: variables de entorno del AppPool IIS
- JWT Key mínimo 64 bytes, generada con `RandomNumberGenerator.Fill`
- Enum parsing externo: siempre `Enum.TryParse`, nunca `Enum.Parse`
- Health check `/health` restringido a `localhost` y `127.0.0.1`

---

## Entidades principales

| Entidad | Tabla | Descripción |
|---------|-------|-------------|
| `Empresa` | `sf.empresa` | Tenant raíz (multitenant) |
| `Usuario` | `sf.usuario` | Usuarios web con rol |
| `Rol` / `Permiso` | `sf.rol` / `sf.permiso` | RBAC |
| `RefreshToken` | `sf.refresh_token` | Tokens revocables web+móvil (usuario_id OR operador_id) |
| `Operador` | `sf.operador` | Operadores de campo (login móvil independiente) |
| `Flujo` / `FlujoVersion` | `sf.flujo` / `sf.flujo_version` | Motor de formularios dinámicos con versionado |
| `FlujoSeccion` / `FlujoPregunta` | — | Estructura del formulario |
| `FlujoRegla` | `sf.flujo_regla` | Lógica condicional: trigger → acción |
| `ServicioInspeccion` | `sf.servicio_inspeccion` | Servicios importados desde Excel |
| `AsignacionInspeccion` | `sf.asignacion_inspeccion` | Asignación operador ↔ servicio |
| `Inspeccion` | `sf.inspeccion` | Inspección ejecutada |
| `InspeccionRespuesta` | `sf.inspeccion_respuesta` | Respuestas a preguntas del flujo |
| `InspeccionFotografia` | `sf.inspeccion_fotografia` | Fotos asociadas a inspección |

---

## Servicios registrados en DI

```csharp
// Scoped
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IExcelImportService, ExcelImportService>();
builder.Services.AddScoped<IFlowValidatorService, FlowValidatorService>();
```

---

## Endpoints API (resumen)

| Controller | Ruta base | Auth |
|---|---|---|
| AuthController | `/api/v1/auth` | Público (login) / JWT (refresh, logout) |
| OperadoresController | `/api/v1/operadores` | JWT |
| UsuariosController | `/api/v1/usuarios` | JWT |
| TiposInspeccionController | `/api/v1/tipos-inspeccion` | JWT |
| FlujoController | `/api/v1/flujos` | JWT |
| ImportacionController | `/api/v1/importaciones` | JWT |
| ServiciosController | `/api/v1/servicios` | JWT |
| AsignacionController | `/api/v1/asignaciones` | JWT |
| InspeccionesController | `/api/v1/inspecciones` | JWT |
| SyncController | `/api/v1/sync` | JWT (rate limit: `sync`) |
| DashboardController | `/api/v1/dashboard` | JWT |
| ReportesController | `/api/v1/reportes` | JWT |

---

## Rate limiting (políticas activas)

| Política | Límite producción | Límite Testing | Aplica a |
|---|---|---|---|
| `auth` | 10 req/min por IP | 1000 | Login, LoginMovil, Refresh |
| `api` | 120 req/min por IP | 5000 | Endpoints generales |
| `sync` | 30 req/min por operador_id | 5000 | SyncController |

---

## Tests — estado actual

- **Total**: 46 tests, 0 failures
- **Framework**: xUnit + WebApplicationFactory<Program> + InMemory DB
- **Entorno**: `Testing` (sin PostgreSQL, sin Serilog, rate limits permisivos)
- **Seed**: empresa "test", admin `admin@test.cl / Test@123`, operadores OP001/OP002 con `Op@123`, 10 servicios, 1 flujo publicado con regla condicional
- **Ejecutar**: `dotnet test tests/SgiForm.Tests/SgiForm.Tests.csproj -v normal`

---

## Cómo trabajar con este proyecto

### Desarrollo local
```powershell
# Levantar PostgreSQL (Docker)
docker run -d --name sgiform_postgres -p 5434:5432 \
  -e POSTGRES_DB=sgiform -e POSTGRES_USER=sgiform \
  -e POSTGRES_PASSWORD=SgiForm2024! postgres:17

# Ejecutar scripts SQL
psql -U sgiform -h localhost -p 5434 -d sgiform -f database/01_schema.sql
psql -U sgiform -h localhost -p 5434 -d sgiform -f database/02_seed.sql

# Iniciar API
cd src/SgiForm.Api && dotnet run

# Iniciar Web
cd src/SgiForm.Web && dotnet run
```

### Tests
```powershell
dotnet test tests/SgiForm.Tests/ -v normal
# Resultado esperado: 46 passed, 0 failed
```

### Despliegue producción
```powershell
# Script automatizado (Windows Server, Admin)
.\deploy\deploy-server.ps1 -ApiPublishPath "C:\publish\api" -WebPublishPath "C:\publish\web"
.\deploy\validate-deployment.ps1
```

## Producción — estado real (2026-03-24)

| Componente | Detalle |
|---|---|
| Servidor | Windows Server 2019 (10.0.17763) |
| PostgreSQL | Servicio `postgresql-x64-16` — puerto 5432 |
| API IIS | AppPool `SgiFormApi` — puerto **5001** |
| Web IIS | AppPool `SgiFormWeb` — puerto **8080** |
| Archivos | `C:\SgiForm\publish\{api,web}` |
| Logs | `C:\SgiForm\logs\sgiform-YYYYMMDD.log` (Serilog) |
| Admin seed | `admin@sanitaria-demo.cl` — empresa slug: `sanitaria-demo` |
| Login móvil | Requiere `empresa_slug` en el body (`sanitaria-demo`) |

**Fix aplicado en producción** (no requería recompilación):
- `ip_origen` en `sf.refresh_token` y `sf.sincronizacion_log` cambiado de `INET` a `TEXT` via ALTER TABLE
- `AllowedHosts` en `appsettings.Production.json` cambiado a `"*"` (configurar al dominio real cuando se tenga)

# SGI-FORM

**Gestion inteligente de inspecciones en terreno.**

> Nombre tecnico del proyecto: SanitasField

Sistema SaaS B2B para empresas sanitarias. Gestiona el ciclo completo de inspecciones de medidores de agua: carga masiva desde Excel, formularios dinamicos con logica condicional, asignacion a operadores, ejecucion offline en Android, sincronizacion, control de calidad y reportes.

## Stack

| Componente | Tecnologia |
|---|---|
| API | ASP.NET Core 8 + EF Core 8 |
| Base de datos | PostgreSQL 17 (Docker, puerto 5434) |
| Web admin | Blazor Server (.NET 8) |
| App movil | .NET MAUI Android (offline-first + SQLite) |
| Auth | JWT Bearer + BCrypt |
| Excel | ClosedXML |
| Tests | xUnit + FluentAssertions (43 tests) |

## Estructura

```
SanitasField.sln
|-- database/01_schema.sql              # Schema PostgreSQL (25 tablas)
|-- database/02_seed.sql                # Datos demo
|-- src/SanitasField.Domain/            # Entidades y enums
|-- src/SanitasField.Infrastructure/    # EF Core, AuthService, ExcelImport
|-- src/SanitasField.Api/               # 12 Controllers REST
|-- src/SanitasField.Web/               # 13 paginas Blazor
|-- src/SanitasField.Mobile/            # App Android MAUI offline-first
|-- tests/SanitasField.Tests/           # 43 tests de integracion
```

## Inicio rapido

### 1. PostgreSQL (Docker)

```bash
docker run -d --name sanitasfield_postgres \
  -e POSTGRES_USER=sanitasfield \
  -e POSTGRES_PASSWORD=SanitasField2024! \
  -e POSTGRES_DB=sanitasfield \
  -p 5434:5432 --restart unless-stopped postgres:17

docker exec -i sanitasfield_postgres psql -U sanitasfield -d sanitasfield < database/01_schema.sql
docker exec -i sanitasfield_postgres psql -U sanitasfield -d sanitasfield < database/02_seed.sql
```

### 2. API (puerto 5043)

```bash
cd src/SanitasField.Api
dotnet run --launch-profile http
# http://localhost:5043
```

### 3. Web (puerto 5054)

```bash
cd src/SanitasField.Web
dotnet run --launch-profile http
# http://localhost:5054
```

### 4. Tests

```bash
dotnet test tests/SanitasField.Tests/SanitasField.Tests.csproj
# 43/43 passed
```

### 5. App Android (opcional)

```bash
dotnet workload install maui-android
set ANDROID_HOME=C:\Program Files (x86)\Android\android-sdk
dotnet build src/SanitasField.Mobile/SanitasField.Mobile.csproj -f net8.0-android -c Debug -t:Run
```

## Credenciales demo

**Web** (http://localhost:5054):

| Email | Password | Rol |
|---|---|---|
| `admin@sanitaria-demo.cl` | `Admin@2024!` | Administrador |
| `hector@sanitaria-demo.cl` | `Hector@2024!` | Supervisor |

**Movil** (URL API: `http://10.0.2.2:5043` desde emulador):

| Codigo | Empresa | Password |
|---|---|---|
| `OP001` | `sanitaria-demo` | `Admin@2024!` |
| `OP002` | `sanitaria-demo` | `Admin@2024!` |
| `OP003` | `sanitaria-demo` | `Admin@2024!` |
| `OP100` | `sanitaria-demo` | `Campo@2024!` |

## Documentacion

| Documento | Descripcion |
|---|---|
| `DOCUMENTACION_TECNICA.md` | Arquitectura completa, decisiones, diagramas, glosario |
| `DOCUMENTACION_TECNICA.pdf` | Version PDF con diagramas renderizados |
| `PROMPT_MAESTRO_CONTINUIDAD.md` | Prompt de continuidad para nuevos chats de desarrollo |

## Endpoints principales

```
POST /api/v1/auth/login              # Login web
POST /api/v1/auth/login-movil        # Login operador
POST /api/v1/auth/refresh            # Renovar token
GET  /api/v1/operadores              # Listar operadores
GET  /api/v1/flujos                  # Listar flujos
POST /api/v1/importaciones/upload    # Subir Excel
POST /api/v1/asignaciones/masiva     # Asignacion masiva
GET  /api/v1/sync/download           # Descargar asignaciones (movil)
POST /api/v1/sync/upload             # Subir inspecciones (movil)
GET  /api/v1/dashboard/resumen       # KPIs
GET  /api/v1/reportes/excel          # Exportar reporte
```

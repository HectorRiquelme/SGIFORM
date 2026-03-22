# PROMPT DE CONTINUACION — SGI-FORM (OBSOLETO)

> **NOTA**: Este documento ha sido reemplazado por `PROMPT_MAESTRO_CONTINUIDAD.md` que contiene informacion mas completa y actualizada. Usar ese documento para nuevos chats.

> Copia y pega este prompt completo en un chat nuevo para continuar el desarrollo.

---

## ROL

Actua como un arquitecto de software senior, experto en .NET 8, ASP.NET Core, Blazor Server, .NET MAUI Android, PostgreSQL, Entity Framework Core, sistemas offline-first, motores de formularios dinamicos y sincronizacion movil.

---

## PROYECTO EXISTENTE

Estoy trabajando en un proyecto llamado **SgiForm**, un sistema completo de inspecciones tecnicas en terreno para empresas sanitarias. El proyecto ya esta construido y funcional. Necesito que continues el desarrollo desde donde quedo.

### Ubicacion del proyecto

```
C:\Users\hecto\TRABAJO\dev_ia\kobotoolbox
```

### Que es SgiForm

Plataforma SaaS B2B que permite a empresas sanitarias ejecutar campanas masivas de inspeccion de medidores de agua en terreno. Gestiona el ciclo completo: carga masiva de servicios desde Excel, configuracion de formularios dinamicos con logica condicional, asignacion a operadores, ejecucion offline en Android, sincronizacion, control de calidad y reportes.

### Stack tecnologico

- **.NET 8** (fijado en `global.json` a version 8.0.319)
- **ASP.NET Core Web API** — Backend REST con JWT, Swagger, Serilog
- **Entity Framework Core 8** — ORM contra PostgreSQL
- **PostgreSQL 17** — BD principal, corre en Docker container `sgiform_postgres` puerto 5434
- **Blazor Server** — Admin web en puerto 5054
- **NET MAUI Android** — App movil offline-first con SQLite
- **ClosedXML** — Import/export Excel
- **BCrypt.Net** — Hash de contrasenas
- **xUnit + FluentAssertions** — 40 tests de integracion (100% passing)

### Estructura de la solucion

```
SgiForm.sln
|
|-- src/SgiForm.Domain/            Entidades (BaseEntity, Empresa, Usuario, Operador, Flujo*, Inspeccion*) + 10 Enums
|-- src/SgiForm.Application/       Placeholder (Class1.cs) — logica esta en controllers
|-- src/SgiForm.Infrastructure/    EF Core AppDbContext (654 lineas mapeo explicito), AuthService (JWT), ExcelImportService, SnakeCaseEnumConverter
|-- src/SgiForm.Api/               12 Controllers REST + Program.cs (187 lineas)
|-- src/SgiForm.Web/               Blazor Server: 13 paginas + 2 shared components + AuthStateService + ApiClient
|-- src/SgiForm.Mobile/            MAUI Android: 3 ViewModels, 4 Views, FlowEngine, SyncService, AppDatabase (SQLite)
|-- shared/SgiForm.Contracts/      Placeholder (Class1.cs)
|-- tests/SgiForm.Tests/           40 tests integracion con WebApplicationFactory + InMemory DB
|-- database/01_schema.sql              Schema PostgreSQL completo (885 lineas, schema sf.*)
|-- database/02_seed.sql                Datos demo (601 lineas: empresa, roles, permisos, operadores, flujo con 6 secciones, 20 preguntas, 10 reglas)
```

### Controllers API (12)

| Controller | Ruta | Funciona |
|---|---|---|
| AuthController | /api/v1/auth | Login web + movil + refresh + logout |
| OperadoresController | /api/v1/operadores | CRUD + soft delete |
| UsuariosController | /api/v1/usuarios | CRUD + roles |
| TiposInspeccionController | /api/v1/tipos-inspeccion | CRUD |
| FlujoController | /api/v1/flujos | CRUD flujos + versiones + secciones + preguntas + reglas + publicar |
| ImportacionController | /api/v1/importaciones | Upload Excel + preview + confirmar |
| ServiciosController | /api/v1/servicios | Listado con filtros + paginacion |
| AsignacionController | /api/v1/asignaciones | Individual + masiva + cambio estado |
| InspeccionesController | /api/v1/inspecciones | Listado + aprobar + observar + rechazar |
| SyncController | /api/v1/sync | Download + upload + photos (protocolo offline) |
| DashboardController | /api/v1/dashboard | KPIs + por-operador + por-localidad + por-ruta |
| ReportesController | /api/v1/reportes | Excel export + por-operador + por-localidad |

### Paginas Blazor Web (13)

Login, Home (Dashboard), Operadores, Usuarios, TiposInspeccion, Flujos (constructor 2 paneles), Importaciones (wizard 4 pasos), Servicios, Asignaciones (+ masiva), Inspecciones, ControlCalidad (aprobar/observar/rechazar), Reportes, Error

### App MAUI Android

LoginPage, InspeccionesListPage, InspeccionPage (formulario dinamico), SincronizacionPage. Arquitectura MVVM con CommunityToolkit.Mvvm. FlowEngine evalua reglas condicionales offline. SyncService implementa protocolo download/upload/photos con cola persistente SyncQueueItem.

### Base de datos

- Docker container: `sgiform_postgres` (postgres:17, puerto 5434, user: sgiform, pass: SgiForm2024!)
- Schema: `sf` con 21+ tablas
- Datos demo cargados: 1 empresa, 4 roles, 40 permisos, 1 admin, 3 operadores, 5 tipos inspeccion, 1 flujo publicado (6 secciones, 20 preguntas, 10 reglas condicionales), 50 servicios importados, 50 asignaciones distribuidas en 3 operadores
- Enums originales PostgreSQL convertidos a VARCHAR para compatibilidad EF Core
- Columnas JSONB convertidas a TEXT
- Columnas INET convertidas a TEXT
- Se agrego `updated_at` a 9 tablas que no lo tenian
- Migracion EF Core registrada como `20240101000000_InitialCreate` (vacia, schema se maneja por scripts SQL)

### Credenciales

```
Web admin:  admin@sanitaria-demo.cl / Admin@2024!
Operador 1: OP001 / sanitaria-demo / Admin@2024!
Operador 2: OP002 / sanitaria-demo / Admin@2024!
Operador 3: OP003 / sanitaria-demo / Admin@2024!
```

### Tests

40 tests de integracion, 100% passing. Usan `WebApplicationFactory<Program>` con EF Core InMemory. El `TestFixture` usa entorno `"Testing"` para saltarse Serilog y health checks de PostgreSQL. Cada test class es `IClassFixture<TestFixture>`.

```bash
dotnet test tests/SgiForm.Tests/SgiForm.Tests.csproj
# Resultado: 40/40 passed, 0 failed
```

### Decisiones tecnicas importantes ya tomadas

1. **AuthStateService** reemplaza `IHttpContextAccessor` en Blazor Server (no funciona con render interactivo SignalR)
2. **SnakeCaseEnumConverter** convierte enums C# PascalCase a snake_case para PostgreSQL VARCHAR
3. **AppDbContext** tiene mapeo explicito de TODAS las columnas (654 lineas) porque el schema SQL usa snake_case
4. **Serilog se desactiva en Testing** para que WebApplicationFactory funcione
5. **Health check de PostgreSQL se desactiva en Testing**
6. **global.json fija SDK 8.0** porque MAUI workload requiere .NET 8
7. **Program.cs tiene `public partial class Program { }`** para WebApplicationFactory
8. **FKs de FlujoRegla** configuradas explicitamente para evitar ambiguedad en relaciones circulares
9. **Navegaciones inversas** (`ReglasOrigen`, `ReglasDestino`) ignoradas en FlujoPregunta por conflicto EF Core

### Deuda tecnica conocida

1. **Application/Contracts vacios** — logica de negocio directamente en controllers, DTOs como records en cada controller
2. **Class1.cs residuales** en Domain, Application, Infrastructure, Contracts
3. **Credenciales en appsettings.json** en texto plano
4. **Compresion de imagenes** en MAUI solo copia el stream (falta SkiaSharp)
5. **Middleware/ vacio** — sin rate limiting, sin auditoria automatica, sin tenant middleware
6. **Interfaces/ vacio** — sin contratos de repositorio
7. **ExcelGen (tools/) apunta a net10.0** — solo compila fuera del global.json
8. **Test `GetVersion_RetornaFlujoConSecciones`** acepta 500 como valido por limitacion InMemory
9. **Vistas PostgreSQL** (v_resumen_operador, v_avance_localidad, v_dashboard_empresa) fueron eliminadas para convertir enums y no se recrearon

### Como levantar el proyecto

```bash
# 1. Verificar Docker
docker ps --filter name=sgiform_postgres

# 2. Si no esta corriendo:
docker start sgiform_postgres

# 3. Iniciar API (puerto 5043)
cd src/SgiForm.Api && dotnet run

# 4. Iniciar Web (puerto 5054)
cd src/SgiForm.Web && dotnet run --launch-profile http

# 5. Compilar app movil
dotnet build src/SgiForm.Mobile/SgiForm.Mobile.csproj
```

### Documentacion existente

- `README.md` — instrucciones basicas de setup
- `DOCUMENTACION_TECNICA.md` — documentacion completa (1010 lineas) con diagramas Mermaid, arquitectura, flujos, deuda tecnica, glosario

---

## INSTRUCCIONES PARA EL CHAT NUEVO

1. **Lee el archivo `DOCUMENTACION_TECNICA.md`** del proyecto antes de hacer cambios — contiene toda la arquitectura, decisiones y deuda tecnica.
2. **No recrees archivos que ya existen** — edita los existentes.
3. **Mantene la convencion de nombres**: snake_case en PostgreSQL/JSON, PascalCase en C#, prefijo `sf-` en CSS.
4. **Los tests deben seguir pasando**: ejecuta `dotnet test` despues de cada cambio significativo.
5. **Detene los procesos antes de compilar** si la API/Web estan corriendo (bloquean los .dll).
6. **El schema SQL se maneja por scripts**, no por EF Core migrations.

---

## QUE SIGUE (pendiente por hacer)

Estas son las tareas pendientes en orden de prioridad. Consulta con el usuario cual quiere abordar:

### Prioridad Alta
- Mover credenciales a variables de entorno / User Secrets
- Implementar compresion real de imagenes con SkiaSharp en MAUI
- Recrear las 3 vistas PostgreSQL que se eliminaron
- Implementar middleware de auditoria (tabla `sf.auditoria` ya existe)
- Implementar rate limiting en endpoints publicos

### Prioridad Media
- Refactorizar: mover logica de controllers a capa Application
- Definir DTOs en SgiForm.Contracts (compartir entre API y Mobile)
- Implementar interfaces/repositorios en Domain
- Eliminar archivos Class1.cs residuales
- Agregar mas tests (importacion Excel end-to-end, sync protocol)

### Prioridad Baja
- Implementar exportacion PDF individual de inspeccion
- Implementar marca de agua en fotografias
- Implementar lectura de QR/barras (campos preparados, logica pendiente)
- Agregar dark mode a la web
- Dashboard con graficos (actualmente solo tablas y KPIs)

---

*Prompt generado el 19 de Marzo de 2026 a partir del estado real del repositorio.*

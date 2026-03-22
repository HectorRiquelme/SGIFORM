# PROMPT MAESTRO DE CONTINUIDAD — SGI-FORM

> Documento generado el 19 de Marzo de 2026.
> Objetivo: permitir que otro asistente continue el desarrollo sin perder contexto, sin rehacer analisis, y sin romper decisiones previas.

---

## 1. Identidad del proyecto

| Campo | Valor |
|---|---|
| **Nombre comercial** | SGI-FORM |
| **Nombre tecnico** | SgiForm (namespaces, solucion, paquetes) |
| **Tagline** | Gestion inteligente de inspecciones en terreno |
| **Tipo** | SaaS B2B multitenant |
| **Dominio** | Empresas sanitarias — inspeccion de medidores de agua |
| **Repositorio local** | `C:\Users\hecto\TRABAJO\dev_ia\kobotoolbox` |
| **Solucion** | `SgiForm.sln` |
| **SDK fijado** | .NET 8.0.319 (`global.json`) |

**Nota sobre el nombre**: El branding visible al usuario es **SGI-FORM** (login, sidebar, manifest Android, PageTitles). Los namespaces C# siguen siendo `SgiForm.*` — renombrarlos no es prioritario y requeriria refactoring masivo sin valor funcional.

---

## 2. Resumen ejecutivo

SGI-FORM es una plataforma que permite a empresas sanitarias ejecutar campanas masivas de inspeccion de medidores de agua en terreno. Gestiona el ciclo completo:

1. **Carga masiva** de servicios desde Excel
2. **Configuracion** de formularios dinamicos con logica condicional (motor de flujos)
3. **Asignacion** de trabajo a operadores de campo
4. **Ejecucion offline** en dispositivos Android con captura de fotos y GPS
5. **Sincronizacion** cuando hay conexion
6. **Control de calidad** (aprobar/observar/rechazar inspecciones)
7. **Reportes** y exportacion Excel

El sistema tiene 3 frontends: API REST, Web admin (Blazor Server), App movil (MAUI Android).

### Estado actual

- **API**: 12 controllers, 42+ endpoints, 100% funcional
- **Web**: 13 paginas Blazor, funcional con login, navegacion y dashboard
- **Movil**: 4 vistas, login funcional, formularios en desarrollo
- **Tests**: 43 tests de integracion, 100% passing
- **Base de datos**: Schema completo con datos demo

---

## 3. Stack tecnologico

| Componente | Tecnologia | Version | Notas |
|---|---|---|---|
| SDK | .NET | 8.0.319 | Fijado en `global.json` |
| API | ASP.NET Core Web API | 8.0 | Puerto 5043, escucha en 0.0.0.0 |
| ORM | Entity Framework Core | 8.0 | Mapeo explicito (654 lineas en AppDbContext) |
| BD principal | PostgreSQL | 17 | Docker container `sgiform_postgres`, puerto 5434 |
| Web admin | Blazor Server | 8.0 | Puerto 5054, render mode InteractiveServer |
| App movil | .NET MAUI Android | 8.0 | Offline-first con SQLite |
| BD movil | SQLite (sqlite-net-pcl) | 1.9.172 | 11 tablas locales |
| MVVM | CommunityToolkit.Mvvm | 8.3.2 | Source generators, [ObservableProperty], [RelayCommand] |
| Auth | JWT Bearer + BCrypt | — | HS256, 60 min web, 7 dias movil |
| Excel | ClosedXML | 0.102.1 | Import/export |
| Logging | Serilog | 8.0 | Desactivado en Testing |
| API docs | Swagger/OpenAPI | 6.5 | Solo en Development |
| Tests | xUnit + FluentAssertions | 2.5/6.12 | WebApplicationFactory + InMemory |
| PDF | Pandoc + XeLaTeX + mermaid-cli | — | Filtro Lua custom en `tools/mermaid-filter.lua` |

---

## 4. Arquitectura general

### Componentes y puertos

```
Navegador Web ──> Blazor Server (:5054) ──> API REST (:5043) ──> PostgreSQL (:5434)
                                                                    |
Emulador Android ──> App MAUI ──────────────────────> API REST (:5043)
   (10.0.2.2:5043)      |
                    SQLite local
```

### Dependencias entre proyectos

```
Domain (entidades, enums) ← sin dependencias externas
   ↑
Application (placeholder vacio) ← depende de Domain + Contracts
   ↑
Infrastructure (EF Core, AuthService, ExcelImport) ← depende de Domain + Application
   ↑
Api (controllers, Program.cs) ← depende de Infrastructure + Application + Contracts
   ↑
Tests (xUnit) ← depende de Api + Infrastructure + Domain

Web (Blazor) ← depende de Contracts (consume API via HTTP)
Mobile (MAUI) ← independiente (consume API via HTTP)
```

---

## 5. Estructura del proyecto

```
SgiForm.sln
|
|-- database/
|   |-- 01_schema.sql              # 999 lineas, 25 tablas, 3 vistas, schema sf.*
|   |-- 02_seed.sql                # 641 lineas, datos demo completos
|
|-- src/
|   |-- SgiForm.Domain/       # Entidades (21), Enums (9), Interfaces/ (vacio)
|   |-- SgiForm.Application/  # PLACEHOLDER — solo Class1.cs
|   |-- SgiForm.Infrastructure/
|   |   |-- Persistence/AppDbContext.cs        # 654 lineas mapeo explicito
|   |   |-- Persistence/SnakeCaseEnumConverter.cs
|   |   |-- Services/AuthService.cs            # JWT web + movil + refresh
|   |   |-- Services/ExcelImportService.cs     # Procesamiento Excel
|   |
|   |-- SgiForm.Api/
|   |   |-- Controllers/ (12)      # Auth, Operadores, Usuarios, TiposInspeccion,
|   |   |                          # Flujo, Importacion, Servicios, Asignacion,
|   |   |                          # Inspecciones, Sync, Dashboard, Reportes
|   |   |-- Program.cs             # Config completa (JWT, CORS, Swagger, Serilog)
|   |   |-- Properties/launchSettings.json  # http: 0.0.0.0:5043
|   |   |-- Middleware/            # VACIO
|   |
|   |-- SgiForm.Web/
|   |   |-- Components/App.razor   # @rendermode="InteractiveServer"
|   |   |-- Components/Layout/MainLayout.razor  # Sidebar SGI-FORM + RedirectToLogin
|   |   |-- Components/Layout/EmptyLayout.razor  # Para login (sin sidebar)
|   |   |-- Components/Shared/RedirectToLogin.razor  # Redirect seguro via OnAfterRender
|   |   |-- Components/Pages/ (13) # Login, Home, Operadores, Usuarios, TiposInspeccion,
|   |   |                          # Flujos, Importaciones, Servicios, Asignaciones,
|   |   |                          # Inspecciones, ControlCalidad, Reportes, Error
|   |   |-- Services/AuthStateService.cs  # Estado JWT scoped por circuito SignalR
|   |   |-- Services/ApiClient.cs         # HttpClient tipado con BaseAddress
|   |   |-- Program.cs                    # AddHttpClient<ApiClient> (SIN duplicado AddScoped)
|   |
|   |-- SgiForm.Mobile/
|   |   |-- MauiProgram.cs         # DI: HttpClientHolder, AuthService, SyncService, FlowEngine
|   |   |-- App.xaml               # 10 Value Converters registrados + 4 colores globales
|   |   |-- App.xaml.cs            # CreateWindow con Loaded event async (sin deadlock)
|   |   |-- AppShell.xaml          # Shell: login (raiz) + TabBar (inspecciones, sincronizacion)
|   |   |-- Converters/ValueConverters.cs  # 10 converters
|   |   |-- Database/AppDatabase.cs        # SemaphoreSlim + PRAGMAs opcionales
|   |   |-- Services/AuthService.cs        # HttpClientHolder + token cacheado + LogoutAsync
|   |   |-- Services/SyncService.cs        # Protocolo offline completo
|   |   |-- Services/FlowEngine.cs         # Motor de reglas condicionales
|   |   |-- Models/LocalModels.cs          # 12 clases SQLite
|   |   |-- ViewModels/LoginViewModel.cs
|   |   |-- ViewModels/InspeccionesListViewModel.cs
|   |   |-- ViewModels/InspeccionViewModel.cs
|   |   |-- ViewModels/SincronizacionViewModel.cs  # NUEVO: sync + logout
|   |   |-- Views/LoginPage.xaml           # SGI-FORM branding
|   |   |-- Views/InspeccionesListPage.xaml
|   |   |-- Views/InspeccionPage.xaml
|   |   |-- Views/SincronizacionPage.xaml  # NUEVO: sync status + cerrar sesion
|   |   |-- Platforms/Android/AndroidManifest.xml  # usesCleartextTraffic + label SGI-FORM
|
|-- shared/
|   |-- SgiForm.Contracts/    # PLACEHOLDER — solo Class1.cs
|
|-- tests/
|   |-- SgiForm.Tests/
|       |-- TestFixture.cs         # WebApplicationFactory + InMemory + seed
|       |-- ApiIntegrationTests.cs # 43 tests (10 clases)
|
|-- tools/
|   |-- mermaid-filter.lua         # Filtro Pandoc para generar diagramas PNG
|   |-- mermaid-images/            # Imagenes generadas (14 diagramas)
|   |-- ExcelGen/                  # Utilidad Excel (target net10.0, no compila con SDK 8)
|
|-- DOCUMENTACION_TECNICA.md       # 1010 lineas, arquitectura completa
|-- DOCUMENTACION_TECNICA.pdf      # 572 KB, con diagramas renderizados
|-- PROMPT_CONTINUACION.md         # Prompt anterior (version previa a este documento)
|-- PROMPT_MAESTRO_CONTINUIDAD.md  # ESTE DOCUMENTO
|-- README.md
|-- global.json                    # SDK 8.0.319
```

---

## 6. Logica funcional del sistema

### Motor de formularios dinamicos (pieza central)

- **Flujo** → tiene **Versiones** (inmutables una vez publicadas)
- **Version** → contiene **Secciones** (ordenadas)
- **Seccion** → contiene **Preguntas** (20 tipos de control)
- **Version** → tiene **Reglas** (logica condicional entre preguntas)
- **Regla**: Si `pregunta_origen` `operador` `valor`, entonces `accion` sobre `pregunta_destino`
- 14 operadores: eq, neq, gt, lt, gte, lte, contains, not_contains, in, not_in, is_empty, is_not_empty, starts_with, ends_with
- 10 acciones: mostrar, ocultar, obligatorio, opcional, saltar_seccion, bloquear_cierre, calcular, asignar_valor, min_fotos, max_fotos

### Protocolo de sincronizacion offline-first

1. **Login** (requiere conexion) → JWT de 7 dias para movil
2. **Descarga** → `GET /sync/download` → asignaciones + flujos + catalogos → SQLite
3. **Trabajo offline** → todo en SQLite, cola `SyncQueueItem` persistente
4. **Subida** → `POST /sync/upload` (inspecciones) + `POST /sync/photos` (fotos en batch)
5. **Resolucion de conflictos** → "Mobile wins" para datos de campo

### Importacion Excel

Upload .xlsx → Preview (10 filas) → Confirmar → Procesamiento con validacion por fila → Resultado con errores detallados.

### Control de calidad

Inspecciones completadas → Supervisor revisa → Aprobar / Observar (con motivo) / Rechazar (con motivo).

---

## 7. Tipos de usuarios y permisos

| Rol | Codigo | Acceso | Permisos |
|---|---|---|---|
| Administrador | `admin` | Web | Total (30 permisos) |
| Supervisor | `supervisor` | Web | Operadores, asignaciones, inspecciones, dashboard, reportes (17 permisos) |
| Auditor | `auditor` | Web | Solo lectura: inspecciones, dashboard, reportes (7 permisos) |
| Cliente Consulta | `cliente_consulta` | Web | Dashboard e inspecciones aprobadas (3 permisos) |
| Operador | `operador` | Movil | Ejecuta inspecciones. Login independiente con codigo+empresa+password |

### Multitenant

Cada request contiene claim `empresa_id`. Todos los controllers filtran por `EmpresaId`. Aislamiento logico por tenant.

### Credenciales de prueba (datos del seed)

**Web:**
- `admin@sanitaria-demo.cl` / `Admin@2024!` (Administrador)
- `hector@sanitaria-demo.cl` / `Hector@2024!` (Supervisor) — creado en esta sesion

**Movil:**
- `OP001` / `sanitaria-demo` / `Admin@2024!` (Carlos Munoz, La Serena)
- `OP002` / `sanitaria-demo` / `Admin@2024!` (Ana Rojas, Coquimbo)
- `OP003` / `sanitaria-demo` / `Admin@2024!` (Pedro Gonzalez, Ovalle)
- `OP100` / `sanitaria-demo` / `Campo@2024!` (Hector Campo, La Serena) — creado en esta sesion

---

## 8. Estado actual real del proyecto

### Que funciona

| Componente | Estado | Detalle |
|---|---|---|
| API REST | Funcional | 12 controllers, 42+ endpoints, Swagger |
| Auth web | Funcional | Login JWT, refresh token corregido |
| Auth movil | Funcional | Login JWT 7 dias, sin refresh token (por FK) |
| Web Login | Funcional | SGI-FORM branding, InteractiveServer |
| Web Dashboard | Funcional | KPIs en tarjetas |
| Web Operadores | Funcional | CRUD completo |
| Web Usuarios | Funcional | CRUD + roles |
| Web Tipos Inspeccion | Funcional | CRUD |
| Web Flujos | Funcional | Constructor visual 2 paneles |
| Web Importaciones | Funcional | Wizard 4 pasos (IHttpContextAccessor removido) |
| Web Servicios | Funcional | Listado con filtros |
| Web Asignaciones | Funcional | Individual + masiva |
| Web Inspecciones | Funcional | Listado |
| Web Control Calidad | Funcional | Aprobar/observar/rechazar |
| Web Reportes | Funcional | Excel export |
| Web Navegacion | Funcional | Sidebar con NavLinks + RedirectToLogin |
| Movil Login | Funcional | Con URL configurable, usesCleartextTraffic |
| Movil Lista inspecciones | Funcional | Vacia hasta que se sincronice |
| Movil Sincronizacion | Funcional | Status + sync + cerrar sesion |
| Movil Formulario | Parcial | Navega secciones pero solo muestra labels (sin controles de input) |
| Tests | 43/43 passing | Incluye 3 tests nuevos de refresh token |
| PostgreSQL | Funcional | Docker container, datos demo cargados |
| Emulador Android | Configurado | AVD SgiForm_API34 (Pixel 6, API 34) |

### Que NO funciona / esta incompleto

| Item | Estado | Detalle |
|---|---|---|
| InspeccionPage controles | Incompleto | Solo muestra Labels de preguntas, no hay Entry/Picker/Switch para responder |
| BoolToCommandConverter | Placeholder | Retorna null — boton "Siguiente/Cerrar" sin comando |
| Compresion de imagenes | Stub | `ComprimirImagenAsync` solo copia el stream |
| Application layer | Vacia | Solo Class1.cs, logica en controllers |
| Contracts layer | Vacia | Solo Class1.cs, DTOs inline en controllers |
| Middleware/ | Vacio | Sin rate limiting, auditoria, tenant middleware |
| Vistas PostgreSQL | Existen en schema | v_resumen_operador, v_avance_localidad, v_dashboard_empresa — no se usan desde EF Core |

---

## 9. Decisiones tecnicas ya tomadas

### Arquitecturales

1. **AuthStateService** reemplaza `IHttpContextAccessor` en Blazor Server — IHttpContextAccessor NO funciona con render interactivo SignalR
2. **InteractiveServer render mode** aplicado globalmente en `App.razor` — todas las paginas son interactivas
3. **RedirectToLogin** componente dedicado que navega en `OnAfterRender` — no usar `Nav.NavigateTo` durante el render
4. **AddHttpClient<ApiClient>** registra ApiClient como Scoped automaticamente — NO agregar `AddScoped<ApiClient>()` adicional
5. **HttpClientHolder** patron compartido en movil — AuthService y SyncService usan la misma instancia, actualizable via `ActualizarBaseUrl`
6. **Token cacheado en memoria** — `_cachedToken` evita deadlock de `SecureStorage.GetAsync().Result` en UI thread
7. **InitializeAsync** en App.Loaded event — carga token de SecureStorage asincrono despues de que Shell esta listo
8. **SemaphoreSlim + double-check** en `AppDatabase.GetConnectionAsync` — evita race condition en inicializacion SQLite

### De datos

9. **Enums nativos PostgreSQL convertidos a VARCHAR** — compatibilidad con EF Core via `SnakeCaseEnumConverter`
10. **Schema SQL maneja el DDL** — EF Core migrations vacias, no usar `dotnet ef` para cambios de schema
11. **FKs de FlujoRegla** configuradas explicitamente — evita ambiguedad en relaciones circulares PreguntaOrigen/PreguntaDestino
12. **Navegaciones inversas** (`ReglasOrigen`, `ReglasDestino`) ignoradas en FlujoPregunta

### De autenticacion

13. **Login movil genera JWT de 7 dias SIN refresh token** — tabla refresh_token tiene FK a usuario, no a operador
14. **Login web genera JWT de 60 min + refresh token de 30 dias** — con rotacion (revocar anterior al refrescar)
15. **RefreshTokenAsync** valida usuario activo/no bloqueado antes de emitir nuevo token

### De sincronizacion

16. **PRAGMAs SQLite opcionales** — envueltos en try-catch porque `journal_mode=WAL` falla con ciertos flags en sqlite-net-pcl
17. **SharedCache** flag en SQLiteAsyncConnection — `FullMutex` causaba `SQLiteException: not an error`
18. **UpsertAsignacionesAsync** usa `RunInTransactionAsync` — atomicidad en batch
19. **GroupBy + Last** en FlowEngine respuestas — evita crash por `PreguntaId` duplicados en `ToDictionary`
20. **DateTimeOffset.TryParse** en todos los parsers — evita FormatException en datos corruptos

### De UI

21. **Branding SGI-FORM** — nombre comercial en todos los puntos visibles, namespaces siguen como SgiForm
22. **CSS custom con prefijo `sf-`** — sin frameworks CSS, ~500 lineas en `app.css`
23. **10 Value Converters** registrados en App.xaml — usados por todas las vistas XAML
24. **usesCleartextTraffic=true** en AndroidManifest — necesario para desarrollo con HTTP

### De infraestructura

25. **API escucha en 0.0.0.0:5043** (profile http) — permite conexion desde emulador Android via 10.0.2.2
26. **Serilog se desactiva en entorno Testing** — WebApplicationFactory incompatible
27. **Health check PostgreSQL se desactiva en Testing**
28. **global.json fija SDK 8.0** — MAUI workload requiere .NET 8
29. **Program.cs tiene `public partial class Program { }`** — para WebApplicationFactory

---

## 10. Restricciones y reglas a respetar

1. **No usar `IHttpContextAccessor`** en componentes Blazor interactivos — causa congelamiento
2. **No llamar `Nav.NavigateTo` durante el render** de un componente — usar `OnAfterRender` o eventos
3. **No agregar `AddScoped<ApiClient>()`** en Program.cs de Web — `AddHttpClient<ApiClient>` ya lo registra como Scoped
4. **No usar `SecureStorage.GetAsync().Result`** ni `.Wait()` en UI thread — causa deadlock en MAUI
5. **No cambiar a `FullMutex`** en SQLiteOpenFlags — causa `SQLiteException` con PRAGMAs
6. **No usar `ToDictionary` directo** sobre respuestas — puede haber duplicados, usar GroupBy
7. **Siempre detener API/Web antes de compilar** — los procesos bloquean los .dll
8. **Los tests deben seguir pasando**: ejecutar `dotnet test` despues de cada cambio
9. **Schema SQL se maneja por scripts** (`database/01_schema.sql`), no por EF Core migrations
10. **Convencion de nombres**: snake_case en PostgreSQL/JSON, PascalCase en C#, prefijo `sf-` en CSS
11. **No renombrar namespaces** de SgiForm a SGI-FORM — refactoring masivo sin valor funcional

---

## 11. Pendientes priorizados

### Prioridad Critica (bloquean uso en produccion)

| # | Pendiente | Detalle |
|---|---|---|
| 1 | **Controles de input en InspeccionPage** | Solo muestra Labels. Necesita Entry, Picker, Switch, DatePicker, CheckBox, camera segun `TipoControl` de cada pregunta |
| 2 | **Boton Siguiente/Cerrar sin comando** | `BoolToCommandConverter` retorna null. Crear `SiguienteOCerrarCommand` en InspeccionViewModel |
| 3 | **Mover credenciales a variables de entorno** | JWT Key y connection string en texto plano en appsettings.json |

### Prioridad Alta

| # | Pendiente | Detalle |
|---|---|---|
| 4 | Compresion real de imagenes | `ComprimirImagenAsync` solo copia stream. Usar SkiaSharp para resize a 1920px, 75% calidad |
| 5 | Rate limiting en endpoints publicos | Login web, login movil, sync — sin proteccion actualmente |
| 6 | Middleware de auditoria | Tabla `sf.auditoria` existe pero no se usa |
| 7 | Thread safety en DefaultRequestHeaders | SyncService muta `DefaultRequestHeaders` en shared HttpClient — usar `HttpRequestMessage` por request |
| 8 | Mas tests | Excel import end-to-end, sync protocol, FlowEngine reglas |

### Prioridad Media

| # | Pendiente | Detalle |
|---|---|---|
| 9 | Refactorizar controllers a Application layer | Logica de negocio directamente en controllers |
| 10 | Definir DTOs en Contracts | Compartir entre API y Mobile |
| 11 | Eliminar Class1.cs residuales | Domain, Application, Infrastructure, Contracts |
| 12 | GuardarFlujoAsync sin transaccion | Inserts secuenciales sin atomicidad en SyncService |
| 13 | Iconos de TabBar faltantes | `list.png` y `sync.png` no existen — warnings Glide |
| 14 | ExcelGen target net10.0 | No compila con SDK 8 fijado en global.json |

### Prioridad Baja

| # | Pendiente | Detalle |
|---|---|---|
| 15 | Exportacion PDF individual de inspeccion | |
| 16 | Marca de agua en fotografias | |
| 17 | Lectura QR/codigo de barras | Campos preparados, logica pendiente |
| 18 | Dark mode web | |
| 19 | Dashboard con graficos | Actualmente solo tablas y KPIs |
| 20 | Quitar `usesCleartextTraffic` para produccion | O hacerlo condicional por build config |

---

## 12. Riesgos y puntos delicados

| Riesgo | Severidad | Detalle |
|---|---|---|
| **Credenciales en repositorio** | ALTA | JWT Key, DB password en appsettings.json |
| **Sin rate limiting** | ALTA | Endpoints de login expuestos a fuerza bruta |
| **InspeccionPage sin controles** | ALTA | Usuarios no pueden responder preguntas en campo |
| **DefaultRequestHeaders no thread-safe** | MEDIA | Race condition si sync corre en background mientras UI hace requests |
| **Refresh token FK a usuario** | MEDIA | Operadores moviles no pueden usar refresh tokens — JWT de 7 dias como workaround |
| **Test acepta HTTP 500** | BAJA | `GetVersion_RetornaFlujoConSecciones` acepta 500 por limitacion InMemory |
| **Archivos basura en raiz** | BAJA | `B`, `nul`, `toolsmermaid-images/` — artefactos de comandos mal formados |
| **Version hardcodeada** | BAJA | "v1.0.0" en footers de login web y movil — deberia ser dinamico |

---

## 13. Glosario tecnico y de dominio

| Termino | Significado |
|---|---|
| **Servicio** | Punto fisico a inspeccionar (medidor de agua en una direccion). Importado desde Excel. |
| **Flujo** | Conjunto de secciones, preguntas y reglas que definen un formulario de inspeccion. |
| **Version de flujo** | Snapshot inmutable de un flujo. Inspecciones se vinculan a una version especifica. |
| **Seccion** | Grupo logico de preguntas dentro de un flujo. |
| **Regla** | Condicion logica que altera comportamiento de otra pregunta (si X=No, mostrar Y). |
| **Asignacion** | Vinculo servicio + operador + flujo. Es la "orden de trabajo". |
| **Inspeccion** | Ejecucion real de una asignacion. Contiene respuestas, fotos, GPS, timestamps. |
| **Operador** | Persona que ejecuta inspecciones en terreno con la app Android. |
| **Tenant** | Empresa sanitaria. Aislamiento logico por `empresa_id`. |
| **SyncQueue** | Cola persistente en SQLite con operaciones pendientes de sincronizar. |
| **FlowEngine** | Motor que evalua reglas condicionales en tiempo real. |
| **Soft delete** | Borrado logico via `deleted_at` en vez de DELETE. |
| **Snake case** | Convencion `nombre_campo`. Usada en PostgreSQL, API JSON. |
| **HttpClientHolder** | Wrapper del HttpClient compartido entre AuthService y SyncService movil. |
| **RedirectToLogin** | Componente Blazor que navega a /login en OnAfterRender (evita crash durante render). |

---

## 14. Instructivo para entender el codigo

### Para entender el dominio

1. Empezar por `Domain/Enums/DomainEnums.cs` — define todos los estados y tipos
2. Leer `Domain/Entities/Flujo.cs` — estructura del motor de formularios (7 clases)
3. Leer `database/02_seed.sql` — ejemplo real de flujo con 6 secciones, 17 preguntas, 10 reglas

### Para entender la API

1. `Api/Program.cs` — configuracion completa del pipeline
2. `Infrastructure/Services/AuthService.cs` — JWT web + movil + refresh (250 lineas)
3. `Infrastructure/Persistence/AppDbContext.cs` — mapeo EF Core completo (654 lineas)
4. Cualquier controller — los DTOs estan como records al final de cada archivo

### Para entender la Web

1. `Web/Program.cs` — notar `AddHttpClient<ApiClient>` sin duplicado AddScoped
2. `Web/Components/App.razor` — `@rendermode="InteractiveServer"` global
3. `Web/Components/Layout/MainLayout.razor` — sidebar + RedirectToLogin + logout
4. `Web/Services/AuthStateService.cs` — estado JWT en memoria, scoped por circuito

### Para entender la App Movil

1. `Mobile/MauiProgram.cs` — DI con HttpClientHolder, lifecycle de servicios
2. `Mobile/App.xaml.cs` — CreateWindow con Loaded event async
3. `Mobile/Services/AuthService.cs` — token cacheado, HttpClientHolder, sin deadlock
4. `Mobile/Services/FlowEngine.cs` — motor de reglas, GroupBy en respuestas
5. `Mobile/Database/AppDatabase.cs` — SemaphoreSlim, transacciones, PRAGMAs opcionales

### Como levantar el proyecto

```bash
# 1. Docker PostgreSQL
docker start sgiform_postgres

# 2. API (puerto 5043, todas las interfaces)
cd src/SgiForm.Api && dotnet run --launch-profile http

# 3. Web (puerto 5054)
cd src/SgiForm.Web && dotnet run --launch-profile http

# 4. App movil (requiere emulador corriendo)
set ANDROID_HOME=C:\Program Files (x86)\Android\android-sdk
dotnet build src/SgiForm.Mobile/SgiForm.Mobile.csproj -f net8.0-android -c Debug -t:Run

# 5. Tests
dotnet test tests/SgiForm.Tests/SgiForm.Tests.csproj
```

### Emulador Android

- AVD: `SgiForm_API34` (Pixel 6, API 34, Google APIs x86_64)
- Iniciar: `"C:\Program Files (x86)\Android\android-sdk\emulator\emulator.exe" -avd SgiForm_API34`
- URL API desde emulador: `http://10.0.2.2:5043`

---

## 15. Forma correcta de continuar en un nuevo chat

### Antes de hacer cualquier cambio

1. **Lee este documento completo** antes de tocar codigo
2. **No recrees archivos existentes** — edita los que ya existen
3. **Ejecuta los tests** antes y despues de cada cambio significativo:
   ```bash
   dotnet test tests/SgiForm.Tests/SgiForm.Tests.csproj
   ```
4. **Detiene API/Web** antes de compilar (bloquean .dll)
5. **Respeta las 11 restricciones** de la seccion 10

### Orden recomendado de trabajo

1. Primero: corregir pendientes criticos (controles de input, comando siguiente/cerrar)
2. Segundo: seguridad (credenciales, rate limiting)
3. Tercero: robustez (thread safety, mas tests)
4. Cuarto: refactoring (Application layer, Contracts)
5. Ultimo: features nuevas

### Como verificar que todo sigue funcionando

```bash
# Tests backend
dotnet test tests/SgiForm.Tests/SgiForm.Tests.csproj
# Resultado esperado: 43/43 passed

# Compilar movil
dotnet build src/SgiForm.Mobile/SgiForm.Mobile.csproj -f net8.0-android -c Debug
# Resultado esperado: 0 Errores

# Login web (API debe estar corriendo)
curl -s http://localhost:5043/api/v1/auth/login -X POST -H "Content-Type: application/json" \
  -d '{"email":"admin@sanitaria-demo.cl","password":"Admin@2024!"}'
# Resultado esperado: JSON con access_token

# Login movil
curl -s http://localhost:5043/api/v1/auth/login-movil -X POST -H "Content-Type: application/json" \
  -d '{"codigo_operador":"OP001","empresa_slug":"sanitaria-demo","password":"Admin@2024!"}'
# Resultado esperado: JSON con access_token
```

---

## 16. Prompt final listo para reutilizar

> **Copia y pega el bloque completo de abajo en un nuevo chat.**

---

```
Actua como un arquitecto de software senior experto en .NET 8, ASP.NET Core, Blazor Server, .NET MAUI Android, PostgreSQL, Entity Framework Core, sistemas offline-first y motores de formularios dinamicos.

Estoy trabajando en un proyecto llamado SGI-FORM (nombre tecnico: SgiForm). Es un sistema de inspecciones en terreno para empresas sanitarias. El proyecto esta construido y parcialmente funcional.

UBICACION: C:\Users\hecto\TRABAJO\dev_ia\kobotoolbox

Lee el archivo PROMPT_MAESTRO_CONTINUIDAD.md del proyecto — contiene toda la arquitectura, decisiones tomadas, restricciones, bugs corregidos y pendientes priorizados.

REGLAS CRITICAS:
- No uses IHttpContextAccessor en Blazor — causa congelamiento
- No llames Nav.NavigateTo durante el render — usar OnAfterRender
- No agregues AddScoped<ApiClient>() en Web/Program.cs — ya esta registrado via AddHttpClient
- No uses SecureStorage.GetAsync().Result en MAUI — causa deadlock
- No uses FullMutex en SQLiteOpenFlags — causa SQLiteException
- Schema SQL se maneja por scripts, NO por EF Core migrations
- Los 43 tests deben seguir pasando despues de cada cambio

ESTADO ACTUAL:
- API: 12 controllers, 43 tests passing, funcional
- Web: 13 paginas Blazor con InteractiveServer, login funcional, navegacion funcional
- Movil: Login funcional, lista vacia (sin asignaciones), formulario de inspeccion incompleto (solo labels, sin controles de input)
- Branding: SGI-FORM en toda la UI, namespaces siguen como SgiForm

PENDIENTE MAS URGENTE:
1. Implementar controles de input en InspeccionPage.xaml (Entry, Picker, Switch, DatePicker segun TipoControl)
2. Crear SiguienteOCerrarCommand en InspeccionViewModel (BoolToCommandConverter es placeholder)
3. Mover credenciales a variables de entorno

Consulta conmigo que tarea quieres que aborde.
```

---

*Documento generado a partir del analisis exhaustivo del repositorio y la sesion de desarrollo del 19 de Marzo de 2026.*

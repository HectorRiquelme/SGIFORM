# NEXT_STEPS.md — Trabajo pendiente

> Actualizar este archivo al inicio y al final de cada sesión de trabajo.
> Ordenado por prioridad real de negocio / riesgo técnico.

---

## Estado del sistema — 2026-03-24 (última actualización)

| Check | Estado |
|-------|--------|
| Build `dotnet build SgiForm.sln -c Release` | OK 0 errores, 0 advertencias |
| Tests `dotnet test tests/SgiForm.Tests/` | OK 46/46 passed |
| Naming SgiForm en todo el repo | OK 0 ocurrencias de SanitasField |
| GitHub Actions workflows | OK en código — pendiente configurar secrets y runner |
| Documentación de despliegue | OK Scripts y manual completos |
| Archivos de contexto persistente | OK CLAUDE.md, AGENTS.md, PROJECT_CONTEXT.md, NEXT_STEPS.md |
| README.md en raíz | OK Reescrito con arquitectura, build, IIS, PostgreSQL, troubleshooting |
| deploy/rollback.ps1 | OK Script simplificado con modos restore y remove |
| Fix ip_origen schema (tipo inet → text) | OK Documentado en README Troubleshooting y scripts SQL |

---

## Lo que se hizo en esta sesión (2026-03-23)

### 1. Despliegue completo en servidor Windows Server 2019

Se ejecutó el primer despliegue real del sistema en el servidor de producción. Problemas encontrados y resueltos:

- **HTTP 400 Bad Request (Invalid Hostname)**: `AllowedHosts` en `appsettings.Production.json` no incluía el hostname del servidor. Fix: cambiar a `"AllowedHosts": "*"`.
- **HTTP 500 - columna ip_origen es de tipo inet**: El schema SQL definía `ip_origen` como `INET` pero EF Core envía `text`. Fix SQL aplicado:
  ```sql
  ALTER TABLE sf.refresh_token ALTER COLUMN ip_origen TYPE text USING ip_origen::text;
  ALTER TABLE sf.sincronizacion_log ALTER COLUMN ip_origen TYPE text USING ip_origen::text;
  ```
- **AppPool names**: Los nombres usados en producción fueron `SgiFormApi` y `SgiFormWeb` (sin guión), en puertos 5001 y 8080.
- Variables de entorno configuradas via `appcmd.exe` en los AppPools de IIS.
- Scripts SQL `01_schema.sql`, `02_seed.sql`, `03_operador_refresh_token.sql` ejecutados correctamente.

### 2. README.md reescrito

- **Archivo**: `README.md` en raíz del repo.
- **Qué cambió**: Reescrito completamente con arquitectura actualizada (puertos 5001/8080, Windows Server 2019), comandos de deploy IIS con `appcmd.exe`, sección de troubleshooting con los problemas reales encontrados en producción (ip_origen, AllowedHosts, binding de puertos), credenciales del seed.

### 3. deploy/rollback.ps1 reescrito

- **Archivo**: `deploy/rollback.ps1`
- **Qué cambió**: Reescrito con interfaz simplificada. Dos modos: `restore` (lista backups disponibles, permite seleccionar) y `remove` (elimina sitios y AppPools completamente). Validación post-rollback integrada.

---

## Pendientes — ALTA (bloquean uso real)

### 1. Configurar secrets en GitHub para los workflows

**Problema**: `deploy-iis.yml` requiere 4 secrets de GitHub que no están configurados. Sin ellos, el workflow falla al intentar desplegar.

**Secrets requeridos** (configurar en `https://github.com/HectorRiquelme/SGIFORM/settings/secrets/actions`):

| Secret | Valor esperado | Ejemplo |
|--------|---------------|---------|
| `IIS_WEB_PATH` | Ruta física del sitio Web en el servidor | `C:\SgiForm\publish\web` |
| `IIS_API_PATH` | Ruta física del sitio API en el servidor | `C:\SgiForm\publish\api` |
| `IIS_WEB_APPPOOL` | Nombre del AppPool Web | `SgiFormWeb` |
| `IIS_API_APPPOOL` | Nombre del AppPool API | `SgiFormApi` |

**Cómo configurar**:
1. Ir a `Settings → Secrets and variables → Actions → New repository secret`
2. Agregar los 4 secrets con los valores del servidor de producción

---

### 2. Registrar un self-hosted runner en el servidor de producción

**Problema**: Ambos workflows usan runners `self-hosted`. Sin un runner registrado en el servidor, los workflows quedan en cola indefinidamente.

- `deploy-iis.yml` requiere labels: `[self-hosted, windows, iis]`
- `publish-apk.yml` requiere labels: `[self-hosted, windows, android]`

**Cómo registrar el runner** (ejecutar en el servidor de producción):
```powershell
mkdir C:\actions-runner; cd C:\actions-runner
# Ir a: Settings → Actions → Runners → New self-hosted runner
# Seguir instrucciones de GitHub (generan token único)
.\config.cmd --url https://github.com/HectorRiquelme/SGIFORM --token TOKEN_GENERADO_POR_GITHUB --labels "self-hosted,windows,iis,android"
.\svc.cmd install
.\svc.cmd start
```

---

### 3. Configurar CORS para la URL real de producción

**Archivo**: `src/SgiForm.Api/appsettings.Production.json` o variable de entorno del AppPool.

**Problema**: `appsettings.json` tiene origenes de desarrollo. En producción la app móvil y el web necesitan la URL real.

**Solución** (en el servidor, después del deploy):
```powershell
$appcmd = "$env:windir\system32\inetsrv\appcmd.exe"
& $appcmd set apppool "SgiFormApi" /+"environmentVariables.[name='Cors__AllowedOrigins__0',value='https://tudominio.cl']"
& $appcmd set apppool "SgiFormApi" /+"environmentVariables.[name='Cors__AllowedOrigins__1',value='http://IP_DEL_SERVIDOR']"
iisreset /restart
```

---

## Pendientes — MEDIA (mejoran calidad)

### 4. Aplicar fix ip_origen al schema SQL base

**Archivo**: `database/01_schema.sql`

**Problema**: El schema define `ip_origen` como tipo `INET` en `sf.refresh_token` y `sf.sincronizacion_log`. EF Core envía estos campos como `text`, causando error 500 en producción.

**Fix permanente** (editar el schema para que nuevas instalaciones no tengan el problema):
```sql
-- En 01_schema.sql, cambiar:
-- ip_origen INET
-- Por:
-- ip_origen TEXT
```

Este fix se aplicó manualmente en el servidor de producción via ALTER TABLE, pero el archivo fuente no ha sido actualizado aún.

---

### 5. Actualizar CHANGELOG.md

**Archivo**: `CHANGELOG.md` en raíz del repo.

**Qué agregar**:
```markdown
## [1.1.0] - 2026-03-23

### Added
- README.md reescrito con arquitectura IIS real (puertos 5001/8080)
- deploy/rollback.ps1 con modos restore y remove
- Troubleshooting documentado para problemas reales de producción

### Fixed
- ip_origen: ALTER TABLE para cambiar tipo INET → TEXT en refresh_token y sincronizacion_log
- AllowedHosts: configurar * en producción para evitar 400 Bad Request

### Changed
- AppPool names: SgiFormApi y SgiFormWeb (sin guión, coherente con scripts)
- Puertos IIS: API en 5001, Web en 8080
```

---

### 6. Ampliar cobertura de tests de integración

**Estado actual**: 46 tests. Módulos sin tests:

| Módulo | Controller | Cobertura actual |
|--------|-----------|-----------------|
| Flujos | `FlujoController` | 0% |
| Importación | `ImportacionController` | 0% |
| Asignaciones | `AsignacionController` | 0% |
| Inspecciones | `InspeccionesController` | 0% |
| Dashboard | `DashboardController` | 0% |
| Reportes | `ReportesController` | 0% |

**Objetivo**: 46 → ~90 tests.

---

### 7. Refresh token automático en Blazor Web

**Archivos**: `src/SgiForm.Web/Services/ApiClient.cs` + `src/SgiForm.Web/Services/AuthStateService.cs`

**Problema**: El JWT de Blazor expira a los 60 minutos. El usuario ve errores 401 sin aviso.

**Qué implementar**: Interceptar HTTP 401, llamar `POST /api/v1/auth/refresh` automáticamente, reintentar el request original.

---

## Pendientes — BAJA (mejoras futuras)

| # | Tarea | Archivo(s) afectados |
|---|-------|---------------------|
| 8 | Caché IMemoryCache para KPIs (TTL 5 min) | `DashboardController.cs` |
| 9 | Paginación cursor-based para tablas grandes | `ServiciosController.cs`, `InspeccionesController.cs` |
| 10 | Mapa Leaflet.js en panel web para coordenadas GPS | `Inspecciones.razor`, nuevo componente |
| 11 | Notificaciones push FCM a operadores móviles | `SgiForm.Mobile` + nuevo servicio API |
| 12 | MFA/TOTP para usuarios admin | `AuthController.cs`, `AuthService.cs` |

---

## Decisiones técnicas pendientes

| Decisión | Opciones | Recomendación |
|----------|---------|---------------|
| ¿HTTPS en producción? | SSL binding IIS / Cloudflare / nginx | Cloudflare o nginx delante de IIS es lo más simple |
| ¿EF Core Migrations vs scripts SQL? | Mantener scripts | Mantener scripts SQL — dan más control y son auditables |
| ¿Versionar API v2? | Solo cuando haya breaking changes | Mantener v1 |

---

## Registro de sesiones

| Fecha | Commits | Trabajo realizado |
|-------|---------|-------------------|
| 2026-03-19 | `dde9a9e` | Creación inicial del proyecto completo |
| 2026-03-22 | `b4b48cb` | QA pre-producción: fix bugs, nullable warnings, package versions |
| 2026-03-22 | `5692c10` | Rate limiting nativo ASP.NET Core 8 (3 políticas) |
| 2026-03-22 | `8995ae6` | Producción: refresh token revocable móvil, FlowValidator, health check seguro |
| 2026-03-22 | `c8fbde0` | DevOps: DEPLOY_MANUAL.md, deploy-server.ps1, validate-deployment.ps1, rollback.ps1, README.md |
| 2026-03-22 | `ed7d51a` | Renombramiento SanitasField → SgiForm (180 archivos, build OK, 46/46 tests) |
| 2026-03-22 | `05dc347` | Contexto persistente: CLAUDE.md, AGENTS.md, PROJECT_CONTEXT.md, NEXT_STEPS.md |
| 2026-03-22 | `e68a79f` | NEXT_STEPS.md actualizado con detalle completo de sesión y pendientes exactos |
| 2026-03-23 | `fe0b21a` | Deploy producción completo, fix ip_origen, README y rollback.ps1 reescritos |
| 2026-03-24 | *(pendiente commit)* | Deploy real ejecutado en servidor, validado login web+móvil, fix ip_origen aplicado en BD, schema SQL corregido |

---

## Lo que se hizo en sesión 2026-03-24

### Deploy real en servidor Windows Server 2019

Se ejecutó el despliegue completo interactivo, paso a paso. Hallazgos relevantes para futuras instalaciones:

- **PostgreSQL ya existía** como servicio `postgresql-x64-16` (instalación previa). El instalador que bajamos no fue necesario. La contraseña del superusuario `postgres` era `postgres`.
- **Puertos ocupados**: Puerto 5000 y 5001 estaban en uso por http.sys. La API quedó en **puerto 5001**.
- **`AllowedHosts`** en `appsettings.Production.json` estaba configurado como `"DOMINIO_PRODUCCION.cl"` — bloqueaba todos los requests con HTTP 400. Fix: cambiar a `"*"`.
- **`ip_origen INET`**: EF Core envía el campo como `text`, PostgreSQL rechaza con error 42804. Fix aplicado en BD y en `database/01_schema.sql`.
- **Hash BCrypt del seed**: El hash del `02_seed.sql` era un placeholder que no correspondía a ninguna contraseña conocida. Se regeneró con `BCrypt.Net.BCrypt.HashPassword("Admin@2024!")` usando una DLL del publish.
- **Login móvil**: Requiere campo `empresa_slug` en el body (valor: `sanitaria-demo`).

### Estado final del servidor

| Check | Estado |
|-------|--------|
| API login web | ✅ `admin@sanitaria-demo.cl / Admin@2024!` |
| API login móvil | ✅ `OP001 / Admin@2024! / sanitaria-demo` |
| Web Blazor HTTP | ✅ 200 en `http://[IP]:8080` |
| PostgreSQL 25 tablas | ✅ schema `sf` completo |
| AppPools IIS | ✅ SgiFormApi:5001 + SgiFormWeb:8080 |

---

## Siguiente paso exacto recomendado

> **Commitear y hacer push del fix de `database/01_schema.sql`** (`ip_origen INET → TEXT`)

El archivo ya tiene el cambio aplicado localmente pero NO está commiteado. Es el único cambio pendiente de código.

```powershell
git add database/01_schema.sql AGENTS.md CLAUDE.md PROJECT_CONTEXT.md NEXT_STEPS.md
git commit -m "fix(schema): cambiar ip_origen de INET a TEXT en refresh_token y sincronizacion_log"
git push
```

El siguiente paso después de eso es:

> **Configurar secrets en GitHub + registrar el runner self-hosted** (ver pendiente #1 y #2 arriba)

# NEXT_STEPS.md — Trabajo pendiente

> Actualizar este archivo al inicio y al final de cada sesión de trabajo.
> Ordenado por prioridad real de negocio / riesgo técnico.

---

## Estado del sistema — 2026-03-22 (última actualización)

| Check | Estado |
|-------|--------|
| Build `dotnet build SgiForm.sln -c Release` | ✅ 0 errores, 0 advertencias |
| Tests `dotnet test tests/SgiForm.Tests/` | ✅ 46/46 passed |
| Naming SgiForm en todo el repo | ✅ 0 ocurrencias de "SanitasField" |
| GitHub Actions workflows | ✅ Código correcto — ⚠️ falta configurar secrets y runner |
| Documentación de despliegue | ✅ Scripts y manual completos |
| Archivos de contexto persistente | ✅ CLAUDE.md, AGENTS.md, PROJECT_CONTEXT.md, NEXT_STEPS.md |

---

## Lo que se hizo en esta sesión (2026-03-22)

### ✅ 1. Renombramiento masivo SanitasField → SgiForm
- **Qué**: 180 archivos modificados/renombrados. Zero ocurrencias de "SanitasField" en el código.
- **Archivos afectados**:
  - 8 directorios: `src/SanitasField.*` → `src/SgiForm.*`, `tests/SanitasField.Tests` → `tests/SgiForm.Tests`
  - `SanitasField.sln` → `SgiForm.sln` / `SgiForm.slnx`
  - Todos los `.cs`, `.csproj`, `.razor`, `.json`, `.sql`, `.ps1`, `.md`, `.yml`
  - Connection strings: `sanitasfield` → `sgiform`
  - AppPool IIS: `SanitasField-*` → `SgiForm-*`
  - Rutas servidor: `C:\SanitasField\` → `C:\SgiForm\`
- **Resultado**: Build 0 errores, 46/46 tests tras el rename.
- **Commit**: `ed7d51a`

### ✅ 2. Documentación DevOps completa (previa al rename, ya actualizada)
- **Qué**: Documentación oficial de despliegue en Windows Server + IIS + PostgreSQL nativo.
- **Archivos creados**:
  - `deploy/DEPLOY_MANUAL.md` — 13 secciones, comandos exactos paso a paso
  - `deploy/deploy-server.ps1` — automatización de 11 pasos (backup, build, IIS, vars, SQL, validación)
  - `deploy/validate-deployment.ps1` — 26 checks PASS/WARN/FAIL
  - `deploy/rollback.ps1` — rollback interactivo o a timestamp específico
  - `README.md` — reescrito con arquitectura, build, deploy, variables, validación, troubleshooting, rollback
  - `src/SgiForm.Api/appsettings.Production.json` — template de producción sin secretos reales
- **Commit**: `c8fbde0`

### ✅ 3. Sistema de contexto persistente
- **Qué**: 4 archivos creados para mantener contexto entre sesiones de trabajo con Claude Code.
- **Archivos creados**:
  - `CLAUDE.md` — stack, convenciones, entidades, endpoints, rate limiting, tests, comandos
  - `AGENTS.md` — reglas para agentes de IA, flujo de trabajo, archivos críticos, qué NO hacer
  - `PROJECT_CONTEXT.md` — historial de 6 fases, estado por módulo, deuda técnica, entorno dev vs prod
  - `NEXT_STEPS.md` — este archivo
- **Commit**: `05dc347`

### ✅ 4. Verificación de GitHub Actions post-renombramiento
- **Resultado**: Los workflows YA están correctos. Referencian `SgiForm.sln`, `SgiForm.Api.csproj`, `SgiForm.Web.csproj`, `SgiForm.Tests` correctamente.
- **Pero**: Tienen dependencias no configuradas (ver pendientes #1 y #2 abajo).
- **Archivos revisados**:
  - `.github/workflows/deploy-iis.yml` — OK en código
  - `.github/workflows/publish-apk.yml` — OK en código

---

## Pendientes — ALTA (bloquean uso real)

### 1. Configurar secrets en GitHub para los workflows

**Problema**: `deploy-iis.yml` requiere 4 secrets de GitHub que no están configurados. Sin ellos, el workflow falla al intentar desplegar.

**Secrets requeridos** (configurar en `https://github.com/HectorRiquelme/SGIFORM/settings/secrets/actions`):

| Secret | Valor esperado | Ejemplo |
|--------|---------------|---------|
| `IIS_WEB_PATH` | Ruta física del sitio Web en el servidor | `C:\SgiForm\web` |
| `IIS_API_PATH` | Ruta física del sitio API en el servidor | `C:\SgiForm\api` |
| `IIS_WEB_APPPOOL` | Nombre del AppPool Web | `SgiForm-Web` |
| `IIS_API_APPPOOL` | Nombre del AppPool API | `SgiForm-API` |

**Cómo configurar**:
1. Ir a `Settings → Secrets and variables → Actions → New repository secret`
2. Agregar los 4 secrets con los valores del servidor de producción

---

### 2. Registrar un self-hosted runner en el servidor de producción

**Problema**: Ambos workflows usan runners `self-hosted` (no `ubuntu-latest`). Sin un runner registrado en el servidor, los workflows quedan en cola indefinidamente.

- `deploy-iis.yml` requiere labels: `[self-hosted, windows, iis]`
- `publish-apk.yml` requiere labels: `[self-hosted, windows, android]`

**Cómo registrar el runner** (ejecutar en el servidor de producción):
```powershell
# 1. Descargar runner desde GitHub
# Ir a: Settings → Actions → Runners → New self-hosted runner
# Seguir las instrucciones de GitHub (generan un token único)

# 2. Ejecutar en el servidor (ejemplo, el token lo genera GitHub):
mkdir C:\actions-runner; cd C:\actions-runner
Invoke-WebRequest -Uri https://github.com/actions/runner/releases/download/v2.x.x/actions-runner-win-x64-2.x.x.zip -OutFile runner.zip
Expand-Archive runner.zip -DestinationPath .
.\config.cmd --url https://github.com/HectorRiquelme/SGIFORM --token TOKEN_GENERADO_POR_GITHUB --labels "self-hosted,windows,iis,android"
.\svc.cmd install
.\svc.cmd start
```

**Dependencia**: El servidor de producción debe estar levantado y accesible antes de esto.

---

### 3. Primer despliegue real en el servidor de producción

**Problema**: Los scripts están listos pero nunca se han ejecutado en el servidor real.

**Prerequisitos antes de ejecutar**:
- [ ] Servidor Windows con acceso RDP/VPN
- [ ] Credenciales de producción definidas (password BD, JWT key de producción)
- [ ] URL/dominio real del sistema (para configurar CORS y Jwt__Issuer)

**Pasos exactos** (en el servidor, PowerShell como Admin):
```powershell
# 1. Clonar o copiar el repo
git clone https://github.com/HectorRiquelme/SGIFORM.git C:\repos\sgiform
# o via robocopy desde máquina de desarrollo

# 2. Copiar scripts SQL
Copy-Item C:\repos\sgiform\database\* C:\SgiForm\scripts\

# 3. Ejecutar despliegue completo (primera vez, con SQL)
Set-ExecutionPolicy RemoteSigned -Scope Process
C:\repos\sgiform\deploy\deploy-server.ps1 `
    -SourceRepoPath "C:\repos\sgiform" `
    -RunSqlScripts:$true

# 4. Validar
C:\repos\sgiform\deploy\validate-deployment.ps1
# Resultado esperado: PASS: 26, WARN: 0, FAIL: 0
```

---

### 4. Configurar CORS para la URL real de producción

**Archivo**: `src/SgiForm.Api/appsettings.Production.json` o variable de entorno del AppPool.

**Problema**: `appsettings.json` tiene origenes de desarrollo (`localhost:5200`, `localhost:7200`). En producción la app móvil y el web necesitan la URL real.

**Solución** (en el servidor, después del deploy):
```powershell
# Reemplazar con la URL real del sistema web
Set-AppPoolEnv "SgiForm-API" "Cors__AllowedOrigins__0" "https://tudominio.cl"
Set-AppPoolEnv "SgiForm-API" "Cors__AllowedOrigins__1" "http://IP_DEL_SERVIDOR"
Restart-WebAppPool "SgiForm-API"
```

---

## Pendientes — MEDIA (mejoran calidad)

### 5. Actualizar CHANGELOG.md

**Archivo**: `CHANGELOG.md` en raíz del repo.

**Problema**: Solo tiene la entrada `[1.0.0] - 2026-03-19`. No refleja nada de lo hecho en las fases 2-6.

**Qué agregar**:
```markdown
## [1.1.0] - 2026-03-22

### Added
- Rate limiting nativo ASP.NET Core 8 (políticas: auth 10/min, api 120/min, sync 30/min)
- Refresh tokens revocables para operadores móviles (columna operador_id en refresh_token)
- FlowValidatorService: validación server-side de campos obligatorios, fotos mínimas y completitud
- Scripts de despliegue: deploy-server.ps1, validate-deployment.ps1, rollback.ps1
- Manual de despliegue completo: deploy/DEPLOY_MANUAL.md (13 secciones)
- appsettings.Production.json: template sin secretos
- Archivos de contexto persistente: CLAUDE.md, AGENTS.md, PROJECT_CONTEXT.md, NEXT_STEPS.md

### Fixed
- SyncController: Enum.TryParse reemplaza Enum.Parse (evitaba crash con estados inválidos)
- TiposInspeccion.razor, Operadores.razor: nullable warnings corregidos con ?? ""
- Health check: removido ::1 de RequireHost (causaba InvalidOperationException)
- Microsoft.IdentityModel.Tokens: versión actualizada a 8.4.0 para eliminar NU1603

### Changed
- Renombramiento completo SanitasField → SgiForm en 180 archivos
- Login móvil: emite JWT 24h + refresh token 30d revocable (antes: JWT 7d sin refresh)
- Health check /health restringido a localhost y 127.0.0.1
- Swagger desactivado explícitamente en Production (solo IsDevelopment)
- Password mínimo 6 caracteres en creación de operadores

### Security
- JWT key generada con RandomNumberGenerator.Fill (64 bytes)
- Secretos de producción exclusivamente en variables de entorno del AppPool IIS
```

---

### 6. Ampliar cobertura de tests de integración

**Archivo**: `tests/SgiForm.Tests/ApiIntegrationTests.cs` (agregar nuevas clases).

**Estado actual**: 46 tests. Módulos sin tests:

| Módulo | Controller | Cobertura actual | Qué falta testear |
|--------|-----------|-----------------|-------------------|
| Flujos | `FlujoController` | 0% | Crear flujo, agregar sección/pregunta, publicar versión |
| Importación | `ImportacionController` | 0% | Upload Excel multipart, preview, confirmar lote |
| Asignaciones | `AsignacionController` | 0% | Asignación individual, masiva, cambio de estado |
| Inspecciones | `InspeccionesController` | 0% | Aprobar, observar, rechazar |
| Dashboard | `DashboardController` | 0% | KPIs con datos semilla existentes |
| Reportes | `ReportesController` | 0% | Generación Excel sin excepción |

**Objetivo**: 46 → ~90 tests.

---

### 7. Refresh token automático en Blazor Web

**Archivos**: `src/SgiForm.Web/Services/ApiClient.cs` + `src/SgiForm.Web/Services/AuthStateService.cs`

**Problema**: El JWT de Blazor expira a los 60 minutos. El usuario ve una pantalla rota o errores 401 sin aviso.

**Qué implementar**:
- `ApiClient.cs`: interceptar HTTP 401, llamar `POST /api/v1/auth/refresh` automáticamente, reintentar el request original
- `AuthStateService.cs`: guardar el `refresh_token` recibido en el login, actualizarlo en cada renovación

---

### 8. Mover lógica de negocio a capa Application

**Archivos**: `src/SgiForm.Application/` (actualmente con `Class1.cs` placeholder)

**Problema**: Los controllers de la API tienen lógica de negocio mezclada con HTTP handling. Esto dificulta los tests unitarios y viola SRP.

**Priorizar por**: `AuthController` (más complejo), `SyncController` (validación + sync + fotos).

---

## Pendientes — BAJA (mejoras futuras)

| # | Tarea | Archivo(s) afectados |
|---|-------|---------------------|
| 9 | Caché IMemoryCache para KPIs (TTL 5 min) | `DashboardController.cs` |
| 10 | Paginación cursor-based para tablas grandes | `ServiciosController.cs`, `InspeccionesController.cs` |
| 11 | Mapa Leaflet.js en panel web para coordenadas GPS | `Inspecciones.razor`, nuevo componente |
| 12 | Notificaciones push FCM a operadores móviles | `SgiForm.Mobile` + nuevo servicio API |
| 13 | MFA/TOTP para usuarios admin | `AuthController.cs`, `AuthService.cs` |

---

## Decisiones técnicas pendientes

| Decisión | Opciones | Recomendación |
|----------|---------|---------------|
| ¿HTTPS en producción? | SSL binding IIS / Cloudflare / nginx | Cloudflare o nginx delante de IIS es lo más simple |
| ¿CQRS en Application layer? | Sí / No | Solo si el equipo crece; no urgente |
| ¿EF Core Migrations vs scripts SQL? | Mantener scripts | Mantener scripts SQL — dan más control y son auditables |
| ¿Versionar API v2? | Solo cuando haya breaking changes | Mantener v1 |

---

## Registro de sesiones

| Fecha | Commits | Trabajo realizado |
|-------|---------|-------------------|
| 2026-03-19 | `dde9a9e` | Creación inicial del proyecto completo |
| 2026-03-22 | `b4b48cb` | QA pre-producción: fix bugs, nullable warnings, package versions |
| 2026-03-22 | `5692c10` | Rate limiting nativo ASP.NET Core 8 (3 políticas) |
| 2026-03-22 | `8995ae6` | Producción: refresh token revocable móvil, FlowValidator, health check seguro, password mínimo |
| 2026-03-22 | `c8fbde0` | DevOps: DEPLOY_MANUAL.md, deploy-server.ps1, validate-deployment.ps1, rollback.ps1, README.md |
| 2026-03-22 | `ed7d51a` | Renombramiento SanitasField → SgiForm (180 archivos, build OK, 46/46 tests) |
| 2026-03-22 | `05dc347` | Contexto persistente: CLAUDE.md, AGENTS.md, PROJECT_CONTEXT.md, NEXT_STEPS.md |
| 2026-03-22 | *(este)* | NEXT_STEPS.md actualizado con detalle completo de sesión y siguientes pasos exactos |

---

## Siguiente paso exacto recomendado

> **Configurar secrets en GitHub + registrar el runner self-hosted**

Es el paso con menor esfuerzo y mayor impacto inmediato: desbloquea el CI/CD automático que ya existe y está codificado correctamente.

**Acción concreta**:
1. Ir a `https://github.com/HectorRiquelme/SGIFORM/settings/secrets/actions`
2. Crear los 4 secrets: `IIS_WEB_PATH`, `IIS_API_PATH`, `IIS_WEB_APPPOOL`, `IIS_API_APPPOOL`
3. En el servidor Windows: registrar el runner self-hosted con labels `self-hosted,windows,iis,android`
4. Hacer un push cualquiera y disparar `deploy-iis.yml` manualmente desde Actions

Si el servidor aún no existe o no está disponible, el siguiente paso alternativo es:

> **Actualizar CHANGELOG.md** (`NEXT_STEPS.md #5`) — puramente documental, 10 minutos, sin riesgo.

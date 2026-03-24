# PROJECT_CONTEXT.md — Estado actual del proyecto

> Fuente de verdad sobre qué está implementado, qué fue cambiado y cuál es el estado real del sistema.
> Actualizar después de cada sesión de trabajo significativa.

---

## Resumen ejecutivo

SgiForm es una plataforma B2B para inspecciones técnicas en terreno de empresas sanitarias. Permite gestionar el ciclo completo: importar servicios desde Excel → configurar formularios dinámicos → asignar a operadores → ejecutar offline en Android → sincronizar → revisar y aprobar → exportar reportes.

**Estado**: Producción-ready. Tests: 46/46. Build: 0 errores.

---

## Historial de fases completadas

### Fase 1 — Creación inicial del proyecto
- Diseño e implementación completa de la arquitectura en capas
- 6 proyectos: Domain, Application, Infrastructure, Api, Web, Mobile
- 21 tablas PostgreSQL en schema `sf`
- 12 controllers REST con Swagger
- 13 páginas Blazor Server
- App MAUI Android offline-first con SQLite y motor de flujos

### Fase 2 — QA pre-producción
- Análisis completo de bugs antes de salir a producción
- Fix: `Enum.TryParse` en SyncController (era `Enum.Parse`, lanzaba excepción con input inválido)
- Fix: nullable warnings en TiposInspeccion.razor y Operadores.razor (`?? ""`)
- Fix: versiones de paquetes NuGet (`Microsoft.IdentityModel.Tokens` → 8.4.0)
- Fix: `RequireHost` en health check (removido `::1` que causaba `InvalidOperationException`)

### Fase 3 — Rate limiting
- Implementado con ASP.NET Core 8 nativo (`Microsoft.AspNetCore.RateLimiting`)
- 3 políticas: `auth` (10/min), `api` (120/min), `sync` (30/min)
- Configurable desde `appsettings.json` → sección `RateLimiting`
- Testing: límites permisivos (1000-5000) para no interferir con tests

### Fase 4 — Producción completa
**Cambios implementados para salir a producción:**

#### Refresh token móvil revocable
- `RefreshToken.UsuarioId` → `Guid?` (nullable)
- `RefreshToken.OperadorId` → `Guid?` (nueva columna)
- Constraint: `usuario_id IS NOT NULL OR operador_id IS NOT NULL`
- `Operador.RefreshTokens` → colección de navegación agregada
- `AuthService.cs` reescrito: login móvil ahora emite JWT 24h + refresh token 30d
- Refresh token móvil: verifica `Activo` del operador; si está desactivado → 401

#### FlowValidatorService (nuevo)
- Valida campos obligatorios, mínimo de fotos, % completitud
- Integrado en SyncController durante upload
- Errores bloqueantes para estados `Completada`/`Enviada`; advertencias para intermedios

#### Migración SQL
- `database/03_operador_refresh_token.sql`: ALTER TABLE + columna + FK + constraint + índice

#### Seguridad adicional
- Health check: `RequireHost("localhost", "127.0.0.1")`
- Password mínimo 6 caracteres en OperadoresController
- Swagger desactivado en producción (`IsDevelopment()`)

#### Tests nuevos (fase 4)
- `RefreshMovil_ConTokenValido_RetornaNuevoToken`
- `RefreshMovil_TokenUsadoDosVeces_SegundoUsoRetorna401`
- `RefreshMovil_OperadorDesactivado_Retorna401` (usa OP_DEACTIVATE_TEST para aislamiento)

### Fase 5 — DevOps / Despliegue
**Archivos creados:**
- `deploy/DEPLOY_MANUAL.md` — 13 secciones, manual completo para Windows Server + IIS + PostgreSQL nativo
- `deploy/deploy-server.ps1` — script PowerShell con 11 pasos automatizados
- `deploy/validate-deployment.ps1` — 26 checks PASS/WARN/FAIL
- `deploy/rollback.ps1` — rollback interactivo o a backup específico
- `README.md` — reescrito con documentación oficial completa
- `src/SgiForm.Api/appsettings.Production.json` — template de producción

### Fase 6 — Renombramiento SgiForm → SgiForm
- **Alcance**: 180 archivos modificados/renombrados
- Directorios `src/SgiForm.*` → `src/SgiForm.*`
- `tests/SgiForm.Tests` → `tests/SgiForm.Tests`
- `shared/SgiForm.Contracts` → `shared/SgiForm.Contracts`
- `SgiForm.sln` → `SgiForm.sln`
- Namespaces C#, connection strings, AppPool names, rutas del servidor
- Build: 0 errores. Tests: 46/46. Zero ocurrencias de "SgiForm" en código.

---

## Estado actual del código

### Tests
```
Total: 46  |  Passed: 46  |  Failed: 0  |  Skipped: 0
Duración: ~4 segundos
```

### Build
```
dotnet build SgiForm.sln -c Release
→ 0 errores, 0 advertencias
```

### Cobertura por módulo

| Módulo | Estado | Notas |
|--------|--------|-------|
| Auth web (login/refresh/logout) | ✅ Completo + tests | |
| Auth móvil (login/refresh) | ✅ Completo + tests | Refresh revocable implementado |
| Operadores CRUD | ✅ Completo + tests | Password min 6 chars |
| Tipos de inspección | ✅ Completo + tests | |
| Flujos | ✅ Completo | Sin tests de integración específicos |
| Importación Excel | ✅ Completo | Sin tests de integración |
| Servicios | ✅ Completo | Sin tests |
| Asignaciones | ✅ Completo | Sin tests |
| Sync (download/upload/photos) | ✅ Completo + tests | FlowValidator integrado |
| Inspecciones (aprobar/observar/rechazar) | ✅ Completo | Sin tests |
| Dashboard KPIs | ✅ Completo | Sin tests |
| Reportes Excel | ✅ Completo | Sin tests |
| Health check | ✅ Seguro | Restringido a localhost |
| Rate limiting | ✅ Activo | 3 políticas |
| FlowValidatorService | ✅ Implementado | Integrado en sync |

### Deuda técnica conocida

| # | Descripción | Severidad |
|---|-------------|-----------|
| 1 | `SgiForm.Application` está vacío (placeholder) — lógica de negocio en controllers | Media |
| 2 | `SgiForm.Contracts` está vacío — DTOs definidos como records inline en controllers | Media |
| 3 | Sin tests de integración para: Flujos, Importación, Servicios, Asignaciones, Dashboard, Reportes | Media |
| 4 | Sin autenticación multi-factor | Baja |
| 5 | Sin paginación cursor-based (usa offset) | Baja |
| 6 | Sin caché (Redis o in-memory) para endpoints de lectura frecuente | Baja |
| 7 | Sin CI/CD funcional post-renombramiento (workflows en `.github/workflows/` pueden requerir ajuste) | Media |
| 8 | Tokens de Blazor Web no tienen refresh automático en frontend | Media |

---

## Entorno de trabajo

### Desarrollo local

| Componente | Tecnología | Detalle |
|------------|-----------|---------|
| PostgreSQL | **Docker** | Imagen `postgres:17`, puerto `5434→5432`, nombre `sgiform_postgres` |
| API REST | .NET 8 dotnet run | Puerto `5043` |
| Web Blazor | .NET 8 dotnet run | Puerto `5054` |
| App Móvil | MAUI Android | Emulador o dispositivo físico |

```
# Docker run para desarrollo:
docker run -d --name sgiform_postgres -p 5434:5432 \
  -e POSTGRES_DB=sgiform -e POSTGRES_USER=sgiform \
  -e POSTGRES_PASSWORD=SgiForm2024! postgres:17
```

**Credenciales de desarrollo (NO usar en producción):**
- BD: `Host=localhost;Port=5434;Database=sgiform;Username=sgiform;Password=SgiForm2024!`
- JWT Key dev: `SgiForm_JWT_SecretKey_2024!@#$_MustBe32CharsMin` (en `appsettings.json`)
- Admin web: `admin@empresa.cl` (seed en `02_seed.sql`)

### Producción (Windows Server 2019 — estado real 2026-03-24)

| Componente | Tecnología | Detalle |
|------------|-----------|---------|
| PostgreSQL | **Nativo Windows** | Servicio `postgresql-x64-16`, puerto `5432` |
| API REST | IIS + ANCM v2 | AppPool `SgiFormApi`, puerto **5001** |
| Web Blazor | IIS + ANCM v2 | AppPool `SgiFormWeb`, puerto **8080** |
| Secretos | Variables de entorno AppPool | Nunca en archivos |
| Archivos | `C:\SgiForm\publish\{api,web}` | |
| Logs | `C:\SgiForm\logs\sgiform-YYYYMMDD.log` | Serilog rolling daily |
| Admin seed | `admin@sanitaria-demo.cl` | empresa_slug: `sanitaria-demo` |

**Fixes aplicados manualmente en producción** (ya incorporados en `01_schema.sql`):
- `sf.refresh_token.ip_origen`: `INET` → `TEXT` (EF Core enviaba text, PostgreSQL rechazaba con 42804)
- `sf.sincronizacion_log.ip_origen`: `INET` → `TEXT` (mismo motivo)
- `appsettings.Production.json`: `AllowedHosts` → `"*"` (antes bloqueaba con HTTP 400 Invalid Hostname)

**Regla**: En producción **NO se usa Docker**. PostgreSQL corre como servicio Windows nativo.

---

## Archivos de configuración clave

| Archivo | Propósito | ¿Contiene secretos? |
|---------|-----------|---------------------|
| `src/SgiForm.Api/appsettings.json` | Config base desarrollo | Sí (credenciales dev) — NO usar en prod |
| `src/SgiForm.Api/appsettings.Production.json` | Template producción | No (valores placeholder) |
| `src/SgiForm.Api/appsettings.Development.json` | Override dev | Sí (dev only) |
| `global.json` | Fija SDK .NET 8.0.319 | No |

---

## Último commit relevante

| Commit | Descripción |
|--------|-------------|
| `ed7d51a` | refactor: renombrar SgiForm → SgiForm en todo el proyecto |
| `c8fbde0` | feat(deploy): agregar documentación y scripts de despliegue completos |
| `8995ae6` | feat(prod): implementar todos los requisitos de producción |
| `5692c10` | feat(security): implementar rate limiting nativo ASP.NET Core 8 |
| `b4b48cb` | fix(qa): corregir bugs detectados en análisis QA pre-producción |

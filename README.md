# SgiForm (SGI-FORM)

Sistema de inspecciones técnicas en terreno para empresas sanitarias. Permite a operadores de campo realizar inspecciones offline desde dispositivos Android y sincronizarlas con un servidor central con validación de flujos y gestión documental.

---

## Índice

1. [Arquitectura](#arquitectura)
2. [Requisitos del Sistema](#requisitos-del-sistema)
3. [Estructura del Repositorio](#estructura-del-repositorio)
4. [Build y Compilación](#build-y-compilación)
5. [Despliegue en IIS (Windows Server)](#despliegue-en-iis-windows-server)
6. [Configuración PostgreSQL](#configuración-postgresql)
7. [Variables de Entorno y Secretos](#variables-de-entorno-y-secretos)
8. [Validación del Despliegue](#validación-del-despliegue)
9. [Troubleshooting](#troubleshooting)
10. [Rollback](#rollback)
11. [Tests](#tests)
12. [Seguridad](#seguridad)

---

## Arquitectura

```
┌─────────────────────────────────────────────────────────┐
│                    SERVIDOR WINDOWS                      │
│                                                         │
│  ┌──────────────┐   ┌──────────────┐   ┌────────────┐  │
│  │ SgiForm │   │ SgiForm │   │ PostgreSQL │  │
│  │     Web      │   │     API      │   │     16     │  │
│  │ (Blazor)     │   │  (ASP.NET 8) │   │ schema: sf │  │
│  │  :80 (IIS)   │   │  :5043 (IIS) │   │  :5432     │  │
│  └──────┬───────┘   └──────┬───────┘   └────────────┘  │
│         │                  │                            │
└─────────┼──────────────────┼────────────────────────────┘
          │                  │
          ▼                  ▼
   Supervisores         Operadores
   (Navegador)         (Android MAUI)
                      [modo offline]
```

### Capas

| Proyecto | Descripción |
|----------|-------------|
| `SgiForm.Domain` | Entidades, enums, interfaces de dominio |
| `SgiForm.Application` | DTOs, contratos de servicios |
| `SgiForm.Infrastructure` | EF Core, PostgreSQL, servicios (Auth, Excel, FlowValidator) |
| `SgiForm.Api` | ASP.NET Core 8 Web API — endpoints REST |
| `SgiForm.Web` | Blazor Server — panel de supervisores |
| `SgiForm.Mobile` | .NET MAUI — app Android offline-first |

### Stack Tecnológico

- **Backend API**: ASP.NET Core 8, EF Core 8, Npgsql
- **Frontend**: Blazor Server 8
- **Mobile**: .NET MAUI (Android)
- **Base de datos**: PostgreSQL 16, schema `sf`
- **Autenticación**: JWT Bearer + Refresh Token rotation
- **Servidor web**: IIS con ANCM v2 (ASP.NET Core Module)
- **Logging**: Serilog (consola + archivos rotativos)
- **Rate limiting**: ASP.NET Core 8 nativo (`Microsoft.AspNetCore.RateLimiting`)

---

## Requisitos del Sistema

### Servidor de Producción

| Componente | Versión mínima | Notas |
|------------|---------------|-------|
| Windows Server | 2019 / 2022 | Recomendado 2022 |
| IIS | 10 | Rol: Web-Server |
| .NET 8 Hosting Bundle | 8.0.x | Incluye runtime + ANCM |
| PostgreSQL | 16 | Instalación nativa (no Docker) |
| RAM | 4 GB | 8 GB recomendado |
| Disco | 20 GB | Para logs y uploads |

### Desarrollo Local

- .NET 8 SDK
- PostgreSQL 16 (local o Docker)
- Visual Studio 2022 / VS Code + C# Dev Kit

---

## Estructura del Repositorio

```
/
├── src/
│   ├── SgiForm.Api/          # API REST principal
│   │   ├── Controllers/
│   │   ├── appsettings.json
│   │   ├── appsettings.Production.json  # template — ver Variables de Entorno
│   │   └── Program.cs
│   ├── SgiForm.Domain/       # Entidades y contratos
│   ├── SgiForm.Infrastructure/
│   │   ├── Migrations/            # EF Core migrations
│   │   ├── Persistence/AppDbContext.cs
│   │   └── Services/
│   ├── SgiForm.Web/          # Blazor Server
│   └── SgiForm.Mobile/       # MAUI Android
├── database/
│   ├── 01_schema.sql              # DDL completo (schema sf)
│   ├── 02_seed.sql                # Datos iniciales / demo
│   └── 03_operador_refresh_token.sql  # Migración: tokens móviles
├── tests/
│   └── SgiForm.Tests/        # Tests de integración xUnit
├── deploy/
│   ├── DEPLOY_MANUAL.md           # Manual completo paso a paso
│   ├── deploy-server.ps1          # Script automatizado de despliegue
│   ├── rollback.ps1               # Script de rollback
│   └── validate-deployment.ps1    # Validación post-despliegue
└── README.md                      # Este archivo
```

---

## Build y Compilación

### Restaurar dependencias

```powershell
dotnet restore SgiForm.sln
```

### Compilar solución completa

```powershell
dotnet build SgiForm.sln -c Release
```

### Ejecutar tests

```powershell
dotnet test tests/SgiForm.Tests/SgiForm.Tests.csproj -v normal
```

Los tests usan base de datos InMemory (`ASPNETCORE_ENVIRONMENT=Testing`). No requieren PostgreSQL.

### Publicar para producción

```powershell
# API
dotnet publish src/SgiForm.Api/SgiForm.Api.csproj `
    -c Release -r win-x64 --self-contained false `
    -o C:\SgiForm\api

# Web (Blazor)
dotnet publish src/SgiForm.Web/SgiForm.Web.csproj `
    -c Release -r win-x64 --self-contained false `
    -o C:\SgiForm\web
```

---

## Despliegue en IIS (Windows Server)

### Despliegue Automatizado (Recomendado)

```powershell
# Ejecutar como Administrador
Set-ExecutionPolicy RemoteSigned -Scope Process
.\deploy\deploy-server.ps1
```

El script realiza automáticamente:
1. Verificación de prerequisitos (IIS, .NET 8, ANCM, PostgreSQL)
2. Backup del despliegue anterior
3. Build desde fuente (opcional) con gate de tests
4. Publicación con `robocopy /MIR`
5. Creación/configuración de Application Pools
6. Configuración de sitios IIS
7. Variables de entorno con secretos seguros
8. Ejecución de scripts SQL (opcional)
9. Validación post-despliegue

### Despliegue Manual

Ver `deploy/DEPLOY_MANUAL.md` para instrucciones completas paso a paso.

#### Estructura de carpetas en servidor

```
C:\SgiForm\
├── api\           # Publicación de SgiForm.Api
├── web\           # Publicación de SgiForm.Web
├── uploads\       # Fotos de inspecciones (NTFS: IIS_IUSRS ← Modify)
├── logs\          # Logs de Serilog (NTFS: IIS_IUSRS ← Modify)
└── backups\       # Backups automáticos pre-despliegue
```

#### Application Pools IIS

| Pool | Pipeline | .NET CLR | Idle Timeout | Start Mode |
|------|----------|----------|-------------|------------|
| SgiForm-API | Integrated | No Managed Code | 0 min | AlwaysRunning |
| SgiForm-Web | Integrated | No Managed Code | 0 min | AlwaysRunning |

#### Bindings IIS

| Sitio | Puerto | AppPool |
|-------|--------|---------|
| SgiForm-API | 5043 | SgiForm-API |
| SgiForm-Web | 80 | SgiForm-Web |

---

## Configuración PostgreSQL

### Instalación

PostgreSQL 16 se instala como servicio nativo de Windows (no Docker). Ruta esperada:
```
C:\PostgreSQL\16\bin\psql.exe
```

### Scripts de base de datos

Ejecutar en orden:

```powershell
$psql = "C:\PostgreSQL\16\bin\psql.exe"

# 1. Schema, tablas, constraints, índices (como superusuario)
& $psql -U postgres -f "database\01_schema.sql"

# 2. Datos iniciales (empresa demo, admin, tipos de inspección)
& $psql -U postgres -f "database\02_seed.sql"

# 3. Migración: soporte refresh token operadores móviles
& $psql -U sgiform -d sgiform -f "database\03_operador_refresh_token.sql"
```

### Usuario de base de datos

El script `01_schema.sql` crea el usuario `sgiform`. Cambiar la contraseña antes del primer despliegue:

```sql
ALTER USER sgiform WITH PASSWORD 'nueva_password_segura';
```

### Cadena de conexión

```
Host=localhost;Port=5432;Database=sgiform;Username=sgiform;Password=TU_PASSWORD
```

---

## Variables de Entorno y Secretos

**NUNCA** poner secretos en `appsettings.json` ni en el repositorio.
Los secretos se configuran como **variables de entorno del AppPool IIS**.

### Variables obligatorias — AppPool SgiForm-API

| Variable | Ejemplo | Descripción |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Modo de ejecución |
| `ConnectionStrings__Default` | `Host=localhost;Port=5432;Database=sgiform;...` | Cadena de conexión PostgreSQL |
| `Jwt__Key` | `[64+ caracteres aleatorios]` | Clave HMAC-SHA256 para JWT |
| `Jwt__Issuer` | `https://api.miempresa.cl` | Issuer del token |
| `Jwt__Audience` | `SgiForm` | Audience del token |
| `Storage__UploadPath` | `C:\SgiForm\uploads` | Ruta de fotos |

### Variables opcionales — Rate Limiting

| Variable | Default | Descripción |
|----------|---------|-------------|
| `RateLimiting__AuthPermitLimit` | `10` | Req/min por IP en login |
| `RateLimiting__ApiPermitLimit` | `120` | Req/min por IP endpoints generales |
| `RateLimiting__SyncPermitLimit` | `30` | Req/min por operador en sincronización |

### Generar Jwt__Key seguro

```powershell
[Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Maximum 256 }))
```

### Configurar variables vía PowerShell

```powershell
Import-Module WebAdministration
$pool = Get-Item "IIS:\AppPools\SgiForm-API"
$pool.environmentVariables.Add("Jwt__Key", "tu_clave_aqui")
$pool | Set-Item
```

---

## Validación del Despliegue

```powershell
# Ejecutar como Administrador
.\deploy\validate-deployment.ps1
```

Verifica:
- Servicios Windows (IIS W3SVC, PostgreSQL 16)
- Application Pools (estado Started)
- Sitios IIS (estado Started, bindings)
- Archivos críticos presentes
- Permisos de escritura en `uploads/` y `logs/`
- Puertos TCP (5432, 5043, 80) accesibles
- Endpoints HTTP: `GET /health` → 200, `GET /` → 200
- Swagger desactivado en Production (esperado 404)
- Conectividad PostgreSQL y conteo de tablas schema `sf` (≥ 20)
- Migración 03 aplicada (`operador_id` en `refresh_token`)
- Variables de entorno del AppPool

### Resultado esperado

```
  PASS : 22+
  WARN : 0
  FAIL : 0
  ESTADO: PRODUCCIÓN LISTA ✓
```

---

## Troubleshooting

### HTTP 502.5 — Process Failure

```powershell
# Ver logs ANCM en Event Viewer
Get-EventLog -LogName Application -Source "IIS AspNetCore Module*" -Newest 10

# Ver logs de la aplicación
Get-Content "C:\SgiForm\logs\api-*.log" -Tail 50
```

**Causas comunes:**
- .NET 8 Runtime no instalado → Instalar Hosting Bundle y reiniciar IIS
- Variable `Jwt__Key` no configurada → revisar variables de entorno del AppPool
- Cadena de conexión incorrecta → verificar usuario/password PostgreSQL

### HTTP 500.30 — ASP.NET Core App Failed to Start

```powershell
# Probar startup manualmente (muestra error en consola)
cd C:\SgiForm\api
.\SgiForm.Api.exe
```

### PostgreSQL no conecta

```powershell
# Verificar servicio
Get-Service "postgresql-16"

# Probar conexión directa
& "C:\PostgreSQL\16\bin\psql.exe" -U sgiform -d sgiform -c "\conninfo"
```

### AppPool se detiene solo (Rapid Fail Protection)

```powershell
# Ver log de eventos
Get-EventLog -LogName System -Source "Microsoft-Windows-WAS" -Newest 20

# Reiniciar
Restart-WebAppPool "SgiForm-API"
```

### Permisos insuficientes en uploads/logs

```powershell
icacls "C:\SgiForm\uploads" /grant "IIS_IUSRS:(OI)(CI)M"
icacls "C:\SgiForm\logs"   /grant "IIS_IUSRS:(OI)(CI)M"
```

---

## Rollback

```powershell
# Rollback interactivo al último backup disponible
.\deploy\rollback.ps1

# Rollback a un backup específico
.\deploy\rollback.ps1 -BackupPath "C:\SgiForm\backups\20260322_143000"
```

El script detiene los AppPools, restaura archivos con `robocopy`, reinicia los pools y ejecuta validación automática.

**Rollback de base de datos** se realiza manualmente:

```sql
-- Revertir migración 03 si es necesario
ALTER TABLE sf.refresh_token DROP COLUMN IF EXISTS operador_id;
ALTER TABLE sf.refresh_token ALTER COLUMN usuario_id SET NOT NULL;
DROP INDEX IF EXISTS sf.idx_rt_operador_id;
```

---

## Tests

### Ejecutar todos los tests

```powershell
dotnet test tests/SgiForm.Tests/ -v normal
```

**Resultado esperado: 46 tests, 0 failures.**

Los tests de integración usan `WebApplicationFactory<Program>` con base de datos InMemory y no requieren PostgreSQL ni IIS.

### Clases de test

| Clase | Tests | Descripción |
|-------|-------|-------------|
| `AuthTests` | ~8 | Login web/móvil, refresh token, revocación |
| `OperadoresTests` | ~6 | CRUD operadores, validación password |
| `TiposInspeccionTests` | ~4 | CRUD tipos de inspección |
| `SyncTests` | ~12 | Download, upload, fotos, validación de flujo |
| `FlowTests` | ~7 | Motor de reglas, flujo condicional |
| `ExcelImportTests` | ~5 | Importación de servicios desde Excel |
| `HealthTests` | ~4 | Health check, restricción localhost |

### Entorno de testing

- `ASPNETCORE_ENVIRONMENT=Testing`
- Base de datos InMemory (sin PostgreSQL)
- Rate limiting permisivo (1000-5000 req/min)
- Serilog desactivado (sin archivos de log)

---

## Seguridad

| Control | Implementación |
|---------|---------------|
| Autenticación | JWT Bearer, 60 min (web) / 24h (móvil) |
| Refresh tokens | Rotación en cada uso, almacenados en BD |
| Tokens móviles | Revocables; revocados al desactivar operador |
| Rate limiting | 10/min auth, 120/min API, 30/min sync (por IP) |
| Health check | Restringido a `localhost` y `127.0.0.1` |
| Swagger | Solo en entorno `Development` |
| Secretos | Variables de entorno AppPool (nunca en código) |
| Passwords | BCrypt hash (work factor 11) |
| Multi-tenant | Aislamiento por `empresa_id` en todas las queries |
| CORS | Origins explícitos en `Cors:AllowedOrigins` |
| HTTPS | Redirección automática (`UseHttpsRedirection`) |
| Enum parsing | `Enum.TryParse` — nunca `Enum.Parse` en inputs externos |

---

## Licencia

Uso interno — SgiForm © 2026

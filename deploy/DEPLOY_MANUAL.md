# Manual de Despliegue — SanitasField v1.0
## Windows Server + IIS + PostgreSQL Nativo

> **Nivel:** Producción real
> **Stack:** .NET 8 · ASP.NET Core · Blazor Server · PostgreSQL 16 · IIS 10
> **Tiempo estimado:** 90–120 minutos (primera vez)

---

## ÍNDICE

1. [Arquitectura objetivo](#1-arquitectura-objetivo)
2. [Requisitos previos del servidor](#2-requisitos-previos-del-servidor)
3. [Preparación del servidor](#3-preparación-del-servidor)
4. [Instalación de .NET 8 Hosting Bundle](#4-instalación-de-net-8-hosting-bundle)
5. [Instalación de PostgreSQL 16 nativo](#5-instalación-de-postgresql-16-nativo)
6. [Creación de base de datos y ejecución de scripts](#6-creación-de-base-de-datos-y-ejecución-de-scripts)
7. [Build y publicación de la aplicación](#7-build-y-publicación-de-la-aplicación)
8. [Configuración de IIS](#8-configuración-de-iis)
9. [Variables de entorno y secretos](#9-variables-de-entorno-y-secretos)
10. [Estructura de carpetas en producción](#10-estructura-de-carpetas-en-producción)
11. [Validación del despliegue](#11-validación-del-despliegue)
12. [Troubleshooting](#12-troubleshooting)
13. [Rollback](#13-rollback)

---

## 1. ARQUITECTURA OBJETIVO

```
┌─────────────────────────────────────────────────────────────┐
│                   Windows Server 2019/2022                  │
│                                                             │
│  ┌─────────────────────┐   ┌─────────────────────────────┐  │
│  │   IIS - Sitio Web   │   │     IIS - Sitio API         │  │
│  │   Puerto 80/443     │   │     Puerto 5043             │  │
│  │   SanitasField.Web  │   │     SanitasField.Api        │  │
│  │   (Blazor Server)   │   │     (ASP.NET Core REST)     │  │
│  │   AppPool: Web      │   │     AppPool: API            │  │
│  └─────────────────────┘   └─────────────────────────────┘  │
│           │                         │                        │
│           └──────────┬──────────────┘                        │
│                      ▼                                        │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │           PostgreSQL 16 (Servicio Windows)              │  │
│  │           localhost:5432  Database: sanitasfield        │  │
│  │           Schema: sf       User: sanitasfield           │  │
│  └─────────────────────────────────────────────────────────┘  │
│                                                             │
│  C:\SanitasField\                                           │
│    ├── api\          ← publicación API                      │
│    ├── web\          ← publicación Web                      │
│    ├── uploads\      ← fotos de inspección                  │
│    └── logs\         ← archivos de log                      │
└─────────────────────────────────────────────────────────────┘
         ▲                        ▲
         │ HTTPS                  │ HTTPS
    Navegador Web          App Móvil MAUI
    (Admin/Supervisor)    (Operadores campo)
```

### Puertos en producción

| Componente | Puerto | Protocolo |
|---|---|---|
| IIS Web (Blazor) | 80 / 443 | HTTP/HTTPS |
| IIS API | 5043 | HTTP (o 443 con binding adicional) |
| PostgreSQL | 5432 | TCP local |

---

## 2. REQUISITOS PREVIOS DEL SERVIDOR

### Sistema operativo
- Windows Server 2019 o 2022 (recomendado)
- Mínimo 4 GB RAM, 8 GB recomendado
- 40 GB disco libre

### Software a instalar (este manual lo hace)
- IIS con módulos ASP.NET Core
- .NET 8 Hosting Bundle
- PostgreSQL 16 para Windows

### Acceso requerido
- Sesión RDP o consola con permisos de Administrador local
- VPN activa para acceso remoto
- Acceso a internet (o repositorio interno de paquetes)

---

## 3. PREPARACIÓN DEL SERVIDOR

### 3.1 Activar IIS y módulos necesarios

Ejecutar en PowerShell como **Administrador**:

```powershell
# Instalar IIS con módulos requeridos por ASP.NET Core
Enable-WindowsOptionalFeature -Online -FeatureName `
    IIS-WebServerRole, `
    IIS-WebServer, `
    IIS-CommonHttpFeatures, `
    IIS-HttpErrors, `
    IIS-HttpRedirect, `
    IIS-ApplicationDevelopment, `
    IIS-NetFxExtensibility45, `
    IIS-HealthAndDiagnostics, `
    IIS-HttpLogging, `
    IIS-Security, `
    IIS-RequestFiltering, `
    IIS-Performance, `
    IIS-WebServerManagementTools, `
    IIS-ManagementConsole, `
    IIS-ASPNET45 `
    -All -NoRestart
```

> **IMPORTANTE:** ASP.NET Core en IIS NO usa el módulo ASPNET45. El módulo correcto es el **ASP.NET Core Module (ANCM)** que se instala con el Hosting Bundle (Paso 4).

### 3.2 Crear estructura de carpetas

```powershell
# Crear carpetas de la aplicación
$dirs = @(
    'C:\SanitasField\api',
    'C:\SanitasField\web',
    'C:\SanitasField\uploads',
    'C:\SanitasField\logs',
    'C:\SanitasField\backups',
    'C:\SanitasField\scripts'
)
foreach ($d in $dirs) { New-Item -ItemType Directory -Path $d -Force }
```

### 3.3 Copiar scripts SQL al servidor

```powershell
# Desde la máquina de origen (ajustar IP/ruta según entorno)
# Ejemplo si ya tienes acceso RDP:
Copy-Item ".\database\01_schema.sql"  "C:\SanitasField\scripts\"
Copy-Item ".\database\02_seed.sql"    "C:\SanitasField\scripts\"
Copy-Item ".\database\03_operador_refresh_token.sql" "C:\SanitasField\scripts\"
```

---

## 4. INSTALACIÓN DE .NET 8 HOSTING BUNDLE

El **Hosting Bundle** instala el runtime de .NET 8 **y** el módulo ASP.NET Core para IIS.

### 4.1 Descargar e instalar

```powershell
# Descargar Hosting Bundle .NET 8 LTS
$url = "https://download.visualstudio.microsoft.com/download/pr/dotnet-hosting-8.0-win.exe"
$destino = "C:\SanitasField\dotnet-hosting-8.0-win.exe"

Write-Host "Descargando .NET 8 Hosting Bundle..."
Invoke-WebRequest -Uri $url -OutFile $destino

# Instalar silenciosamente
Start-Process -FilePath $destino `
    -ArgumentList "/quiet /norestart OPT_NO_X86=1" `
    -Wait -PassThru

Write-Host "Instalación completada."
```

### 4.2 Reiniciar IIS para cargar el módulo ANCM

```powershell
net stop was /y
net start w3svc
```

### 4.3 Verificar instalación

```powershell
dotnet --version
# Debe mostrar: 8.0.x

# Verificar que el módulo ANCM está instalado en IIS
Get-WebConfiguration -Filter "system.webServer/globalModules/add[@name='AspNetCoreModuleV2']"
# Debe retornar el módulo configurado
```

---

## 5. INSTALACIÓN DE POSTGRESQL 16 NATIVO

### 5.1 Descargar el instalador

```powershell
$pgUrl = "https://get.enterprisedb.com/postgresql/postgresql-16.3-1-windows-x64.exe"
$pgInstaller = "C:\SanitasField\postgresql-16.3-1-windows-x64.exe"
Invoke-WebRequest -Uri $pgUrl -OutFile $pgInstaller
```

### 5.2 Instalar PostgreSQL (modo silencioso)

```powershell
# IMPORTANTE: Reemplazar PG_SA_PASSWORD con una contraseña segura para el superusuario 'postgres'
$pgSaPassword = "SuperAdmin_PG_2024!"   # ← CAMBIAR

Start-Process -FilePath $pgInstaller -ArgumentList `
    "--mode unattended",
    "--superpassword `"$pgSaPassword`"",
    "--servicename postgresql-16",
    "--serviceaccount NT AUTHORITY\NetworkService",
    "--serverport 5432",
    "--datadir C:\PostgreSQL\16\data",
    "--prefix C:\PostgreSQL\16" `
    -Wait -PassThru

Write-Host "PostgreSQL 16 instalado."
```

### 5.3 Agregar psql al PATH del sistema

```powershell
$pgBin = "C:\PostgreSQL\16\bin"
[Environment]::SetEnvironmentVariable(
    "Path",
    [Environment]::GetEnvironmentVariable("Path", "Machine") + ";$pgBin",
    "Machine"
)
# Refrescar en la sesión actual
$env:Path += ";$pgBin"

# Verificar
psql --version
# Esperado: psql (PostgreSQL) 16.x
```

### 5.4 Verificar servicio PostgreSQL

```powershell
Get-Service -Name "postgresql-16" | Select-Object Name, Status, StartType
# Status debe ser: Running
# StartType debe ser: Automatic
```

---

## 6. CREACIÓN DE BASE DE DATOS Y EJECUCIÓN DE SCRIPTS

### 6.1 Crear usuario y base de datos

```powershell
# Variables (ajustar contraseña)
$pgBin   = "C:\PostgreSQL\16\bin"
$pgSa    = "postgres"
$pgSaPwd = "SuperAdmin_PG_2024!"     # contraseña del superusuario postgres
$appUser = "sanitasfield"
$appPwd  = "CAMBIAR_PASSWORD_APP"    # ← contraseña del usuario de aplicación
$dbName  = "sanitasfield"

# Exportar contraseña para que psql no la solicite interactivamente
$env:PGPASSWORD = $pgSaPwd

# 1. Crear usuario de aplicación
& "$pgBin\psql.exe" -U $pgSa -h localhost -c `
    "CREATE USER $appUser WITH PASSWORD '$appPwd';"

# 2. Crear base de datos asignada al usuario
& "$pgBin\psql.exe" -U $pgSa -h localhost -c `
    "CREATE DATABASE $dbName OWNER $appUser ENCODING 'UTF8' LC_COLLATE 'Spanish_Chile.1252' LC_CTYPE 'Spanish_Chile.1252' TEMPLATE template0;"

# 3. Otorgar privilegios
& "$pgBin\psql.exe" -U $pgSa -h localhost -c `
    "GRANT ALL PRIVILEGES ON DATABASE $dbName TO $appUser;"

Write-Host "Base de datos y usuario creados."
```

> **Nota sobre encoding:** Si el servidor no tiene `Spanish_Chile.1252`, usar `LC_COLLATE 'en_US.UTF-8'` o simplemente omitir los parámetros LC para usar el default del sistema.

### 6.2 Ejecutar scripts SQL en orden

```powershell
$env:PGPASSWORD = $pgSaPwd

# Script 01: Schema completo
Write-Host "Ejecutando 01_schema.sql..."
& "$pgBin\psql.exe" -U $pgSa -h localhost -d $dbName `
    -f "C:\SanitasField\scripts\01_schema.sql"
if ($LASTEXITCODE -ne 0) { throw "ERROR en 01_schema.sql" }

# Script 02: Datos semilla
Write-Host "Ejecutando 02_seed.sql..."
& "$pgBin\psql.exe" -U $pgSa -h localhost -d $dbName `
    -f "C:\SanitasField\scripts\02_seed.sql"
if ($LASTEXITCODE -ne 0) { throw "ERROR en 02_seed.sql" }

# Script 03: Migración refresh token operadores
Write-Host "Ejecutando 03_operador_refresh_token.sql..."
& "$pgBin\psql.exe" -U $pgSa -h localhost -d $dbName `
    -f "C:\SanitasField\scripts\03_operador_refresh_token.sql"
if ($LASTEXITCODE -ne 0) { throw "ERROR en 03_operador_refresh_token.sql" }

Write-Host "Scripts SQL ejecutados correctamente."
Remove-Item Env:\PGPASSWORD  # Limpiar contraseña de entorno
```

### 6.3 Verificar que el schema se creó correctamente

```powershell
$env:PGPASSWORD = $pgSaPwd
& "$pgBin\psql.exe" -U $pgSa -h localhost -d sanitasfield -c `
    "SELECT table_name FROM information_schema.tables WHERE table_schema = 'sf' ORDER BY table_name;"
```

Debe listar al menos 20 tablas: `asignacion_inspeccion`, `auditoria`, `catalogo`, `empresa`, `flujo`, etc.

### 6.4 Configurar pg_hba.conf para conexión local

Editar `C:\PostgreSQL\16\data\pg_hba.conf` para asegurar acceso local:

```
# Agregar o verificar que esta línea exista:
host    sanitasfield    sanitasfield    127.0.0.1/32    scram-sha-256
```

Recargar configuración:

```powershell
& "$pgBin\pg_ctl.exe" reload -D "C:\PostgreSQL\16\data"
```

---

## 7. BUILD Y PUBLICACIÓN DE LA APLICACIÓN

### 7.1 En la máquina de build (o servidor con código fuente)

```powershell
# Posicionarse en la raíz del repositorio
cd C:\repos\SanitasField

# Restaurar dependencias
dotnet restore SanitasField.sln

# Build en modo Release
dotnet build SanitasField.sln -c Release --no-restore

# Ejecutar tests antes de publicar (obligatorio)
dotnet test tests/SanitasField.Tests/SanitasField.Tests.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw "Tests fallaron. No se publicará." }

# Publicar API
dotnet publish src/SanitasField.Api/SanitasField.Api.csproj `
    -c Release `
    -o "C:\publish\api" `
    --no-build `
    -p:EnvironmentName=Production

# Publicar Web
dotnet publish src/SanitasField.Web/SanitasField.Web.csproj `
    -c Release `
    -o "C:\publish\web" `
    --no-build `
    -p:EnvironmentName=Production
```

### 7.2 Transferir publicación al servidor de producción

```powershell
# Opción A: Si se construye en el mismo servidor
robocopy "C:\publish\api" "C:\SanitasField\api" /MIR /R:3 /W:5
robocopy "C:\publish\web" "C:\SanitasField\web" /MIR /R:3 /W:5

# Opción B: Desde máquina de origen via red (ajustar UNC path)
robocopy "C:\publish\api" "\\SERVIDOR\C$\SanitasField\api" /MIR /R:3 /W:5
robocopy "C:\publish\web" "\\SERVIDOR\C$\SanitasField\web" /MIR /R:3 /W:5
```

### 7.3 Verificar web.config generado

El `dotnet publish` genera automáticamente `web.config` para IIS con el módulo ANCM. Verificar que existe:

```powershell
Test-Path "C:\SanitasField\api\web.config"  # Debe ser True
Test-Path "C:\SanitasField\web\web.config"  # Debe ser True
```

---

## 8. CONFIGURACIÓN DE IIS

### 8.1 Crear Application Pools

```powershell
Import-Module WebAdministration

# AppPool para la API
New-WebAppPool -Name "SanitasField-API"
Set-ItemProperty "IIS:\AppPools\SanitasField-API" managedRuntimeVersion ""
Set-ItemProperty "IIS:\AppPools\SanitasField-API" startMode "AlwaysRunning"
Set-ItemProperty "IIS:\AppPools\SanitasField-API" processModel.idleTimeout "00:00:00"
Set-ItemProperty "IIS:\AppPools\SanitasField-API" recycling.periodicRestart.time "00:00:00"

# AppPool para la Web (Blazor)
New-WebAppPool -Name "SanitasField-Web"
Set-ItemProperty "IIS:\AppPools\SanitasField-Web" managedRuntimeVersion ""
Set-ItemProperty "IIS:\AppPools\SanitasField-Web" startMode "AlwaysRunning"
Set-ItemProperty "IIS:\AppPools\SanitasField-Web" processModel.idleTimeout "00:00:00"
Set-ItemProperty "IIS:\AppPools\SanitasField-Web" recycling.periodicRestart.time "00:00:00"
```

> **Crítico:** `managedRuntimeVersion = ""` significa **No Managed Code**. Esto es OBLIGATORIO para ASP.NET Core.

### 8.2 Crear sitios IIS

```powershell
# Sitio API — Puerto 5043
New-Website -Name "SanitasField-API" `
    -PhysicalPath "C:\SanitasField\api" `
    -ApplicationPool "SanitasField-API" `
    -Port 5043 `
    -Force

# Sitio Web (Blazor) — Puerto 80
New-Website -Name "SanitasField-Web" `
    -PhysicalPath "C:\SanitasField\web" `
    -ApplicationPool "SanitasField-Web" `
    -Port 80 `
    -Force
```

### 8.3 Configurar permisos de carpetas

```powershell
# La identidad del AppPool necesita acceso a sus carpetas
$acl = Get-Acl "C:\SanitasField\api"
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "IIS AppPool\SanitasField-API", "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.SetAccessRule($rule)
Set-Acl "C:\SanitasField\api" $acl

$acl = Get-Acl "C:\SanitasField\web"
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "IIS AppPool\SanitasField-Web", "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.SetAccessRule($rule)
Set-Acl "C:\SanitasField\web" $acl

# Uploads y logs necesitan escritura para la API
foreach ($carpeta in @("C:\SanitasField\uploads", "C:\SanitasField\logs")) {
    $acl = Get-Acl $carpeta
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        "IIS AppPool\SanitasField-API", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($rule)
    Set-Acl $carpeta $acl
}

Write-Host "Permisos configurados."
```

### 8.4 Iniciar sitios

```powershell
Start-WebSite -Name "SanitasField-API"
Start-WebSite -Name "SanitasField-Web"
Start-WebAppPool -Name "SanitasField-API"
Start-WebAppPool -Name "SanitasField-Web"
```

---

## 9. VARIABLES DE ENTORNO Y SECRETOS

> **REGLA DE ORO:** Ninguna credencial en texto plano en archivos del repositorio. Todo secreto va en variables de entorno del servidor.

### 9.1 Por qué variables de entorno en el AppPool

ASP.NET Core lee variables de entorno con prefijo `ASPNETCORE_` y también mapea variables directas al `IConfiguration`. El formato de doble guion bajo (`__`) equivale a la separación de secciones en JSON.

### 9.2 Configurar variables en el AppPool de IIS (recomendado)

```powershell
# Función helper
function Set-AppPoolEnv {
    param([string]$Pool, [string]$Name, [string]$Value)
    $pool = Get-Item "IIS:\AppPools\$Pool"
    $pool.environmentVariables | Where-Object { $_.name -eq $Name } | ForEach-Object {
        $pool.environmentVariables.Remove($_)
    }
    $env = $pool.environmentVariables.CreateElement("add")
    $env.name = $Name
    $env.value = $Value
    $pool.environmentVariables.Add($env)
    $pool | Set-Item
}

# ─── Variables para SanitasField-API ────────────────────────────────────────

# Entorno .NET
Set-AppPoolEnv "SanitasField-API" "ASPNETCORE_ENVIRONMENT" "Production"

# Connection string completo (REEMPLAZAR valores reales)
Set-AppPoolEnv "SanitasField-API" "ConnectionStrings__Default" `
    "Host=localhost;Port=5432;Database=sanitasfield;Username=sanitasfield;Password=TU_PASSWORD_AQUI;Search Path=sf,public"

# JWT — USAR UNA CLAVE ALEATORIA DE 64+ CARACTERES
Set-AppPoolEnv "SanitasField-API" "Jwt__Key" `
    "GENERAR_CLAVE_ALEATORIA_MINIMO_64_CARACTERES_AQUI_UNICA_POR_ENTORNO"
Set-AppPoolEnv "SanitasField-API" "Jwt__Issuer" "SanitasField"
Set-AppPoolEnv "SanitasField-API" "Jwt__Audience" "SanitasField"
Set-AppPoolEnv "SanitasField-API" "Jwt__ExpirationMinutes" "60"

# Storage
Set-AppPoolEnv "SanitasField-API" "Storage__UploadPath" "C:\SanitasField\uploads"
Set-AppPoolEnv "SanitasField-API" "Storage__MaxPhotoMb" "10"

# ─── Variables para SanitasField-Web ────────────────────────────────────────
Set-AppPoolEnv "SanitasField-Web" "ASPNETCORE_ENVIRONMENT" "Production"
Set-AppPoolEnv "SanitasField-Web" "ApiBaseUrl" "http://localhost:5043"
```

### 9.3 Generar JWT Key segura

```powershell
# Generar clave aleatoria de 64 bytes (512 bits) en Base64
$bytes = New-Object byte[] 64
[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
$key = [Convert]::ToBase64String($bytes)
Write-Host "JWT Key generada:"
Write-Host $key
# Copiar este valor y usarlo en Set-AppPoolEnv "SanitasField-API" "Jwt__Key"
```

### 9.4 Verificar variables configuradas

```powershell
# Ver variables del AppPool API
(Get-Item "IIS:\AppPools\SanitasField-API").environmentVariables |
    Select-Object name, @{N='value';E={
        if ($_.name -match "Password|Key|Secret") {"***OCULTO***"} else {$_.value}
    }} |
    Format-Table -AutoSize
```

---

## 10. ESTRUCTURA DE CARPETAS EN PRODUCCIÓN

```
C:\
├── SanitasField\
│   ├── api\                    ← Publicación de SanitasField.Api
│   │   ├── SanitasField.Api.exe
│   │   ├── web.config          ← Generado por dotnet publish (ANCM)
│   │   ├── appsettings.json    ← Valores base (sin secretos)
│   │   └── appsettings.Production.json ← Overrides no sensibles
│   ├── web\                    ← Publicación de SanitasField.Web
│   │   ├── SanitasField.Web.exe
│   │   ├── web.config
│   │   └── appsettings.json
│   ├── uploads\                ← Fotos de inspección (escritura API)
│   ├── logs\                   ← Logs Serilog (escritura API)
│   ├── backups\                ← Backups de BD
│   └── scripts\                ← Scripts SQL (ejecución única)
│       ├── 01_schema.sql
│       ├── 02_seed.sql
│       └── 03_operador_refresh_token.sql
│
└── PostgreSQL\
    └── 16\
        ├── bin\                ← psql.exe, pg_dump.exe, etc.
        └── data\               ← Datos de PostgreSQL
```

---

## 11. VALIDACIÓN DEL DESPLIEGUE

### 11.1 Verificar servicios activos

```powershell
# PostgreSQL corriendo
Get-Service "postgresql-16" | Select-Object Name, Status

# IIS corriendo
Get-Service W3SVC | Select-Object Name, Status

# AppPools activos
Get-ChildItem "IIS:\AppPools" |
    Where-Object { $_.Name -match "SanitasField" } |
    Select-Object Name, State
```

### 11.2 Verificar conectividad de la API

```powershell
# Health check (solo desde localhost)
try {
    $resp = Invoke-WebRequest -Uri "http://localhost:5043/health" -UseBasicParsing
    Write-Host "Health check API: $($resp.StatusCode) - $($resp.Content)"
} catch {
    Write-Host "ERROR: $($_.Exception.Message)"
}
```

### 11.3 Verificar login de usuario

```powershell
$body = '{"email":"admin@empresa.cl","password":"TuPassword"}'
try {
    $resp = Invoke-RestMethod -Uri "http://localhost:5043/api/v1/auth/login" `
        -Method POST -Body $body -ContentType "application/json"
    Write-Host "Login exitoso. Token: $($resp.access_token.Substring(0,30))..."
} catch {
    Write-Host "ERROR login: $($_.Exception.Response.StatusCode)"
}
```

### 11.4 Verificar web Blazor

```powershell
try {
    $resp = Invoke-WebRequest -Uri "http://localhost:80" -UseBasicParsing
    Write-Host "Web Blazor: HTTP $($resp.StatusCode)"
} catch {
    Write-Host "ERROR web: $($_.Exception.Message)"
}
```

### 11.5 Verificar conectividad a PostgreSQL

```powershell
$env:PGPASSWORD = "TU_PASSWORD_APP"
& "C:\PostgreSQL\16\bin\psql.exe" -U sanitasfield -h localhost -d sanitasfield -c `
    "SELECT COUNT(*) as tablas FROM information_schema.tables WHERE table_schema = 'sf';"
Remove-Item Env:\PGPASSWORD
```

---

## 12. TROUBLESHOOTING

### 12.1 HTTP 500.30 — ANCM In-Process Handler Load Failure

**Causa más común:** Hosting Bundle no instalado o versión incorrecta.

```powershell
# Verificar que ANCM V2 está instalado
Get-WebConfiguration "system.webServer/globalModules/add[@name='AspNetCoreModuleV2']"

# Si no aparece, reinstalar Hosting Bundle:
Start-Process "C:\SanitasField\dotnet-hosting-8.0-win.exe" `
    -ArgumentList "/quiet /norestart" -Wait
net stop was /y && net start w3svc
```

**Revisar logs de IIS:**
```powershell
Get-Content "C:\Windows\System32\LogFiles\HTTPERR\httperr*.log" -Tail 20
# También revisar:
Get-Content "C:\SanitasField\logs\sanitasfield-$(Get-Date -f yyyyMMdd).log" -Tail 50
```

### 12.2 HTTP 502.5 — ANCM Out-Of-Process Startup Failure

**Causa:** La aplicación .NET falla al iniciarse.

```powershell
# Probar ejecutar la app directamente (sin IIS) para ver el error real
cd "C:\SanitasField\api"
$env:ASPNETCORE_ENVIRONMENT = "Production"
.\SanitasField.Api.exe
# El error aparecerá en la consola
```

**Causas comunes:**
- Connection string incorrecto → PostgreSQL no acepta conexión
- JWT Key muy corta (< 32 caracteres)
- Carpeta `uploads` o `logs` sin permisos de escritura

### 12.3 Problemas de conexión a PostgreSQL

```powershell
# Test de conectividad básica
Test-NetConnection -ComputerName localhost -Port 5432

# Verificar que PostgreSQL escucha en 5432
netstat -ano | findstr ":5432"

# Test de login con credenciales de app
$env:PGPASSWORD = "TU_PASSWORD_APP"
& "C:\PostgreSQL\16\bin\psql.exe" -U sanitasfield -h 127.0.0.1 -d sanitasfield -c "\conninfo"
Remove-Item Env:\PGPASSWORD

# Si falla: verificar pg_hba.conf
notepad "C:\PostgreSQL\16\data\pg_hba.conf"
```

### 12.4 Error "password authentication failed for user sanitasfield"

```powershell
# Resetear contraseña del usuario de aplicación
$env:PGPASSWORD = "SuperAdmin_PG_2024!"
& "C:\PostgreSQL\16\bin\psql.exe" -U postgres -h localhost -c `
    "ALTER USER sanitasfield WITH PASSWORD 'NUEVA_PASSWORD';"
Remove-Item Env:\PGPASSWORD

# Actualizar la variable de entorno del AppPool
Set-AppPoolEnv "SanitasField-API" "ConnectionStrings__Default" `
    "Host=localhost;Port=5432;Database=sanitasfield;Username=sanitasfield;Password=NUEVA_PASSWORD;Search Path=sf,public"

# Reiniciar AppPool
Restart-WebAppPool "SanitasField-API"
```

### 12.5 AppPool se detiene inmediatamente

```powershell
# Ver eventos de Windows relacionados
Get-EventLog -LogName Application -Source "IIS*","ASP*","W3SVC*" -Newest 20 |
    Format-Table TimeGenerated, EntryType, Message -Wrap

# Habilitar stdout logging temporalmente (solo para diagnóstico)
# Editar C:\SanitasField\api\web.config y cambiar:
# stdoutLogEnabled="false" → stdoutLogEnabled="true"
# Los logs aparecerán en C:\SanitasField\api\logs\
```

### 12.6 Error en scripts SQL

```powershell
# Re-ejecutar un script con output detallado
$env:PGPASSWORD = "SuperAdmin_PG_2024!"
& "C:\PostgreSQL\16\bin\psql.exe" -U postgres -h localhost -d sanitasfield `
    -v ON_ERROR_STOP=1 -f "C:\SanitasField\scripts\01_schema.sql" 2>&1 |
    Tee-Object -FilePath "C:\SanitasField\logs\sql_error.log"
Remove-Item Env:\PGPASSWORD
```

---

## 13. ROLLBACK

### 13.1 Rollback de aplicación (a versión anterior)

```powershell
# Detener AppPools
Stop-WebAppPool "SanitasField-API"
Stop-WebAppPool "SanitasField-Web"

# Restaurar backup anterior (debe existir en C:\SanitasField\backups\)
$fechaBackup = "20240101_120000"  # Ajustar a fecha del último backup bueno
robocopy "C:\SanitasField\backups\api_$fechaBackup" "C:\SanitasField\api" /MIR
robocopy "C:\SanitasField\backups\web_$fechaBackup" "C:\SanitasField\web" /MIR

# Reiniciar
Start-WebAppPool "SanitasField-API"
Start-WebAppPool "SanitasField-Web"
```

### 13.2 Backup previo a despliegue (hacer siempre antes)

```powershell
$fecha = Get-Date -Format "yyyyMMdd_HHmmss"

# Backup de aplicación
if (Test-Path "C:\SanitasField\api") {
    Copy-Item "C:\SanitasField\api" "C:\SanitasField\backups\api_$fecha" -Recurse
}
if (Test-Path "C:\SanitasField\web") {
    Copy-Item "C:\SanitasField\web" "C:\SanitasField\backups\web_$fecha" -Recurse
}

# Backup de base de datos
$env:PGPASSWORD = "SuperAdmin_PG_2024!"
& "C:\PostgreSQL\16\bin\pg_dump.exe" -U postgres -h localhost `
    -Fc sanitasfield > "C:\SanitasField\backups\db_$fecha.dump"
Remove-Item Env:\PGPASSWORD
Write-Host "Backup completo en: C:\SanitasField\backups\*_$fecha"
```

### 13.3 Rollback de base de datos

```powershell
# PELIGROSO: Elimina datos actuales. Solo si el rollback de BD es necesario.
$fechaBackup = "20240101_120000"
$env:PGPASSWORD = "SuperAdmin_PG_2024!"

# Desconectar usuarios activos
& "C:\PostgreSQL\16\bin\psql.exe" -U postgres -h localhost -c `
    "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='sanitasfield' AND pid <> pg_backend_pid();"

# Restaurar
& "C:\PostgreSQL\16\bin\pg_restore.exe" -U postgres -h localhost `
    -d sanitasfield -c "C:\SanitasField\backups\db_$fechaBackup.dump"
Remove-Item Env:\PGPASSWORD
Write-Host "Base de datos restaurada al backup $fechaBackup"
```

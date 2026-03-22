#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Script de despliegue completo de SgiForm en Windows Server + IIS + PostgreSQL.

.DESCRIPTION
    Automatiza el despliegue de:
    - API REST (ASP.NET Core 8)
    - Web Admin (Blazor Server)
    Asume que PostgreSQL ya está instalado. Usa variables de entorno para secretos.

.PARAMETER AppBasePath
    Ruta base de instalación. Default: C:\SgiForm

.PARAMETER ApiPublishPath
    Ruta donde está la publicación de la API (dotnet publish output).

.PARAMETER WebPublishPath
    Ruta donde está la publicación de la Web (dotnet publish output).

.PARAMETER ApiPort
    Puerto IIS para la API. Default: 5043

.PARAMETER WebPort
    Puerto IIS para la Web. Default: 80

.PARAMETER PgHost
    Host de PostgreSQL. Default: localhost

.PARAMETER PgPort
    Puerto de PostgreSQL. Default: 5432

.PARAMETER PgDatabase
    Nombre de la base de datos. Default: sgiform

.PARAMETER PgAppUser
    Usuario de aplicación en PostgreSQL. Default: sgiform

.PARAMETER PgSaUser
    Superusuario de PostgreSQL. Default: postgres

.PARAMETER RunSqlScripts
    Si se deben ejecutar los scripts SQL. Solo en primera instalación.

.PARAMETER SourceRepoPath
    Ruta del repositorio para construir desde fuente. Si se omite, usa ApiPublishPath/WebPublishPath directamente.

.EXAMPLE
    # Despliegue desde publicación previa
    .\deploy-server.ps1 `
        -ApiPublishPath "C:\publish\api" `
        -WebPublishPath "C:\publish\web" `
        -RunSqlScripts:$false

.EXAMPLE
    # Primera instalación con scripts SQL
    .\deploy-server.ps1 `
        -ApiPublishPath "C:\publish\api" `
        -WebPublishPath "C:\publish\web" `
        -RunSqlScripts:$true
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$AppBasePath    = "C:\SgiForm",
    [string]$ApiPublishPath = "",
    [string]$WebPublishPath = "",
    [int]   $ApiPort        = 5043,
    [int]   $WebPort        = 80,
    [string]$PgHost         = "localhost",
    [int]   $PgPort         = 5432,
    [string]$PgDatabase     = "sgiform",
    [string]$PgAppUser      = "sgiform",
    [string]$PgSaUser       = "postgres",
    [switch]$RunSqlScripts  = $false,
    [string]$SourceRepoPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ─── Colores y logging ────────────────────────────────────────────────────────
function Write-Step  { param([string]$msg) Write-Host "`n[PASO] $msg" -ForegroundColor Cyan }
function Write-Ok    { param([string]$msg) Write-Host "  [OK] $msg"   -ForegroundColor Green }
function Write-Warn  { param([string]$msg) Write-Host "  [!]  $msg"   -ForegroundColor Yellow }
function Write-Err   { param([string]$msg) Write-Host "  [X]  $msg"   -ForegroundColor Red }
function Write-Info  { param([string]$msg) Write-Host "       $msg"   -ForegroundColor Gray }

# ─── Variables derivadas ──────────────────────────────────────────────────────
$ApiDestPath    = "$AppBasePath\api"
$WebDestPath    = "$AppBasePath\web"
$UploadsPath    = "$AppBasePath\uploads"
$LogsPath       = "$AppBasePath\logs"
$BackupsPath    = "$AppBasePath\backups"
$ScriptsPath    = "$AppBasePath\scripts"
$ApiPoolName    = "SgiForm-API"
$WebPoolName    = "SgiForm-Web"
$ApiSiteName    = "SgiForm-API"
$WebSiteName    = "SgiForm-Web"
$PgBin          = "C:\PostgreSQL\16\bin"
$Timestamp      = Get-Date -Format "yyyyMMdd_HHmmss"

# ─── Funciones de prerequisito ───────────────────────────────────────────────

function Test-Administrator {
    $current = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($current)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-IISInstalled {
    return (Get-Service W3SVC -ErrorAction SilentlyContinue) -ne $null
}

function Test-DotNetRuntime {
    try {
        $ver = & dotnet --version 2>&1
        return $ver -match "^8\."
    } catch { return $false }
}

function Test-PostgreSQL {
    return (Test-Path "$PgBin\psql.exe") -and
           ((Get-Service "postgresql-16" -ErrorAction SilentlyContinue)?.Status -eq "Running")
}

function Test-ANCMModule {
    $mod = Get-WebConfiguration "system.webServer/globalModules/add[@name='AspNetCoreModuleV2']" -ErrorAction SilentlyContinue
    return $mod -ne $null
}

function Get-SecureInput {
    param([string]$Prompt)
    $ss = Read-Host -Prompt $Prompt -AsSecureString
    return [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($ss))
}

function Set-AppPoolEnvVar {
    param([string]$PoolName, [string]$VarName, [string]$VarValue)
    $pool = Get-Item "IIS:\AppPools\$PoolName" -ErrorAction SilentlyContinue
    if (-not $pool) { throw "AppPool '$PoolName' no existe" }

    $existing = $pool.environmentVariables | Where-Object { $_.name -eq $VarName }
    if ($existing) { $pool.environmentVariables.Remove($existing) }

    $envEntry = $pool.environmentVariables.CreateElement("add")
    $envEntry.name  = $VarName
    $envEntry.value = $VarValue
    $pool.environmentVariables.Add($envEntry)
    $pool | Set-Item
}

function Set-FolderPermission {
    param([string]$Path, [string]$Identity, [string]$Rights = "ReadAndExecute")
    $acl  = Get-Acl $Path
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $Identity, $Rights,
        "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($rule)
    Set-Acl $Path $acl
}

# ═══════════════════════════════════════════════════════════════════════════════
# INICIO DEL SCRIPT
# ═══════════════════════════════════════════════════════════════════════════════

Write-Host "`n╔══════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host   "║    SgiForm — Script de Despliegue v1.0     ║" -ForegroundColor Cyan
Write-Host   "╚══════════════════════════════════════════════════╝`n" -ForegroundColor Cyan
Write-Info "Timestamp: $Timestamp"
Write-Info "Base path: $AppBasePath"
Write-Info "API Port:  $ApiPort"
Write-Info "Web Port:  $WebPort"

# ─── PASO 0: Verificar prerequisitos ─────────────────────────────────────────
Write-Step "Verificando prerequisitos"

if (-not (Test-Administrator)) {
    Write-Err "Este script debe ejecutarse como Administrador."
    exit 1
}
Write-Ok "Ejecutando como Administrador"

if (-not (Test-IISInstalled)) {
    Write-Err "IIS no está instalado. Instalar con Enable-WindowsOptionalFeature."
    exit 1
}
Write-Ok "IIS instalado"

if (-not (Test-DotNetRuntime)) {
    Write-Warn ".NET 8 no detectado. Intentando instalar Hosting Bundle..."
    $hbUrl  = "https://download.visualstudio.microsoft.com/download/pr/dotnet-hosting-8.0-win.exe"
    $hbPath = "$AppBasePath\dotnet-hosting-8.0-win.exe"
    Invoke-WebRequest -Uri $hbUrl -OutFile $hbPath -UseBasicParsing
    Start-Process -FilePath $hbPath -ArgumentList "/quiet /norestart OPT_NO_X86=1" -Wait
    net stop was /y | Out-Null
    net start w3svc | Out-Null
    Write-Ok ".NET 8 Hosting Bundle instalado"
} else {
    Write-Ok ".NET 8 Hosting Bundle presente"
}

if (-not (Test-ANCMModule)) {
    Write-Warn "Módulo ANCM no detectado. Reiniciando IIS..."
    net stop was /y | Out-Null; net start w3svc | Out-Null
    if (-not (Test-ANCMModule)) {
        Write-Err "ANCM no se instaló. Verificar Hosting Bundle manualmente."
        exit 1
    }
}
Write-Ok "Módulo ASP.NET Core (ANCM v2) presente"

if (-not (Test-PostgreSQL)) {
    Write-Err "PostgreSQL 16 no está instalado o su servicio no está corriendo."
    Write-Info "Instalar con el script de instalación manual o el instalador de EnterpriseDB."
    exit 1
}
Write-Ok "PostgreSQL 16 activo"

Import-Module WebAdministration -ErrorAction Stop
Write-Ok "Módulo WebAdministration cargado"

# ─── PASO 1: Crear estructura de carpetas ────────────────────────────────────
Write-Step "Creando estructura de carpetas"

foreach ($dir in @($ApiDestPath, $WebDestPath, $UploadsPath, $LogsPath, $BackupsPath, $ScriptsPath)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Write-Info "  $dir"
}
Write-Ok "Carpetas creadas"

# ─── PASO 2: Backup de versión anterior ─────────────────────────────────────
Write-Step "Haciendo backup de versión anterior"

if ((Get-ChildItem $ApiDestPath -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0) {
    Copy-Item $ApiDestPath "$BackupsPath\api_$Timestamp" -Recurse -Force
    Write-Ok "Backup API: $BackupsPath\api_$Timestamp"
} else {
    Write-Info "Primera instalación — sin backup de API"
}

if ((Get-ChildItem $WebDestPath -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0) {
    Copy-Item $WebDestPath "$BackupsPath\web_$Timestamp" -Recurse -Force
    Write-Ok "Backup Web: $BackupsPath\web_$Timestamp"
} else {
    Write-Info "Primera instalación — sin backup de Web"
}

# ─── PASO 3: Build desde fuente (opcional) ───────────────────────────────────
if ($SourceRepoPath -ne "") {
    Write-Step "Construyendo desde repositorio: $SourceRepoPath"

    Push-Location $SourceRepoPath

    Write-Info "Restaurando dependencias..."
    dotnet restore SgiForm.sln
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore falló" }

    Write-Info "Compilando en Release..."
    dotnet build SgiForm.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet build falló" }

    Write-Info "Ejecutando tests..."
    dotnet test tests/SgiForm.Tests/SgiForm.Tests.csproj -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "Tests fallaron. Despliegue cancelado." }
    Write-Ok "Tests pasados"

    Write-Info "Publicando API..."
    dotnet publish src/SgiForm.Api/SgiForm.Api.csproj `
        -c Release -o "$env:TEMP\sf_publish\api" --no-build
    $ApiPublishPath = "$env:TEMP\sf_publish\api"

    Write-Info "Publicando Web..."
    dotnet publish src/SgiForm.Web/SgiForm.Web.csproj `
        -c Release -o "$env:TEMP\sf_publish\web" --no-build
    $WebPublishPath = "$env:TEMP\sf_publish\web"

    Pop-Location
    Write-Ok "Build completado"
}

# ─── PASO 4: Detener AppPools antes de copiar ────────────────────────────────
Write-Step "Deteniendo AppPools para despliegue"

$appcmd = "$env:windir\system32\inetsrv\appcmd.exe"
foreach ($pool in @($ApiPoolName, $WebPoolName)) {
    if (Get-WebAppPoolState -Name $pool -ErrorAction SilentlyContinue) {
        & $appcmd stop apppool /apppool.name:"$pool" | Out-Null
        Write-Info "Detenido: $pool"
    }
}
Start-Sleep -Seconds 3

# ─── PASO 5: Copiar archivos publicados ─────────────────────────────────────
Write-Step "Desplegando archivos de aplicación"

if ($ApiPublishPath -ne "") {
    if (-not (Test-Path $ApiPublishPath)) { throw "ApiPublishPath no existe: $ApiPublishPath" }
    robocopy $ApiPublishPath $ApiDestPath /MIR /R:3 /W:5 /NFL /NDL /NP /NJH /NJS
    if ($LASTEXITCODE -gt 7) { throw "Robocopy API falló (código $LASTEXITCODE)" }
    Write-Ok "API desplegada en $ApiDestPath"
} else {
    Write-Warn "ApiPublishPath no especificado — se omite despliegue de API"
}

if ($WebPublishPath -ne "") {
    if (-not (Test-Path $WebPublishPath)) { throw "WebPublishPath no existe: $WebPublishPath" }
    robocopy $WebPublishPath $WebDestPath /MIR /R:3 /W:5 /NFL /NDL /NP /NJH /NJS
    if ($LASTEXITCODE -gt 7) { throw "Robocopy Web falló (código $LASTEXITCODE)" }
    Write-Ok "Web desplegada en $WebDestPath"
} else {
    Write-Warn "WebPublishPath no especificado — se omite despliegue de Web"
}

# ─── PASO 6: Configurar IIS ──────────────────────────────────────────────────
Write-Step "Configurando IIS"

# AppPool API
if (-not (Test-Path "IIS:\AppPools\$ApiPoolName")) {
    New-WebAppPool -Name $ApiPoolName
    Write-Info "AppPool $ApiPoolName creado"
}
Set-ItemProperty "IIS:\AppPools\$ApiPoolName" managedRuntimeVersion ""
Set-ItemProperty "IIS:\AppPools\$ApiPoolName" startMode "AlwaysRunning"
Set-ItemProperty "IIS:\AppPools\$ApiPoolName" processModel.idleTimeout "00:00:00"
Set-ItemProperty "IIS:\AppPools\$ApiPoolName" recycling.periodicRestart.time "00:00:00"
Write-Ok "AppPool $ApiPoolName configurado (No Managed Code)"

# AppPool Web
if (-not (Test-Path "IIS:\AppPools\$WebPoolName")) {
    New-WebAppPool -Name $WebPoolName
    Write-Info "AppPool $WebPoolName creado"
}
Set-ItemProperty "IIS:\AppPools\$WebPoolName" managedRuntimeVersion ""
Set-ItemProperty "IIS:\AppPools\$WebPoolName" startMode "AlwaysRunning"
Set-ItemProperty "IIS:\AppPools\$WebPoolName" processModel.idleTimeout "00:00:00"
Set-ItemProperty "IIS:\AppPools\$WebPoolName" recycling.periodicRestart.time "00:00:00"
Write-Ok "AppPool $WebPoolName configurado (No Managed Code)"

# Sitio API
if (-not (Test-Path "IIS:\Sites\$ApiSiteName")) {
    New-Website -Name $ApiSiteName `
        -PhysicalPath $ApiDestPath `
        -ApplicationPool $ApiPoolName `
        -Port $ApiPort -Force
    Write-Ok "Sitio $ApiSiteName creado en puerto $ApiPort"
} else {
    Set-ItemProperty "IIS:\Sites\$ApiSiteName" physicalPath $ApiDestPath
    Write-Info "Sitio $ApiSiteName ya existe — path actualizado"
}

# Sitio Web
if (-not (Test-Path "IIS:\Sites\$WebSiteName")) {
    New-Website -Name $WebSiteName `
        -PhysicalPath $WebDestPath `
        -ApplicationPool $WebPoolName `
        -Port $WebPort -Force
    Write-Ok "Sitio $WebSiteName creado en puerto $WebPort"
} else {
    Set-ItemProperty "IIS:\Sites\$WebSiteName" physicalPath $WebDestPath
    Write-Info "Sitio $WebSiteName ya existe — path actualizado"
}

# ─── PASO 7: Permisos de carpetas ────────────────────────────────────────────
Write-Step "Configurando permisos de carpetas"

Set-FolderPermission $ApiDestPath "IIS AppPool\$ApiPoolName" "ReadAndExecute"
Set-FolderPermission $WebDestPath "IIS AppPool\$WebPoolName" "ReadAndExecute"
Set-FolderPermission $UploadsPath "IIS AppPool\$ApiPoolName" "Modify"
Set-FolderPermission $LogsPath    "IIS AppPool\$ApiPoolName" "Modify"
Write-Ok "Permisos configurados"

# ─── PASO 8: Variables de entorno (solicitar secretos interactivamente) ───────
Write-Step "Configurando variables de entorno"

Write-Host "`n  Se solicitarán las credenciales de forma segura." -ForegroundColor Yellow
Write-Host "  Los valores NO se muestran en pantalla.`n" -ForegroundColor Yellow

# Connection string
$pgAppPwd    = Get-SecureInput "Contraseña PostgreSQL usuario '$PgAppUser'"
$connString  = "Host=$PgHost;Port=$PgPort;Database=$PgDatabase;Username=$PgAppUser;Password=$pgAppPwd;Search Path=sf,public"

# JWT Key
Write-Host ""
$jwtKey = Get-SecureInput "JWT Key (mínimo 64 caracteres, Enter para generar automáticamente)"
if ([string]::IsNullOrWhiteSpace($jwtKey) -or $jwtKey.Length -lt 32) {
    $bytes  = New-Object byte[] 64
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $jwtKey = [Convert]::ToBase64String($bytes)
    Write-Warn "JWT Key generada automáticamente (guardar en gestor de secretos):"
    Write-Host "  $jwtKey" -ForegroundColor Yellow
}

# Aplicar variables al AppPool API
$apiVars = @{
    "ASPNETCORE_ENVIRONMENT"    = "Production"
    "ConnectionStrings__Default" = $connString
    "Jwt__Key"                   = $jwtKey
    "Jwt__Issuer"                = "SgiForm"
    "Jwt__Audience"              = "SgiForm"
    "Jwt__ExpirationMinutes"     = "60"
    "Storage__UploadPath"        = $UploadsPath
    "Storage__MaxPhotoMb"        = "10"
}

foreach ($kv in $apiVars.GetEnumerator()) {
    Set-AppPoolEnvVar $ApiPoolName $kv.Key $kv.Value
}
Write-Ok "Variables de entorno API configuradas"

# Aplicar variables al AppPool Web
Set-AppPoolEnvVar $WebPoolName "ASPNETCORE_ENVIRONMENT" "Production"
Set-AppPoolEnvVar $WebPoolName "ApiBaseUrl" "http://localhost:$ApiPort"
Write-Ok "Variables de entorno Web configuradas"

# Limpiar de memoria
$pgAppPwd = $null; $jwtKey = $null; $connString = $null

# ─── PASO 9: Ejecutar scripts SQL (solo primera vez) ─────────────────────────
if ($RunSqlScripts) {
    Write-Step "Ejecutando scripts SQL en PostgreSQL"

    $pgSaPwd = Get-SecureInput "Contraseña del superusuario PostgreSQL ('$PgSaUser')"
    $env:PGPASSWORD = $pgSaPwd

    $scripts = @(
        "$ScriptsPath\01_schema.sql",
        "$ScriptsPath\02_seed.sql",
        "$ScriptsPath\03_operador_refresh_token.sql"
    )

    foreach ($script in $scripts) {
        if (-not (Test-Path $script)) {
            Write-Warn "Script no encontrado: $script — omitido"
            continue
        }
        $name = Split-Path $script -Leaf
        Write-Info "Ejecutando $name..."
        & "$PgBin\psql.exe" -U $PgSaUser -h $PgHost -d $PgDatabase `
            -v ON_ERROR_STOP=1 -f $script 2>&1 | Tee-Object "$LogsPath\sql_${name}_$Timestamp.log"
        if ($LASTEXITCODE -ne 0) {
            Remove-Item Env:\PGPASSWORD
            throw "ERROR en $name (código $LASTEXITCODE). Ver log: $LogsPath\sql_${name}_$Timestamp.log"
        }
        Write-Ok "$name ejecutado"
    }

    Remove-Item Env:\PGPASSWORD
    $pgSaPwd = $null
} else {
    Write-Info "RunSqlScripts=$RunSqlScripts — scripts SQL omitidos"
}

# ─── PASO 10: Iniciar AppPools y sitios ──────────────────────────────────────
Write-Step "Iniciando AppPools y sitios IIS"

foreach ($pool in @($ApiPoolName, $WebPoolName)) {
    & $appcmd start apppool /apppool.name:"$pool" | Out-Null
    Write-Ok "$pool iniciado"
}

Start-Sleep -Seconds 5

foreach ($site in @($ApiSiteName, $WebSiteName)) {
    & $appcmd start site /site.name:"$site" | Out-Null
    Write-Ok "$site iniciado"
}

# ─── PASO 11: Validación post-despliegue ────────────────────────────────────
Write-Step "Validando despliegue"

Start-Sleep -Seconds 5

# Health check API
try {
    $r = Invoke-WebRequest -Uri "http://localhost:$ApiPort/health" -UseBasicParsing -TimeoutSec 15
    Write-Ok "API Health check: HTTP $($r.StatusCode)"
} catch {
    Write-Warn "API no responde en /health: $($_.Exception.Message)"
    Write-Info "Revisar logs en: $LogsPath"
}

# Web Blazor
try {
    $r = Invoke-WebRequest -Uri "http://localhost:$WebPort" -UseBasicParsing -TimeoutSec 15
    Write-Ok "Web Blazor: HTTP $($r.StatusCode)"
} catch {
    Write-Warn "Web no responde: $($_.Exception.Message)"
}

# Estado AppPools
Write-Info ""
Get-ChildItem "IIS:\AppPools" |
    Where-Object { $_.Name -match "SgiForm" } |
    Select-Object Name, State |
    Format-Table -AutoSize

# ─── RESUMEN FINAL ────────────────────────────────────────────────────────────
Write-Host "`n╔══════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host   "║           DESPLIEGUE COMPLETADO                 ║" -ForegroundColor Green
Write-Host   "╚══════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "  API   → http://localhost:$ApiPort/api/v1/" -ForegroundColor Cyan
Write-Host "  Web   → http://localhost:$WebPort"         -ForegroundColor Cyan
Write-Host "  Logs  → $LogsPath"                         -ForegroundColor Gray
Write-Host ""
Write-Host "  Backup anterior guardado en:"              -ForegroundColor Gray
Write-Host "  $BackupsPath\*_$Timestamp"                 -ForegroundColor Gray
Write-Host ""

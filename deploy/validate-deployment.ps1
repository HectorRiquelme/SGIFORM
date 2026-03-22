#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Valida que el despliegue de SgiForm esté operativo.
    Ejecutar después de deploy-server.ps1 o ante cualquier duda sobre el estado.
#>

param(
    [int]   $ApiPort  = 5043,
    [int]   $WebPort  = 80,
    [string]$PgHost   = "localhost",
    [int]   $PgPort   = 5432,
    [string]$PgDb     = "sgiform",
    [string]$PgUser   = "sgiform"
)

$ok    = 0
$warn  = 0
$fail  = 0

function Pass  { param([string]$t) Write-Host "  [PASS] $t" -ForegroundColor Green;  $script:ok++  }
function Warn  { param([string]$t) Write-Host "  [WARN] $t" -ForegroundColor Yellow; $script:warn++ }
function Fail  { param([string]$t) Write-Host "  [FAIL] $t" -ForegroundColor Red;   $script:fail++ }
function Title { param([string]$t) Write-Host "`n── $t ──" -ForegroundColor Cyan }

Write-Host "`n╔══════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host   "║   SgiForm — Validación de Despliegue  ║" -ForegroundColor Cyan
Write-Host   "╚══════════════════════════════════════════╝`n" -ForegroundColor Cyan

# ─── Servicios del sistema ───────────────────────────────────────────────────
Title "SERVICIOS"

$iis = Get-Service W3SVC -ErrorAction SilentlyContinue
if ($iis?.Status -eq "Running") { Pass "IIS (W3SVC) corriendo" }
else { Fail "IIS (W3SVC) no está corriendo" }

$pg = Get-Service "postgresql-16" -ErrorAction SilentlyContinue
if ($pg?.Status -eq "Running") { Pass "PostgreSQL 16 corriendo" }
else { Fail "PostgreSQL 16 no está corriendo" }

# ─── AppPools IIS ─────────────────────────────────────────────────────────────
Title "APPLICATION POOLS"
Import-Module WebAdministration -ErrorAction SilentlyContinue

foreach ($pool in @("SgiForm-API", "SgiForm-Web")) {
    try {
        $state = (Get-WebAppPoolState -Name $pool -ErrorAction Stop).Value
        if ($state -eq "Started") { Pass "AppPool '$pool' = Started" }
        else { Fail "AppPool '$pool' = $state (esperado: Started)" }
    } catch {
        Fail "AppPool '$pool' no existe"
    }
}

# ─── Sitios IIS ──────────────────────────────────────────────────────────────
Title "SITIOS IIS"

foreach ($site in @("SgiForm-API", "SgiForm-Web")) {
    $s = Get-Website -Name $site -ErrorAction SilentlyContinue
    if ($s -and $s.State -eq "Started") { Pass "Sitio '$site' = Started" }
    elseif ($s) { Fail "Sitio '$site' = $($s.State)" }
    else { Fail "Sitio '$site' no existe" }
}

# ─── Archivos de aplicación ──────────────────────────────────────────────────
Title "ARCHIVOS"

$criticalFiles = @(
    "C:\SgiForm\api\SgiForm.Api.exe",
    "C:\SgiForm\api\web.config",
    "C:\SgiForm\api\appsettings.json",
    "C:\SgiForm\web\SgiForm.Web.exe",
    "C:\SgiForm\web\web.config"
)
foreach ($f in $criticalFiles) {
    if (Test-Path $f) { Pass "Existe: $(Split-Path $f -Leaf)" }
    else { Fail "Falta:  $f" }
}

# Carpetas con escritura
foreach ($dir in @("C:\SgiForm\uploads", "C:\SgiForm\logs")) {
    $testFile = "$dir\write_test_$([Guid]::NewGuid()).tmp"
    try {
        [IO.File]::WriteAllText($testFile, "test")
        Remove-Item $testFile -Force
        Pass "Escritura OK en: $dir"
    } catch {
        Fail "Sin permisos de escritura en: $dir"
    }
}

# ─── Conectividad de red ─────────────────────────────────────────────────────
Title "PUERTOS"

foreach ($check in @(
    @{ Host=$PgHost; Port=$PgPort; Name="PostgreSQL" },
    @{ Host="localhost"; Port=$ApiPort; Name="API (IIS)" },
    @{ Host="localhost"; Port=$WebPort; Name="Web (IIS)" }
)) {
    $tcp = Test-NetConnection -ComputerName $check.Host -Port $check.Port -WarningAction SilentlyContinue
    if ($tcp.TcpTestSucceeded) { Pass "Puerto $($check.Port) abierto ($($check.Name))" }
    else { Fail "Puerto $($check.Port) cerrado ($($check.Name))" }
}

# ─── Health check HTTP ───────────────────────────────────────────────────────
Title "ENDPOINTS HTTP"

try {
    $r = Invoke-WebRequest -Uri "http://localhost:$ApiPort/health" -UseBasicParsing -TimeoutSec 10
    if ($r.StatusCode -eq 200) { Pass "GET /health → HTTP 200 ($($r.Content))" }
    else { Warn "GET /health → HTTP $($r.StatusCode)" }
} catch {
    Fail "GET /health → $($_.Exception.Message)"
}

try {
    $r = Invoke-WebRequest -Uri "http://localhost:$WebPort" -UseBasicParsing -TimeoutSec 10
    if ($r.StatusCode -eq 200) { Pass "GET / (Web) → HTTP 200" }
    else { Warn "GET / (Web) → HTTP $($r.StatusCode)" }
} catch {
    Fail "GET / (Web) → $($_.Exception.Message)"
}

# Swagger (solo en Development — en Production debe retornar 404)
try {
    $r = Invoke-WebRequest -Uri "http://localhost:$ApiPort/swagger" -UseBasicParsing -TimeoutSec 5
    Warn "Swagger accesible en producción (HTTP $($r.StatusCode)) — considerar desactivar"
} catch {
    if ($_.Exception.Response?.StatusCode -eq 404) {
        Pass "Swagger desactivado en Production (HTTP 404) — correcto"
    }
}

# ─── Base de datos ───────────────────────────────────────────────────────────
Title "BASE DE DATOS POSTGRESQL"

$pgBin = "C:\PostgreSQL\16\bin\psql.exe"
if (-not (Test-Path $pgBin)) {
    Fail "psql.exe no encontrado en C:\PostgreSQL\16\bin\"
} else {
    $pgPwd = Read-Host "Contraseña del usuario '$PgUser' (Enter para omitir validación BD)" -AsSecureString
    $pwd   = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
                [Runtime.InteropServices.Marshal]::SecureStringToBSTR($pgPwd))

    if (-not [string]::IsNullOrWhiteSpace($pwd)) {
        $env:PGPASSWORD = $pwd

        # Conectividad
        $result = & $pgBin -U $PgUser -h $PgHost -d $PgDb -c "\conninfo" 2>&1
        if ($LASTEXITCODE -eq 0) {
            Pass "Conexión PostgreSQL: usuario '$PgUser' en '$PgDb'"
        } else {
            Fail "Conexión PostgreSQL falló: $result"
        }

        # Contar tablas del schema sf
        $count = & $pgBin -U $PgUser -h $PgHost -d $PgDb -t -c `
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='sf';" 2>&1
        if ($LASTEXITCODE -eq 0) {
            $n = $count.Trim()
            if ([int]$n -ge 20) { Pass "Schema 'sf' contiene $n tablas (esperado ≥ 20)" }
            else { Warn "Schema 'sf' solo tiene $n tablas — verificar scripts SQL" }
        }

        # Verificar migración operador refresh token
        $col = & $pgBin -U $PgUser -h $PgHost -d $PgDb -t -c `
            "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='sf' AND table_name='refresh_token' AND column_name='operador_id';" 2>&1
        if ($col.Trim() -eq "1") { Pass "Migración 03 aplicada (columna operador_id existe)" }
        else { Fail "Migración 03 NO aplicada — falta columna operador_id en refresh_token" }

        Remove-Item Env:\PGPASSWORD
        $pwd = $null
    } else {
        Warn "Validación de BD omitida por usuario"
    }
}

# ─── Variables de entorno del AppPool ────────────────────────────────────────
Title "VARIABLES DE ENTORNO (AppPool API)"

try {
    $pool = Get-Item "IIS:\AppPools\SgiForm-API" -ErrorAction Stop
    $vars = $pool.environmentVariables | Select-Object name, value
    $required = @("ASPNETCORE_ENVIRONMENT", "ConnectionStrings__Default", "Jwt__Key", "Storage__UploadPath")
    foreach ($req in $required) {
        $found = $vars | Where-Object { $_.name -eq $req }
        if ($found) {
            $val = if ($req -match "Key|Password|Secret") { "***" } else { $found.value }
            Pass "Variable '$req' = $val"
        } else {
            Fail "Variable '$req' NO configurada en AppPool"
        }
    }
} catch {
    Fail "No se pudo leer variables del AppPool: $($_.Exception.Message)"
}

# ─── Resumen ─────────────────────────────────────────────────────────────────
Write-Host "`n══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  RESULTADO DE VALIDACIÓN" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  PASS : $ok"   -ForegroundColor Green
Write-Host "  WARN : $warn" -ForegroundColor Yellow
Write-Host "  FAIL : $fail" -ForegroundColor Red
Write-Host ""

if ($fail -eq 0 -and $warn -eq 0) {
    Write-Host "  ESTADO: PRODUCCIÓN LISTA ✓" -ForegroundColor Green
} elseif ($fail -eq 0) {
    Write-Host "  ESTADO: OPERATIVO CON ADVERTENCIAS — revisar WARNs" -ForegroundColor Yellow
} else {
    Write-Host "  ESTADO: ERRORES CRÍTICOS — corregir antes de usar en producción" -ForegroundColor Red
}
Write-Host ""

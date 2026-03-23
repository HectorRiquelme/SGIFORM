<#
.SYNOPSIS
    Rollback del despliegue de SGI-FORM en IIS.

.DESCRIPTION
    Permite revertir el despliegue de SgiFormApi y SgiFormWeb a un backup previo,
    o eliminar completamente los sitios y AppPools de IIS.

.PARAMETER BackupPath
    Ruta base de los backups. Por defecto: C:\SgiForm\backup

.PARAMETER Mode
    Modo de rollback:
    - "restore" : Restaura desde backup (default)
    - "remove"  : Elimina sitios y AppPools completamente

.EXAMPLE
    .\rollback.ps1
    .\rollback.ps1 -Mode remove
    .\rollback.ps1 -BackupPath "D:\backups\sgiform"
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$BackupPath = "C:\SgiForm\backup",
    [ValidateSet("restore","remove")]
    [string]$Mode = "restore"
)

$ErrorActionPreference = "Stop"
Import-Module WebAdministration -ErrorAction SilentlyContinue

function Write-Step { param($msg) Write-Host "`n[$([datetime]::Now.ToString('HH:mm:ss'))] $msg" -ForegroundColor Cyan }
function Write-OK   { param($msg) Write-Host "  OK: $msg" -ForegroundColor Green }
function Write-Warn { param($msg) Write-Host "  WARN: $msg" -ForegroundColor Yellow }
function Write-Fail { param($msg) Write-Host "  FAIL: $msg" -ForegroundColor Red }

# ── Verificar admin ────────────────────────────────────────────────────────
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Fail "Ejecutar como Administrador."
    exit 1
}

Write-Host "========================================" -ForegroundColor Magenta
Write-Host "   SGI-FORM ROLLBACK — Modo: $Mode" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta

if ($Mode -eq "remove") {
    # ── Modo eliminar ──────────────────────────────────────────────────────
    Write-Step "Eliminando sitios IIS y AppPools..."
    $confirm = Read-Host "¿Eliminar COMPLETAMENTE SgiFormApi y SgiFormWeb? (s/N)"
    if ($confirm -ne "s") { Write-Warn "Cancelado."; exit 0 }

    foreach ($site in @("SgiFormApi","SgiFormWeb")) {
        if (Get-Website -Name $site -ErrorAction SilentlyContinue) {
            Stop-Website -Name $site -ErrorAction SilentlyContinue
            Remove-Website -Name $site
            Write-OK "Sitio $site eliminado."
        } else {
            Write-Warn "Sitio $site no existe."
        }
    }
    foreach ($pool in @("SgiFormApi","SgiFormWeb")) {
        if (Test-Path "IIS:\AppPools\$pool") {
            Stop-WebAppPool -Name $pool -ErrorAction SilentlyContinue
            Remove-WebAppPool -Name $pool
            Write-OK "AppPool $pool eliminado."
        } else {
            Write-Warn "AppPool $pool no existe."
        }
    }
    Write-Host "`nEliminación completada." -ForegroundColor Green
    exit 0
}

# ── Modo restore ───────────────────────────────────────────────────────────
Write-Step "Buscando backups disponibles en $BackupPath..."

if (-not (Test-Path $BackupPath)) {
    Write-Fail "No existe la carpeta de backup: $BackupPath"
    Write-Warn "Para crear un backup antes del deploy use deploy-server.ps1."
    exit 1
}

$backups = Get-ChildItem $BackupPath -Directory | Sort-Object Name -Descending
if ($backups.Count -eq 0) {
    Write-Fail "No hay backups en $BackupPath"
    exit 1
}

Write-Host "`nBackups disponibles:" -ForegroundColor Yellow
for ($i = 0; $i -lt [Math]::Min($backups.Count, 10); $i++) {
    Write-Host "  [$i] $($backups[$i].Name)"
}

$sel = Read-Host "`nSelecciona número de backup (Enter = más reciente [0])"
if ([string]::IsNullOrWhiteSpace($sel)) { $sel = "0" }
$selectedBackup = $backups[[int]$sel]
Write-Host "Seleccionado: $($selectedBackup.FullName)" -ForegroundColor Cyan

$apiBackup = Join-Path $selectedBackup.FullName "api"
$webBackup = Join-Path $selectedBackup.FullName "web"

if (-not (Test-Path $apiBackup) -and -not (Test-Path $webBackup)) {
    Write-Fail "El backup seleccionado no contiene carpetas 'api' o 'web'."
    exit 1
}

# ── Detener sitios ─────────────────────────────────────────────────────────
Write-Step "Deteniendo sitios IIS..."
foreach ($site in @("SgiFormApi","SgiFormWeb")) {
    if (Get-Website -Name $site -ErrorAction SilentlyContinue) {
        Stop-Website -Name $site -ErrorAction SilentlyContinue
        Stop-WebAppPool -Name $site -ErrorAction SilentlyContinue
        Write-OK "$site detenido."
    }
}
Start-Sleep -Seconds 3

# ── Restaurar API ──────────────────────────────────────────────────────────
if (Test-Path $apiBackup) {
    Write-Step "Restaurando API desde backup..."
    robocopy $apiBackup "C:\SgiForm\publish\api" /MIR /NFL /NDL /NJH /NJS | Out-Null
    Write-OK "API restaurada desde $apiBackup"
} else {
    Write-Warn "No hay backup de API en el backup seleccionado."
}

# ── Restaurar Web ──────────────────────────────────────────────────────────
if (Test-Path $webBackup) {
    Write-Step "Restaurando Web desde backup..."
    robocopy $webBackup "C:\SgiForm\publish\web" /MIR /NFL /NDL /NJH /NJS | Out-Null
    Write-OK "Web restaurada desde $webBackup"
} else {
    Write-Warn "No hay backup de Web en el backup seleccionado."
}

# ── Iniciar sitios ─────────────────────────────────────────────────────────
Write-Step "Iniciando sitios IIS..."
foreach ($site in @("SgiFormApi","SgiFormWeb")) {
    if (Get-Website -Name $site -ErrorAction SilentlyContinue) {
        Start-WebAppPool -Name $site -ErrorAction SilentlyContinue
        Start-Website -Name $site -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        $state = (Get-WebAppPoolState -Name $site -ErrorAction SilentlyContinue).Value
        Write-OK "$site iniciado — AppPool: $state"
    }
}

# ── Validación post-rollback ───────────────────────────────────────────────
Write-Step "Validando post-rollback..."
Start-Sleep -Seconds 5

try {
    $apiResponse = Invoke-WebRequest -Uri "http://localhost:5001/api/v1/auth/login" `
        -Method POST -ContentType "application/json" `
        -Body '{"email":"admin@sanitaria-demo.cl","password":"Admin@2024!"}' `
        -UseBasicParsing -TimeoutSec 10
    Write-OK "API respondiendo: HTTP $($apiResponse.StatusCode)"
} catch {
    Write-Warn "API no responde correctamente: $($_.Exception.Message)"
}

try {
    $webResponse = Invoke-WebRequest -Uri "http://localhost:8080" -UseBasicParsing -TimeoutSec 10
    Write-OK "Web respondiendo: HTTP $($webResponse.StatusCode)"
} catch {
    Write-Warn "Web no responde correctamente: $($_.Exception.Message)"
}

Write-Host "`n======================================" -ForegroundColor Magenta
Write-Host "   ROLLBACK COMPLETADO" -ForegroundColor Magenta
Write-Host "======================================" -ForegroundColor Magenta

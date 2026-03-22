#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Realiza rollback de SanitasField a un backup anterior.

.DESCRIPTION
    Detiene los AppPools IIS, restaura los archivos publicados desde
    un directorio de backup y reinicia los servicios. Luego ejecuta
    validate-deployment.ps1 para confirmar el estado del sistema.

.PARAMETER BackupPath
    Ruta al directorio de backup específico a restaurar.
    Si se omite, el script lista los backups disponibles y permite
    elegir interactivamente el más reciente.

.PARAMETER SkipValidation
    Omitir la ejecución de validate-deployment.ps1 al finalizar.

.PARAMETER WhatIf
    Simular el rollback sin realizar cambios reales.

.EXAMPLE
    .\rollback.ps1
    # Modo interactivo: lista backups y restaura el elegido

.EXAMPLE
    .\rollback.ps1 -BackupPath "C:\SanitasField\backups\20260322_143000"
    # Rollback directo a un backup específico

.EXAMPLE
    .\rollback.ps1 -WhatIf
    # Simular sin ejecutar cambios
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $BackupRoot   = "C:\SanitasField\backups",
    [string] $InstallRoot  = "C:\SanitasField",
    [string] $BackupPath   = "",
    [switch] $SkipValidation
)

$ErrorActionPreference = "Stop"

# ─── Funciones de salida ─────────────────────────────────────────────────────
function Info  { param([string]$t) Write-Host "  [INFO] $t" -ForegroundColor Cyan }
function Ok    { param([string]$t) Write-Host "  [ OK ] $t" -ForegroundColor Green }
function Warn  { param([string]$t) Write-Host "  [WARN] $t" -ForegroundColor Yellow }
function Err   { param([string]$t) Write-Host "  [ERRO] $t" -ForegroundColor Red }
function Title { param([string]$t) Write-Host "`n── $t ──" -ForegroundColor Magenta }

Write-Host "`n╔══════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host   "║      SanitasField — ROLLBACK             ║" -ForegroundColor Magenta
Write-Host   "╚══════════════════════════════════════════╝`n" -ForegroundColor Magenta

if ($WhatIfPreference) {
    Write-Host "  [MODO SIMULACION] Ningún cambio será aplicado.`n" -ForegroundColor Yellow
}

# ─── Verificar directorio de backups ─────────────────────────────────────────
Title "SELECCIÓN DE BACKUP"

if (-not (Test-Path $BackupRoot)) {
    Err "Directorio de backups no encontrado: $BackupRoot"
    Err "No hay backups disponibles para restaurar."
    exit 1
}

# ─── Seleccionar backup ───────────────────────────────────────────────────────
if ([string]::IsNullOrWhiteSpace($BackupPath)) {

    # Listar backups disponibles ordenados por fecha (más reciente primero)
    $backups = Get-ChildItem -Path $BackupRoot -Directory |
               Where-Object { $_.Name -match '^\d{8}_\d{6}$' } |
               Sort-Object Name -Descending

    if ($backups.Count -eq 0) {
        Err "No se encontraron backups en: $BackupRoot"
        Err "Los backups tienen el formato: YYYYMMDD_HHMMSS"
        exit 1
    }

    Write-Host "  Backups disponibles:" -ForegroundColor White
    $i = 0
    foreach ($b in $backups) {
        $marker = if ($i -eq 0) { " (más reciente)" } else { "" }
        Write-Host "  [$i] $($b.Name)$marker" -ForegroundColor $(if ($i -eq 0) {"Green"} else {"Gray"})
        $i++
    }

    Write-Host ""
    $choice = Read-Host "  Seleccionar backup [0 = más reciente, Enter = cancelar]"

    if ([string]::IsNullOrWhiteSpace($choice)) {
        Warn "Rollback cancelado por el usuario."
        exit 0
    }

    $idx = [int]$choice
    if ($idx -lt 0 -or $idx -ge $backups.Count) {
        Err "Opción inválida: $choice"
        exit 1
    }

    $BackupPath = $backups[$idx].FullName

} else {
    # Verificar que el backup especificado existe
    if (-not (Test-Path $BackupPath)) {
        Err "Backup no encontrado: $BackupPath"
        exit 1
    }
}

# Verificar que el backup contiene los subdirectorios esperados
$backupApi = Join-Path $BackupPath "api"
$backupWeb = Join-Path $BackupPath "web"

$hasApi = Test-Path $backupApi
$hasWeb = Test-Path $backupWeb

if (-not $hasApi -and -not $hasWeb) {
    Err "El backup seleccionado no contiene carpetas 'api' ni 'web': $BackupPath"
    exit 1
}

Write-Host ""
Ok "Backup seleccionado: $BackupPath"
if ($hasApi) { Info "  - Contiene: api\" }
if ($hasWeb)  { Info "  - Contiene: web\" }

# ─── Confirmación ────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "  ADVERTENCIA: Esta operación reemplazará los archivos de producción." -ForegroundColor Yellow
Write-Host "  Backup a restaurar: $BackupPath" -ForegroundColor Yellow
Write-Host ""
$confirm = Read-Host "  Confirmar rollback (s/N)"
if ($confirm -notmatch '^[sS]$') {
    Warn "Rollback cancelado por el usuario."
    exit 0
}

# ─── Cargar módulo IIS ────────────────────────────────────────────────────────
Import-Module WebAdministration -ErrorAction SilentlyContinue
$iisAvailable = $null -ne (Get-Module WebAdministration)

# ─── Detener AppPools ─────────────────────────────────────────────────────────
Title "DETENIENDO SERVICIOS"

$pools = @("SanitasField-API", "SanitasField-Web")
$stoppedPools = @()

foreach ($pool in $pools) {
    try {
        if ($iisAvailable) {
            $state = (Get-WebAppPoolState -Name $pool -ErrorAction SilentlyContinue)?.Value
            if ($state -eq "Started") {
                if ($PSCmdlet.ShouldProcess("AppPool $pool", "Stop")) {
                    Stop-WebAppPool -Name $pool
                    # Esperar hasta 15 segundos a que se detenga
                    $waited = 0
                    while ((Get-WebAppPoolState -Name $pool).Value -ne "Stopped" -and $waited -lt 15) {
                        Start-Sleep -Seconds 1
                        $waited++
                    }
                }
                $stoppedPools += $pool
                Ok "AppPool '$pool' detenido"
            } else {
                Info "AppPool '$pool' ya estaba $state"
            }
        }
    } catch {
        Warn "No se pudo detener AppPool '$pool': $($_.Exception.Message)"
    }
}

# ─── Backup del estado actual (antes de restaurar) ───────────────────────────
Title "BACKUP PRE-ROLLBACK"

$preRollbackStamp = Get-Date -Format "yyyyMMdd_HHmmss"
$preRollbackPath  = Join-Path $BackupRoot "pre_rollback_$preRollbackStamp"

if ($hasApi -and (Test-Path "$InstallRoot\api")) {
    if ($PSCmdlet.ShouldProcess("$InstallRoot\api", "Backup pre-rollback")) {
        $null = New-Item -ItemType Directory -Path "$preRollbackPath\api" -Force
        robocopy "$InstallRoot\api" "$preRollbackPath\api" /MIR /NJH /NJS /NFL /NDL /NC /NS /XD logs uploads | Out-Null
        Ok "Estado actual de api\ guardado en: $preRollbackPath\api\"
    }
}

if ($hasWeb -and (Test-Path "$InstallRoot\web")) {
    if ($PSCmdlet.ShouldProcess("$InstallRoot\web", "Backup pre-rollback")) {
        $null = New-Item -ItemType Directory -Path "$preRollbackPath\web" -Force
        robocopy "$InstallRoot\web" "$preRollbackPath\web" /MIR /NJH /NJS /NFL /NDL /NC /NS | Out-Null
        Ok "Estado actual de web\ guardado en: $preRollbackPath\web\"
    }
}

# ─── Restaurar archivos ──────────────────────────────────────────────────────
Title "RESTAURANDO ARCHIVOS"

if ($hasApi) {
    $dest = "$InstallRoot\api"
    if ($PSCmdlet.ShouldProcess("$dest", "Restaurar desde $backupApi")) {
        $null = New-Item -ItemType Directory -Path $dest -Force
        $rc = robocopy $backupApi $dest /MIR /NJH /NJS /NFL /NDL /NC /NS
        # robocopy: exit codes 0-7 son exitosos
        if ($LASTEXITCODE -le 7) {
            Ok "API restaurada desde: $backupApi"
        } else {
            Err "Error en robocopy al restaurar API (exit: $LASTEXITCODE)"
        }
    }
}

if ($hasWeb) {
    $dest = "$InstallRoot\web"
    if ($PSCmdlet.ShouldProcess("$dest", "Restaurar desde $backupWeb")) {
        $null = New-Item -ItemType Directory -Path $dest -Force
        $rc = robocopy $backupWeb $dest /MIR /NJH /NJS /NFL /NDL /NC /NS
        if ($LASTEXITCODE -le 7) {
            Ok "Web restaurada desde: $backupWeb"
        } else {
            Err "Error en robocopy al restaurar Web (exit: $LASTEXITCODE)"
        }
    }
}

# ─── Permisos ─────────────────────────────────────────────────────────────────
Title "PERMISOS"

foreach ($dir in @("$InstallRoot\uploads", "$InstallRoot\logs")) {
    if (Test-Path $dir) {
        if ($PSCmdlet.ShouldProcess($dir, "icacls IIS_IUSRS Modify")) {
            icacls $dir /grant "IIS_IUSRS:(OI)(CI)M" /T /Q 2>&1 | Out-Null
            Ok "Permisos restaurados en: $dir"
        }
    }
}

# ─── Reiniciar AppPools ──────────────────────────────────────────────────────
Title "REINICIANDO SERVICIOS"

foreach ($pool in $stoppedPools) {
    try {
        if ($iisAvailable) {
            if ($PSCmdlet.ShouldProcess("AppPool $pool", "Start")) {
                Start-WebAppPool -Name $pool
                Start-Sleep -Seconds 3
                $state = (Get-WebAppPoolState -Name $pool).Value
                if ($state -eq "Started") {
                    Ok "AppPool '$pool' iniciado"
                } else {
                    Warn "AppPool '$pool' en estado: $state"
                }
            }
        }
    } catch {
        Err "No se pudo iniciar AppPool '$pool': $($_.Exception.Message)"
    }
}

# Si no detuvimos ningún pool (porque ya estaban detenidos), intentar iniciarlos todos
if ($stoppedPools.Count -eq 0 -and $iisAvailable) {
    foreach ($pool in $pools) {
        try {
            $state = (Get-WebAppPoolState -Name $pool -ErrorAction SilentlyContinue)?.Value
            if ($state -ne "Started") {
                if ($PSCmdlet.ShouldProcess("AppPool $pool", "Start")) {
                    Start-WebAppPool -Name $pool
                    Ok "AppPool '$pool' iniciado"
                }
            }
        } catch { }
    }
}

# ─── Resumen y validación ─────────────────────────────────────────────────────
Write-Host ""
Write-Host "══════════════════════════════════════════" -ForegroundColor Magenta
Write-Host "  ROLLBACK COMPLETADO" -ForegroundColor Magenta
Write-Host "══════════════════════════════════════════" -ForegroundColor Magenta
Write-Host "  Backup restaurado : $BackupPath" -ForegroundColor White
Write-Host "  Backup pre-rollback: $preRollbackPath" -ForegroundColor Gray
Write-Host ""

if (-not $SkipValidation -and -not $WhatIfPreference) {
    $validateScript = Join-Path $PSScriptRoot "validate-deployment.ps1"
    if (Test-Path $validateScript) {
        Write-Host "  Ejecutando validación post-rollback..." -ForegroundColor Cyan
        Write-Host ""
        Start-Sleep -Seconds 5   # Dar tiempo a que IIS levante los procesos
        & $validateScript
    } else {
        Warn "validate-deployment.ps1 no encontrado en: $PSScriptRoot"
        Warn "Ejecutar manualmente para confirmar el estado del sistema."
    }
} else {
    Write-Host "  Validación omitida. Ejecutar manualmente:" -ForegroundColor Yellow
    Write-Host "  .\deploy\validate-deployment.ps1" -ForegroundColor Gray
}

Write-Host ""
Write-Host "  NOTA: Si la base de datos requiere rollback, ejecutar manualmente:" -ForegroundColor Yellow
Write-Host "  ALTER TABLE sf.refresh_token DROP COLUMN IF EXISTS operador_id;" -ForegroundColor Gray
Write-Host "  ALTER TABLE sf.refresh_token ALTER COLUMN usuario_id SET NOT NULL;" -ForegroundColor Gray
Write-Host ""

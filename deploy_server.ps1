# Deploy script for server - run on the server itself
# cd C:\Aplicaciones\sgiform && powershell -ExecutionPolicy Bypass -File deploy_server.ps1

param(
    [string]$Branch = "main"
)

$ErrorActionPreference = "Stop"
$root = "C:\Aplicaciones\sgiform"

Write-Host "=== SGI-FORM DEPLOY ===" -ForegroundColor Cyan
Set-Location $root

# Git pull
Write-Host "`n[1] Git pull..." -ForegroundColor Yellow
git pull origin $Branch
if ($LASTEXITCODE -ne 0) { Write-Host "Git pull failed" -ForegroundColor Red; exit 1 }

# Build API
Write-Host "`n[2] Publishing API..." -ForegroundColor Yellow
dotnet publish "$root\src\SgiForm.Api\SgiForm.Api.csproj" -c Release -o "$root\publish\api" --nologo
if ($LASTEXITCODE -ne 0) { Write-Host "Publish failed" -ForegroundColor Red; exit 1 }

# Apply DB schema fixes (idempotent - adds missing updated_at columns)
Write-Host "`n[3] Applying DB schema fixes..." -ForegroundColor Yellow
$pgBin = "C:\Program Files\PostgreSQL\18\bin"
$psqlExe = "$pgBin\psql.exe"
if (Test-Path $psqlExe) {
    $env:PGPASSWORD = "SgiForm2024!"
    $sqlFix = @"
ALTER TABLE sf.flujo_opcion ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ DEFAULT NOW();
ALTER TABLE sf.catalogo ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ DEFAULT NOW();
ALTER TABLE sf.sincronizacion_log ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ DEFAULT NOW();
ALTER TABLE sf.inspeccion_historial ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ DEFAULT NOW();
ALTER TABLE sf.importacion_detalle ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ DEFAULT NOW();
ALTER TABLE sf.inspeccion_respuesta ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ DEFAULT NOW();
ALTER TABLE sf.inspeccion_fotografia ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ DEFAULT NOW();
ALTER TABLE sf.auditoria ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ DEFAULT NOW();
"@
    $sqlFix | & $psqlExe -h localhost -p 5432 -U sgiform -d sgiform
    Write-Host "  DB schema OK"
} else {
    Write-Host "  psql not found at $psqlExe — skipping schema fixes" -ForegroundColor Yellow
}

# Create required directories
Write-Host "`n[4] Ensuring directories exist..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path "$root\uploads" -Force | Out-Null
New-Item -ItemType Directory -Path "$root\logs" -Force | Out-Null
Write-Host "  uploads: OK"
Write-Host "  logs: OK"

# Touch web.config to restart app
Write-Host "`n[5] Restarting API..." -ForegroundColor Yellow
$wc = "$root\publish\api\web.config"
(Get-Item $wc).LastWriteTime = Get-Date
Write-Host "  web.config touched"

# Try appcmd
$appcmd = "C:\Windows\system32\inetsrv\appcmd.exe"
if (Test-Path $appcmd) {
    & $appcmd recycle apppool /apppool.name:"SgiFormApi"
    Write-Host "  AppPool SgiFormApi recycled"
}

Write-Host "`n=== DEPLOY COMPLETE ===" -ForegroundColor Green
Write-Host "API: https://apps.solucionescloud.cl/sgiformapi/api/v1/auth/login"

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

# Create required directories
Write-Host "`n[3] Ensuring directories exist..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path "$root\uploads" -Force | Out-Null
New-Item -ItemType Directory -Path "$root\logs" -Force | Out-Null
Write-Host "  uploads: OK"
Write-Host "  logs: OK"

# Touch web.config to restart app
Write-Host "`n[4] Restarting API..." -ForegroundColor Yellow
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

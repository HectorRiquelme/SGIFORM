# =============================================================================
# deploy-apps.solucionescloud.ps1
# Ejecutar como Admin desde: C:\Aplicaciones\sgiform\src
#
# PRIMERA VEZ: crear C:\Aplicaciones\sgiform\sgiform.env con:
#   PG_PASSWORD=tu_password_postgresql
#   JWT_KEY=clave_jwt_minimo_64_chars
# =============================================================================

$ErrorActionPreference = "Stop"

$GitRoot    = "C:\Aplicaciones\sgiform\src"
$PublishWeb = "C:\Aplicaciones\sgiform\publish\web"
$PublishApi = "C:\Aplicaciones\sgiform\publish\api"
$EnvFile    = "C:\Aplicaciones\sgiform\sgiform.env"

# Leer credenciales desde archivo .env (fuera del repo)
$PgPassword = "CAMBIAR_EN_PRODUCCION"
$JwtKey     = "SgiForm_Produccion_JWT_2026_SecretKey_MustBe64BytesMinimum_XYZ!@#"

if (Test-Path $EnvFile) {
    Get-Content $EnvFile | ForEach-Object {
        if ($_ -match "^PG_PASSWORD=(.+)$")  { $PgPassword = $Matches[1] }
        if ($_ -match "^JWT_KEY=(.+)$")      { $JwtKey     = $Matches[1] }
    }
} else {
    Write-Warning "No se encontró $EnvFile — usando valores placeholder para credenciales"
}

# Verificar que estamos en el directorio correcto
if (-not (Test-Path "$GitRoot\.git")) {
    Write-Error "ERROR: Ejecuta este script desde $GitRoot (donde está el .git)"
    exit 1
}

Set-Location $GitRoot

Write-Host "=== 1. Git pull ===" -ForegroundColor Cyan
git pull
if ($LASTEXITCODE -ne 0) { Write-Error "git pull falló"; exit 1 }

Write-Host "=== 2. Publicar API ===" -ForegroundColor Cyan
dotnet publish src\SgiForm.Api\SgiForm.Api.csproj -c Release -o $PublishApi
if ($LASTEXITCODE -ne 0) { Write-Error "Publish API falló"; exit 1 }

Write-Host "=== 3. Publicar Web (con app_offline) ===" -ForegroundColor Cyan
"<h1>Actualizando SGI-FORM...</h1>" | Out-File "$PublishWeb\app_offline.htm" -Encoding utf8
Start-Sleep -Seconds 4
dotnet publish src\SgiForm.Web\SgiForm.Web.csproj -c Release -o $PublishWeb
if ($LASTEXITCODE -ne 0) {
    Remove-Item "$PublishWeb\app_offline.htm" -ErrorAction SilentlyContinue
    Write-Error "Publish Web falló"; exit 1
}

Write-Host "=== 4. Restaurar appsettings API ===" -ForegroundColor Cyan
$fileApi = "$PublishApi\appsettings.Production.json"
$jsonApi = Get-Content $fileApi | ConvertFrom-Json
$jsonApi.ConnectionStrings.Default = "Host=localhost;Port=5432;Database=sgiform;Username=sgiform;Password=$PgPassword;Search Path=sf,public;SSL Mode=Disable"
$jsonApi.Jwt.Key = $JwtKey
$jsonApi | ConvertTo-Json -Depth 10 | Set-Content $fileApi

Write-Host "=== 5. Restaurar appsettings Web ===" -ForegroundColor Cyan
$file = "$PublishWeb\appsettings.Production.json"
$json = Get-Content $file | ConvertFrom-Json
$json.ApiBaseUrl = "https://apps.solucionescloud.cl/sgiformapi/"
$json | ConvertTo-Json -Depth 10 | Set-Content $file

Write-Host "=== 6. Reactivar Web ===" -ForegroundColor Cyan
Remove-Item "$PublishWeb\app_offline.htm"

Write-Host "=== 7. Reciclar AppPools ===" -ForegroundColor Cyan
& "$env:windir\system32\inetsrv\appcmd.exe" recycle apppool /apppool.name:"SgiFormApi"
& "$env:windir\system32\inetsrv\appcmd.exe" recycle apppool /apppool.name:"SgiFormWeb"

Write-Host ""
Write-Host "=== Deploy completado ===" -ForegroundColor Green
Write-Host "Web: https://apps.solucionescloud.cl/sgiform/login"
Write-Host "API: https://apps.solucionescloud.cl/sgiformapi/health"

Get-Item "$PublishWeb\SgiForm.Web.dll" | Select-Object @{N="Web DLL";E={$_.Name}}, LastWriteTime
Get-Item "$PublishApi\SgiForm.Api.dll" | Select-Object @{N="API DLL";E={$_.Name}}, LastWriteTime

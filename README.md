# SGI-FORM

Sistema de Gestión de Inspecciones con formularios dinámicos, app móvil offline y portal web administrativo.

## Arquitectura

```
┌─────────────────────────────────────────────────────────┐
│                    Windows Server 2019                   │
│                                                         │
│  IIS :5001           IIS :8080          PostgreSQL 16   │
│  ┌──────────┐       ┌──────────┐       ┌─────────────┐  │
│  │SgiForm   │       │SgiForm   │       │  DB sgiform  │  │
│  │  .Api    │──────▶│  .Web    │       │  schema: sf  │  │
│  │ (REST)   │       │(Blazor)  │       │  25 tablas   │  │
│  └──────────┘       └──────────┘       └─────────────┘  │
│       │                                       ▲          │
│       └───────────────────────────────────────┘          │
└─────────────────────────────────────────────────────────┘
         ▲                    ▲
         │                    │
   App Móvil MAUI        Navegador Web
   (Android offline)     (Administración)
```

## Stack

| Componente | Tecnología | Versión |
|---|---|---|
| API REST | ASP.NET Core Web API | .NET 8 |
| ORM | Entity Framework Core + Npgsql | 8.0 |
| Base de datos | PostgreSQL | 16 |
| Frontend admin | Blazor Server | .NET 8 |
| App móvil | .NET MAUI Android | .NET 8 |
| Auth | JWT Bearer + BCrypt.Net | HS256, 60 min |
| Logging | Serilog | 8.0 |

## Requisitos del servidor

- Windows Server 2016/2019/2022
- IIS con ANCM v2 (ASP.NET Core Module)
- .NET 8 SDK/Runtime
- PostgreSQL 16
- Git
- 4 GB RAM mínimo, 20 GB disco libre

## Build

```powershell
# Clonar
git clone https://github.com/HectorRiquelme/SGIFORM.git
cd SGIFORM

# Ejecutar tests
dotnet test tests/SgiForm.Tests/ -v normal
# Resultado esperado: 46 passed, 0 failed

# Publicar API
dotnet publish src/SgiForm.Api/SgiForm.Api.csproj -c Release -r win-x64 --self-contained false -o C:\SgiForm\publish\api

# Publicar Web
dotnet publish src/SgiForm.Web/SgiForm.Web.csproj -c Release -r win-x64 --self-contained false -o C:\SgiForm\publish\web
```

## Deploy en IIS

### 1. Crear AppPools

```powershell
Import-Module WebAdministration

New-WebAppPool -Name "SgiFormApi"
Set-ItemProperty "IIS:\AppPools\SgiFormApi" managedRuntimeVersion ""
Set-ItemProperty "IIS:\AppPools\SgiFormApi" startMode "AlwaysRunning"
Set-ItemProperty "IIS:\AppPools\SgiFormApi" processModel.idleTimeout "00:00:00"

New-WebAppPool -Name "SgiFormWeb"
Set-ItemProperty "IIS:\AppPools\SgiFormWeb" managedRuntimeVersion ""
Set-ItemProperty "IIS:\AppPools\SgiFormWeb" startMode "AlwaysRunning"
Set-ItemProperty "IIS:\AppPools\SgiFormWeb" processModel.idleTimeout "00:00:00"
```

### 2. Crear sitios

```powershell
New-Website -Name "SgiFormApi" -PhysicalPath "C:\SgiForm\publish\api" -ApplicationPool "SgiFormApi" -Port 5001 -Force
New-Website -Name "SgiFormWeb" -PhysicalPath "C:\SgiForm\publish\web" -ApplicationPool "SgiFormWeb" -Port 8080 -Force
```

### 3. Permisos

```powershell
$paths = @("C:\SgiForm\publish\api", "C:\SgiForm\publish\web", "C:\SgiForm\logs")
foreach ($path in $paths) {
    $acl = Get-Acl $path
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS_IUSRS","FullControl","ContainerInherit,ObjectInherit","None","Allow")
    $acl.SetAccessRule($rule)
    Set-Acl $path $acl
}
```

## Configuración PostgreSQL desde scripts

```powershell
# 1. Crear usuario y base de datos
$env:PGPASSWORD = "postgres"
$psql = "C:\Program Files\PostgreSQL\16\bin\psql.exe"
& $psql -U postgres -c "CREATE ROLE sgiform LOGIN PASSWORD 'TU_PASSWORD';"
& $psql -U postgres -c "CREATE DATABASE sgiform OWNER sgiform ENCODING 'UTF8';"

# 2. Ejecutar scripts en orden
$env:PGPASSWORD = "TU_PASSWORD"
& $psql -U sgiform -d sgiform -f "database\01_schema.sql"
& $psql -U sgiform -d sgiform -f "database\02_seed.sql"
& $psql -U sgiform -d sgiform -f "database\03_operador_refresh_token.sql"

# Verificar: debe mostrar 25 tablas
& $psql -U sgiform -d sgiform -c "\dt sf.*"
```

## Variables de entorno

Configurar en el AppPool `SgiFormApi` via `appcmd.exe`:

```powershell
$appcmd = "$env:windir\system32\inetsrv\appcmd.exe"

& $appcmd set apppool "SgiFormApi" /+"environmentVariables.[name='ASPNETCORE_ENVIRONMENT',value='Production']"
& $appcmd set apppool "SgiFormApi" /+"environmentVariables.[name='ConnectionStrings__Default',value='Host=localhost;Port=5432;Database=sgiform;Username=sgiform;Password=TU_PASSWORD;Search Path=sf,public']"
& $appcmd set apppool "SgiFormApi" /+"environmentVariables.[name='Jwt__Key',value='TU_JWT_SECRET_MIN_64_BYTES_BASE64']"
& $appcmd set apppool "SgiFormApi" /+"environmentVariables.[name='Jwt__Issuer',value='SgiFormApi']"
& $appcmd set apppool "SgiFormApi" /+"environmentVariables.[name='Jwt__Audience',value='SgiFormClients']"

& $appcmd set apppool "SgiFormWeb" /+"environmentVariables.[name='ASPNETCORE_ENVIRONMENT',value='Production']"
& $appcmd set apppool "SgiFormWeb" /+"environmentVariables.[name='ApiSettings__BaseUrl',value='http://localhost:5001']"
```

### Generar JWT Secret seguro

```powershell
$rng = [System.Security.Cryptography.RNGCryptoServiceProvider]::new()
$bytes = New-Object byte[] 64
$rng.GetBytes($bytes)
[Convert]::ToBase64String($bytes)
```

## Validación

```powershell
# 1. Estado IIS
Get-Website | Where-Object { $_.Name -like "SgiForm*" } | Select-Object Name, State
Get-WebAppPoolState -Name "SgiFormApi"
Get-WebAppPoolState -Name "SgiFormWeb"

# 2. Test API
$r = Invoke-RestMethod "http://localhost:5001/api/v1/auth/login" -Method POST -ContentType "application/json" -Body '{"email":"admin@sanitaria-demo.cl","password":"Admin@2024!"}'
Write-Host "Login OK: $($r.nombre)"

# 3. Test Web
(Invoke-WebRequest "http://localhost:8080" -UseBasicParsing).StatusCode

# 4. Test login móvil
$m = Invoke-RestMethod "http://localhost:5001/api/v1/auth/login-movil" -Method POST -ContentType "application/json" -Body '{"codigo_operador":"OP001","password":"Admin@2024!","empresa_slug":"sanitaria-demo"}'
Write-Host "Móvil OK: $($m.access_token.Substring(0,20))..."

# 5. PostgreSQL
$env:PGPASSWORD = "TU_PASSWORD"
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U sgiform -d sgiform -c "SELECT COUNT(*) FROM sf.usuario;"
```

## Troubleshooting

### IIS 400 Bad Request - Invalid Hostname
**Causa**: `AllowedHosts` en `appsettings.Production.json` no incluye el host usado.
**Fix**: Cambiar `"AllowedHosts": "*"` temporalmente o agregar el hostname correcto.

### IIS 500 - columna ip_origen es de tipo inet
**Causa**: Schema SQL define `ip_origen` como `INET` pero EF Core lo envía como `text`.
**Fix**:
```sql
ALTER TABLE sf.refresh_token ALTER COLUMN ip_origen TYPE text USING ip_origen::text;
ALTER TABLE sf.sincronizacion_log ALTER COLUMN ip_origen TYPE text USING ip_origen::text;
```

### IIS 500.30 - Failed to start application
**Causa**: AppPool sin variables de entorno o connection string incorrecta.
**Fix**: Verificar variables con `appcmd list apppool "SgiFormApi" /text:*`

### IIS 502.5 - Process Failure
**Causa**: Puerto ocupado o falta ANCM v2.
**Fix**: Verificar ANCM con `Get-WebConfiguration "system.webServer/globalModules/add[@name='AspNetCoreModuleV2']"`

### PostgreSQL - password authentication failed
**Causa**: Contraseña incorrecta para el usuario `sgiform`.
**Fix**: Resetear con `ALTER ROLE sgiform WITH PASSWORD 'nueva_password';` como superusuario.

### Error binding al puerto
**Causa**: IIS/http.sys reserva el puerto aunque el AppPool esté detenido.
**Fix**: Usar un puerto diferente en el binding del sitio IIS, o detener el sitio antes de usar el puerto directamente.

## Rollback

```powershell
# Parar sitios
Stop-Website -Name "SgiFormApi"
Stop-Website -Name "SgiFormWeb"

# Restaurar backup (si existe)
robocopy "C:\SgiForm\backup\api" "C:\SgiForm\publish\api" /MIR
robocopy "C:\SgiForm\backup\web" "C:\SgiForm\publish\web" /MIR

# Reiniciar
Start-Website -Name "SgiFormApi"
Start-Website -Name "SgiFormWeb"
```

Ver `deploy/rollback.ps1` para rollback completo con selección interactiva.

## Credenciales iniciales (seed)

| Usuario | Email/Código | Password |
|---|---|---|
| Admin web | admin@sanitaria-demo.cl | Admin@2024! |
| Operador 1 | OP001 | Admin@2024! |
| Operador 2 | OP002 | Admin@2024! |
| Operador 3 | OP003 | Admin@2024! |

> **Cambiar todas las contraseñas antes de exponer a producción real.**

## Estructura del proyecto

```
src/
  SgiForm.Api/          # 12 controllers REST, Program.cs
  SgiForm.Web/          # Blazor Server — 13 páginas
  SgiForm.Infrastructure/ # EF Core, AuthService, ExcelImportService
  SgiForm.Domain/       # Entidades, enums
  SgiForm.Mobile/       # MAUI Android — offline + sync
database/
  01_schema.sql         # DDL — 25 tablas, schema sf
  02_seed.sql           # Datos iniciales
  03_operador_refresh_token.sql  # Migración refresh token móvil
deploy/
  deploy-server.ps1     # Automatización completa
  validate-deployment.ps1
  rollback.ps1
tests/
  SgiForm.Tests/        # 46 tests integración — 0 failures
```

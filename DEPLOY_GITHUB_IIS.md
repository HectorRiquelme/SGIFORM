# Deploy desde GitHub a IIS y publicación APK

Este proyecto ya incluye workflows para:

- Crear release y notas en GitHub solo cuando subes un tag (`v*`).
- Desplegar Web + API a IIS desde GitHub Actions.
- Generar APK y copiarla a una ruta específica.

## 1) Versionamiento y notas de versión

Archivos fuente:

- `CHANGELOG.md`
- `release-notes.json`

Flujo recomendado por versión:

1. Actualizar `CHANGELOG.md` y `release-notes.json`.
2. Hacer commit.
3. Crear tag: `git tag v1.0.1`.
4. Subir tag: `git push origin v1.0.1`.

Al subir el tag se ejecuta `.github/workflows/github-release.yml` y crea la release en GitHub.

## 2) Deploy Web + API a IIS

Workflow: `.github/workflows/deploy-iis.yml`

### Requisitos

- Runner self-hosted en el servidor Windows (o en una máquina con VPN y acceso al IIS).
- Labels del runner: `self-hosted`, `windows`, `iis`.
- .NET 8 instalado en el runner.
- Permisos de escritura a las carpetas de publicación de IIS.

### Secrets requeridos

- `IIS_WEB_PATH` (ej: `C:\inetpub\wwwroot\sgiform-web`)
- `IIS_API_PATH` (ej: `C:\inetpub\wwwroot\sgiform-api`)
- `IIS_WEB_APPPOOL` (opcional)
- `IIS_API_APPPOOL` (opcional)

### Ejecución

Desde GitHub > Actions > `deploy-iis` > Run workflow, indicando `ref` (branch o tag).

El workflow:

- Restore/build/test
- `dotnet publish` de Web y API
- copia con `robocopy /MIR`
- detiene/inicia app pools si están configurados

## 3) Publicar APK en ruta específica

Workflow: `.github/workflows/publish-apk.yml`

### Requisitos

- Runner self-hosted con Android SDK y .NET MAUI workload.
- Labels del runner: `self-hosted`, `windows`, `android`.

### Ejecución

Desde GitHub > Actions > `publish-apk` > Run workflow:

- `ref`: branch o tag
- `apk_drop_path`: ruta destino (opcional), por ejemplo `D:\deploy\apk`

También sube el APK como artifact en GitHub Actions.

# NEXT_STEPS.md — Trabajo pendiente

> Actualizar este archivo al inicio y al final de cada sesión de trabajo.
> Ordenado por prioridad real de negocio / riesgo técnico.

---

## Estado del sistema al día de hoy

- ✅ Build: 0 errores
- ✅ Tests: 46/46
- ✅ Naming: SgiForm en todo el repo
- ✅ Producción-ready según validación de deploy

---

## Prioridad ALTA — Bloquean uso real en producción

### 1. Verificar y corregir GitHub Actions post-renombramiento

Los workflows en `.github/workflows/` (deploy-iis.yml, publish-apk.yml) pueden referenciar rutas o nombres anteriores (`SanitasField.*`) que dejaron de existir.

**Qué hacer:**
```powershell
# Revisar workflows
cat .github/workflows/deploy-iis.yml
cat .github/workflows/publish-apk.yml
# Buscar cualquier referencia a SanitasField
grep -r "SanitasField" .github/
```
**Tiempo estimado**: 30 min

---

### 2. Primer despliegue real en servidor de producción

Los scripts están listos. Falta ejecutarlos en el servidor real.

**Pasos pendientes:**
1. Conectar vía RDP/VPN al servidor Windows
2. Copiar scripts SQL (`database/`) al servidor
3. Ejecutar `deploy/deploy-server.ps1`
4. Ejecutar `deploy/validate-deployment.ps1` → confirmar PASS: 26, FAIL: 0
5. Probar login desde la app móvil Android

**Prerequisito**: Tener las credenciales de producción (password BD, JWT key) listas.

---

### 3. Configurar CORS para la URL real de producción

Actualmente `appsettings.json` tiene:
```json
"Cors": {
  "AllowedOrigins": ["http://localhost:5200", "http://localhost:7200"]
}
```

En producción debe tener la URL real del servidor o del dominio. Configurar en la variable de entorno del AppPool o en `appsettings.Production.json`.

```powershell
Set-AppPoolEnv "SgiForm-API" "Cors__AllowedOrigins__0" "https://tudominio.cl"
```

---

## Prioridad MEDIA — Mejoran calidad y mantenibilidad

### 4. Ampliar cobertura de tests de integración

Módulos sin tests actualmente:

| Módulo | Controller | Qué testear |
|--------|-----------|-------------|
| Flujos | FlujoController | Crear flujo, agregar sección/pregunta, publicar versión |
| Importación | ImportacionController | Upload Excel, preview, confirmar lote |
| Asignaciones | AsignacionController | Asignación individual, masiva, cambio de estado |
| Inspecciones | InspeccionesController | Aprobar, observar, rechazar |
| Dashboard | DashboardController | KPIs con datos semilla |
| Reportes | ReportesController | Generación Excel sin error |

**Objetivo**: llevar de 46 a ~80+ tests.

---

### 5. Mover lógica de negocio de controllers a capa Application

`SgiForm.Application` está vacío (placeholder). Los controllers tienen lógica de negocio mezclada con HTTP.

**Patrón a seguir:**
```csharp
// En lugar de lógica en el controller:
public class CrearOperadorUseCase { ... }
// Controller solo orquesta:
var resultado = await _crearOperador.ExecuteAsync(req);
```

**Prioridad para**: AuthController, SyncController (los más complejos).

---

### 6. Refresh token automático en Blazor Web

La sesión Blazor expira cuando el JWT de 60 min caduca sin que el usuario lo sepa. Implementar renovación silenciosa en `ApiClient.cs`.

**Qué implementar:**
- En `ApiClient`: interceptar respuestas 401, llamar `/auth/refresh`, reintentar request original
- `AuthStateService`: almacenar y actualizar `refresh_token`

---

### 7. Actualizar CHANGELOG.md

El `CHANGELOG.md` solo tiene la entrada `[1.0.0] - 2026-03-19`. Documenta las fases 2-6.

```markdown
## [1.1.0] - 2026-03-22
### Added
- Rate limiting nativo ASP.NET Core 8 (3 políticas)
- Refresh tokens revocables para operadores móviles
- FlowValidatorService — validación server-side de flujos
- Scripts y documentación completa de despliegue IIS + PostgreSQL nativo
### Fixed
- Enum.TryParse en SyncController (era Enum.Parse)
- Nullable warnings en Blazor pages
### Changed
- Renombramiento completo SanitasField → SgiForm (180 archivos)
- Health check restringido a localhost/127.0.0.1
```

---

## Prioridad BAJA — Mejoras futuras

### 8. Caché para endpoints de lectura frecuente

Los endpoints de dashboard y listados se consultan frecuentemente. Considerar:
- `IMemoryCache` para KPIs (TTL 5 min)
- `OutputCache` de ASP.NET Core 8 para endpoints de catálogos

### 9. Paginación cursor-based

Actualmente se usa paginación offset (`page`, `pageSize`). Para tablas grandes (> 10k registros), considerar cursor-based con `after_id`.

### 10. GPS en inspecciones web

La app móvil captura coordenadas GPS. El panel web muestra coordenadas como texto. Integrar un mapa (Leaflet.js via JavaScript interop en Blazor) para visualización geográfica.

### 11. Notificaciones push a operadores móviles

Cuando se asigna una nueva inspección, el operador no es notificado automáticamente. Considerar Firebase Cloud Messaging (FCM) en MAUI.

### 12. Autenticación multi-factor (MFA)

Para usuarios web con rol `admin`, implementar TOTP (Google Authenticator) como segundo factor.

---

## Decisiones técnicas pendientes

| Decisión | Opciones | Recomendación |
|----------|---------|---------------|
| ¿Migrar Application layer a patrón CQRS? | Sí / No | Sí, cuando el sistema crezca — no urgente ahora |
| ¿Usar EF Core Migrations en lugar de scripts SQL? | Sí / No | No — los scripts SQL dan más control; mantener patrón actual |
| ¿HTTPS en producción? | SSL en IIS / Reverse proxy nginx | Configurar SSL en binding IIS o poner nginx/Cloudflare delante |
| ¿Versionar la API? | Mantener v1 / Agregar v2 | Mantener v1 hasta que haya breaking changes |

---

## Registro de sesiones

| Fecha | Trabajo realizado |
|-------|-------------------|
| 2026-03-19 | Creación inicial del proyecto completo |
| 2026-03-22 | QA pre-producción, rate limiting, requisitos de producción, DevOps deploy, renombramiento SgiForm |
| 2026-03-22 | Creación de archivos de contexto persistente (CLAUDE.md, AGENTS.md, PROJECT_CONTEXT.md, NEXT_STEPS.md) |

# AGENTS.md — Instrucciones para agentes de IA

> Este archivo define cómo cualquier agente de IA (Claude Code u otro) debe operar en este repositorio. Leer antes de realizar cualquier acción.

---

## Identidad y alcance

- **Proyecto**: SgiForm (SGI-FORM) — sistema de inspecciones técnicas en terreno
- **Repo**: https://github.com/HectorRiquelme/SGIFORM
- **Owner**: HectorRiquelme
- **Stack**: .NET 8 / ASP.NET Core / Blazor Server / MAUI Android / PostgreSQL 16+

---

## Reglas absolutas (no negociables)

1. **Naming**: Los namespaces son `SgiForm.*`. El nombre comercial es `SGI-FORM`. NO usar `SgiForm` en ningún archivo nuevo. La migración de nombres ya fue completada.
2. **Secretos**: NUNCA escribir contraseñas, JWT keys, connection strings con credenciales reales en código o archivos del repositorio. Los secretos van en variables de entorno del AppPool IIS en producción.
3. **Tests antes de desplegar**: Si se modifica código, correr `dotnet test tests/SgiForm.Tests/` y confirmar 46/46 antes de proponer deploy.
4. **Multitenant**: Todo query a la BD debe filtrar por `empresa_id`. No omitir este filtro bajo ninguna circunstancia.
5. **Enum parsing externo**: Usar siempre `Enum.TryParse`, nunca `Enum.Parse` para inputs del usuario o red.
6. **No Docker en producción**: El entorno de producción usa PostgreSQL nativo en Windows Server. Docker solo en desarrollo.

---

## Antes de modificar código

1. Leer `CLAUDE.md` para entender el contexto completo.
2. Revisar `PROJECT_CONTEXT.md` para el estado actual.
3. Revisar `NEXT_STEPS.md` para el trabajo pendiente.
4. Verificar que los tests siguen en 46/46 después de cualquier cambio.

---

## Convenciones que respetar

### C#
- JSON: `snake_case` (`JsonNamingPolicy.SnakeCaseLower`)
- Rutas API: `/api/v1/{recurso}` en plural
- Entidades: heredar de `BaseEntity` o `SoftDeleteEntity`
- PKs: siempre `Guid`, generado por la BD (`uuid-ossp`)
- Enums en BD: `VARCHAR` con `SnakeCaseEnumConverter`

### SQL / PostgreSQL
- Schema: siempre `sf.` como prefijo
- Nombres: snake_case
- Nuevas tablas: incluir `id UUID DEFAULT uuid_generate_v4()`, `created_at`, `updated_at`, `empresa_id`
- Migraciones: nuevos archivos SQL numerados (ej: `04_nombre_cambio.sql`)

### Tests
- Entorno: `ASPNETCORE_ENVIRONMENT = "Testing"`
- BD: InMemory — no se puede probar SQL específico de PostgreSQL
- Operaciones destructivas en tests: crear recursos propios en el test, no modificar seed compartido (OP001, OP002, admin)
- Credenciales de seed: `admin@test.cl / Test@123`, operadores `Op@123`

---

## Flujo de trabajo recomendado para cambios

```
1. Leer archivos de contexto (CLAUDE.md, PROJECT_CONTEXT.md, NEXT_STEPS.md)
2. Identificar archivos afectados
3. Implementar cambio
4. Si hay migración SQL: crear database/0N_descripcion.sql
5. Actualizar tests si aplica
6. Correr dotnet test → confirmar 46/46 (o más si se añadieron tests)
7. Actualizar NEXT_STEPS.md y PROJECT_CONTEXT.md
8. Commit con mensaje descriptivo siguiendo el patrón:
   feat|fix|refactor|docs|test|deploy(alcance): descripción
```

---

## Archivos críticos — no modificar sin entender el impacto

| Archivo | Impacto si se rompe |
|---------|---------------------|
| `src/SgiForm.Infrastructure/Persistence/AppDbContext.cs` | Toda la BD deja de funcionar |
| `src/SgiForm.Infrastructure/Persistence/SnakeCaseEnumConverter.cs` | Todos los enums rompen en BD |
| `src/SgiForm.Api/Program.cs` | API no arranca |
| `src/SgiForm.Infrastructure/Services/AuthService.cs` | Autenticación web y móvil |
| `database/01_schema.sql` | Schema completo de BD (idempotente con cuidado) |
| `SgiForm.sln` | Solución no compila |
| `tests/SgiForm.Tests/TestFixture.cs` | Todos los tests de integración |

---

## Qué NO hacer

- No agregar Docker Compose para producción
- No agregar migraciones EF Core (el proyecto usa scripts SQL manuales)
- No cambiar el schema de BD sin crear un archivo `database/0N_*.sql`
- No cambiar la estructura de claims del JWT sin actualizar el parser en todos los controllers
- No activar Swagger en producción (`IsDevelopment()` lo controla)
- No modificar el seed de tests de forma que rompa tests existentes
- No crear archivos README.md o .md adicionales sin necesidad real

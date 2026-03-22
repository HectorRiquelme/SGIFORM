using SQLite;
using SgiForm.Mobile.Models;

namespace SgiForm.Mobile.Database;

/// <summary>
/// Base de datos SQLite local para la app móvil.
/// Almacena asignaciones, inspecciones, respuestas y cola de sincronización.
/// Implementa el patrón offline-first: todo se guarda localmente primero.
/// </summary>
public class AppDatabase
{
    private SQLiteAsyncConnection? _db;
    private readonly string _dbPath;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public AppDatabase()
    {
        _dbPath = Path.Combine(
            FileSystem.AppDataDirectory,
            "sgiform.db");
    }

    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_db != null) return _db;

        await _initLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_db != null) return _db;

            var db = new SQLiteAsyncConnection(_dbPath, SQLiteOpenFlags.ReadWrite |
                SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

            // Crear tablas primero (siempre funciona)
            await db.CreateTableAsync<AsignacionLocal>();
            await db.CreateTableAsync<InspeccionLocal>();
            await db.CreateTableAsync<RespuestaLocal>();
            await db.CreateTableAsync<FotografiaLocal>();
            await db.CreateTableAsync<FlujoVersionLocal>();
            await db.CreateTableAsync<SeccionLocal>();
            await db.CreateTableAsync<PreguntaLocal>();
            await db.CreateTableAsync<OpcionLocal>();
            await db.CreateTableAsync<ReglaLocal>();
            await db.CreateTableAsync<CatalogoLocal>();
            await db.CreateTableAsync<SyncQueueItem>();

            // PRAGMAs opcionales para rendimiento (pueden fallar en algunos dispositivos)
            try
            {
                await db.ExecuteAsync("PRAGMA journal_mode=WAL;");
                await db.ExecuteAsync("PRAGMA synchronous=NORMAL;");
                await db.ExecuteAsync("PRAGMA cache_size=4000;");
            }
            catch
            {
                // PRAGMAs opcionales; funcionará con defaults si fallan
            }

            _db = db;
            return _db;
        }
        finally
        {
            _initLock.Release();
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ASIGNACIONES
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<List<AsignacionLocal>> GetAsignacionesAsync(string? estado = null)
    {
        var db = await GetConnectionAsync();
        if (estado != null)
            return await db.Table<AsignacionLocal>()
                .Where(a => a.Estado == estado)
                .OrderBy(a => a.Prioridad)
                .ToListAsync();

        return await db.Table<AsignacionLocal>()
            .OrderBy(a => a.Prioridad)
            .ToListAsync();
    }

    public async Task<AsignacionLocal?> GetAsignacionAsync(string id)
    {
        var db = await GetConnectionAsync();
        return await db.Table<AsignacionLocal>().FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task UpsertAsignacionAsync(AsignacionLocal asignacion)
    {
        var db = await GetConnectionAsync();
        var existente = await db.Table<AsignacionLocal>()
            .FirstOrDefaultAsync(a => a.Id == asignacion.Id);

        if (existente == null)
            await db.InsertAsync(asignacion);
        else
        {
            // Solo actualizar si el servidor tiene versión más nueva
            if (asignacion.UpdatedAt > existente.UpdatedAt)
                await db.UpdateAsync(asignacion);
        }
    }

    public async Task UpsertAsignacionesAsync(List<AsignacionLocal> asignaciones)
    {
        var db = await GetConnectionAsync();
        await db.RunInTransactionAsync(conn =>
        {
            foreach (var a in asignaciones)
            {
                var existente = conn.Table<AsignacionLocal>()
                    .FirstOrDefault(x => x.Id == a.Id);
                if (existente == null)
                    conn.Insert(a);
                else if (a.UpdatedAt > existente.UpdatedAt)
                    conn.Update(a);
            }
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // INSPECCIONES
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<InspeccionLocal?> GetInspeccionByAsignacionAsync(string asignacionId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<InspeccionLocal>()
            .FirstOrDefaultAsync(i => i.AsignacionId == asignacionId);
    }

    public async Task<InspeccionLocal?> GetInspeccionAsync(string id)
    {
        var db = await GetConnectionAsync();
        return await db.Table<InspeccionLocal>()
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task SaveInspeccionAsync(InspeccionLocal inspeccion)
    {
        var db = await GetConnectionAsync();
        inspeccion.UpdatedAt = DateTime.UtcNow;

        var existente = await db.Table<InspeccionLocal>()
            .FirstOrDefaultAsync(i => i.Id == inspeccion.Id);

        if (existente == null)
        {
            inspeccion.CreatedAt = DateTime.UtcNow;
            await db.InsertAsync(inspeccion);
        }
        else
            await db.UpdateAsync(inspeccion);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // RESPUESTAS
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<List<RespuestaLocal>> GetRespuestasAsync(string inspeccionId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<RespuestaLocal>()
            .Where(r => r.InspeccionId == inspeccionId)
            .ToListAsync();
    }

    public async Task SaveRespuestaAsync(RespuestaLocal respuesta)
    {
        var db = await GetConnectionAsync();
        respuesta.UpdatedAt = DateTime.UtcNow;

        var existente = await db.Table<RespuestaLocal>()
            .FirstOrDefaultAsync(r => r.InspeccionId == respuesta.InspeccionId &&
                                      r.PreguntaId == respuesta.PreguntaId);
        if (existente == null)
            await db.InsertAsync(respuesta);
        else
        {
            respuesta.Id = existente.Id;
            await db.UpdateAsync(respuesta);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // FOTOGRAFÍAS
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<List<FotografiaLocal>> GetFotografiasAsync(string inspeccionId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<FotografiaLocal>()
            .Where(f => f.InspeccionId == inspeccionId)
            .OrderBy(f => f.Orden)
            .ToListAsync();
    }

    public async Task SaveFotografiaAsync(FotografiaLocal foto)
    {
        var db = await GetConnectionAsync();
        await db.InsertAsync(foto);
    }

    public async Task DeleteFotografiaAsync(string id)
    {
        var db = await GetConnectionAsync();
        await db.DeleteAsync<FotografiaLocal>(id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // FLUJOS (descargados del servidor)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task UpsertFlujoVersionAsync(FlujoVersionLocal version)
    {
        var db = await GetConnectionAsync();
        var existente = await db.Table<FlujoVersionLocal>().FirstOrDefaultAsync(v => v.Id == version.Id);
        if (existente == null) await db.InsertAsync(version);
        // flujos publicados son inmutables; no se actualiza si ya existe
    }

    public async Task UpsertSeccionAsync(SeccionLocal seccion)
    {
        var db = await GetConnectionAsync();
        var existente = await db.Table<SeccionLocal>().FirstOrDefaultAsync(s => s.Id == seccion.Id);
        if (existente == null) await db.InsertAsync(seccion);
    }

    public async Task UpsertPreguntaAsync(PreguntaLocal pregunta)
    {
        var db = await GetConnectionAsync();
        var existente = await db.Table<PreguntaLocal>().FirstOrDefaultAsync(p => p.Id == pregunta.Id);
        if (existente == null) await db.InsertAsync(pregunta);
    }

    public async Task UpsertOpcionAsync(OpcionLocal opcion)
    {
        var db = await GetConnectionAsync();
        var existente = await db.Table<OpcionLocal>().FirstOrDefaultAsync(o => o.Id == opcion.Id);
        if (existente == null) await db.InsertAsync(opcion);
    }

    public async Task UpsertReglaAsync(ReglaLocal regla)
    {
        var db = await GetConnectionAsync();
        var existente = await db.Table<ReglaLocal>().FirstOrDefaultAsync(r => r.Id == regla.Id);
        if (existente == null) await db.InsertAsync(regla);
        else { regla.Id = existente.Id; await db.UpdateAsync(regla); }
    }

    public async Task<FotografiaLocal?> GetFotografiaByIdAsync(string id)
    {
        var db = await GetConnectionAsync();
        return await db.Table<FotografiaLocal>().FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<List<SeccionLocal>> GetSeccionesAsync(string flujoVersionId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<SeccionLocal>()
            .Where(s => s.FlujoVersionId == flujoVersionId)
            .OrderBy(s => s.Orden)
            .ToListAsync();
    }

    public async Task<List<PreguntaLocal>> GetPreguntasAsync(string seccionId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<PreguntaLocal>()
            .Where(p => p.SeccionId == seccionId)
            .OrderBy(p => p.Orden)
            .ToListAsync();
    }

    public async Task<List<OpcionLocal>> GetOpcionesAsync(string preguntaId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<OpcionLocal>()
            .Where(o => o.PreguntaId == preguntaId && o.Activo)
            .OrderBy(o => o.Orden)
            .ToListAsync();
    }

    public async Task<List<ReglaLocal>> GetReglasAsync(string flujoVersionId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<ReglaLocal>()
            .Where(r => r.FlujoVersionId == flujoVersionId && r.Activo)
            .OrderBy(r => r.Orden)
            .ToListAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // COLA DE SINCRONIZACIÓN
    // ──────────────────────────────────────────────────────────────────────────

    public async Task EnqueueAsync(SyncQueueItem item)
    {
        var db = await GetConnectionAsync();
        await db.InsertAsync(item);
    }

    public async Task<List<SyncQueueItem>> GetPendingSyncAsync(int maxItems = 50)
    {
        var db = await GetConnectionAsync();
        return await db.Table<SyncQueueItem>()
            .Where(s => s.Estado == "pending" || s.Estado == "error")
            .OrderBy(s => s.CreatedAt)
            .Take(maxItems)
            .ToListAsync();
    }

    public async Task MarkSyncSentAsync(string id)
    {
        var db = await GetConnectionAsync();
        var item = await db.FindAsync<SyncQueueItem>(id);
        if (item != null)
        {
            item.Estado = "sent";
            item.UltimoIntento = DateTime.UtcNow;
            await db.UpdateAsync(item);
        }
    }

    public async Task MarkSyncErrorAsync(string id, string error)
    {
        var db = await GetConnectionAsync();
        var item = await db.FindAsync<SyncQueueItem>(id);
        if (item != null)
        {
            item.Estado = "error";
            item.Intentos++;
            item.UltimoIntento = DateTime.UtcNow;
            item.Error = error;
            await db.UpdateAsync(item);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CATÁLOGOS
    // ──────────────────────────────────────────────────────────────────────────

    public async Task UpsertCatalogoAsync(CatalogoLocal catalogo)
    {
        var db = await GetConnectionAsync();
        var existente = await db.Table<CatalogoLocal>()
            .FirstOrDefaultAsync(c => c.Tipo == catalogo.Tipo && c.Codigo == catalogo.Codigo);

        if (existente == null) await db.InsertAsync(catalogo);
        else { catalogo.Id = existente.Id; await db.UpdateAsync(catalogo); }
    }

    public async Task<List<CatalogoLocal>> GetCatalogoPorTipoAsync(string tipo)
    {
        var db = await GetConnectionAsync();
        return await db.Table<CatalogoLocal>()
            .Where(c => c.Tipo == tipo && c.Activo)
            .OrderBy(c => c.Orden)
            .ToListAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ESTADÍSTICAS LOCALES
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<EstadisticasLocales> GetEstadisticasAsync()
    {
        var db = await GetConnectionAsync();

        return new EstadisticasLocales
        {
            TotalAsignaciones = await db.Table<AsignacionLocal>().CountAsync(),
            Pendientes = await db.Table<AsignacionLocal>().CountAsync(a => a.Estado == "pendiente"),
            EnEjecucion = await db.Table<AsignacionLocal>().CountAsync(a => a.Estado == "en_ejecucion"),
            Completadas = await db.Table<AsignacionLocal>().CountAsync(a => a.Estado == "finalizada"),
            PendientesSync = await db.Table<SyncQueueItem>().CountAsync(s => s.Estado == "pending")
        };
    }
}

public record EstadisticasLocales(
    int TotalAsignaciones = 0,
    int Pendientes = 0,
    int EnEjecucion = 0,
    int Completadas = 0,
    int PendientesSync = 0);

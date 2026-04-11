-- ============================================================================
-- backfill_bugs_1_2_3.sql
-- ----------------------------------------------------------------------------
-- Repara datos corruptos en producción causados por bugs detectados en la
-- auditoría 2026-04. NO ejecuta nada por defecto: es un script de DRY-RUN con
-- los bloques de UPDATE comentados. Revisar cada SELECT primero, luego des-
-- comentar y ejecutar los UPDATE dentro de una transacción.
--
-- Contexto de cada bug:
--
--   Bug 1 — GPS de fotos almacenados sin separador decimal
--           Causa: SyncService.cs en locales es-CL generaba "-33,4569" con
--                  decimal.ToString() y el API parseaba la coma como
--                  separador de miles, guardando -334569.
--           Fix código: CoordenadaX/Y.ToString(CultureInfo.InvariantCulture)
--           Aplicado en: src/SgiForm.Mobile/Services/SyncService.cs
--
--   Bug 2 — inspeccion.total_respondidas = 0 aunque existen respuestas reales
--           Causa: SyncController leía inspeccion.Respuestas (nav collection)
--                  antes de SaveChanges, cuando las nuevas entidades aún no
--                  estaban en la colección.
--           Fix código: SaveChanges intermedio + recount por query directo.
--           Aplicado en: src/SgiForm.Api/Controllers/SyncController.cs
--
--   Bug 3 — total_fotografias desactualizado por G8 en registros antiguos.
--           Fix código: ya aplicado para nuevos uploads (G8).
--           Backfill aquí: recontar desde inspeccion_fotografia.
--
-- Todos los SELECT usan el schema `sf` (multitenant por empresa_id). Revisar
-- los resultados por empresa antes de aplicar los UPDATE.
-- ============================================================================

\set ON_ERROR_STOP on
SET search_path TO sf, public;

-- ───────────────────────────────────────────────────────────────────────────
-- BUG 1 — GPS fotos: detectar valores fuera de rango físico (|lat|>90 o |lng|>180)
-- ───────────────────────────────────────────────────────────────────────────
-- Escaneo: ¿cuántas filas tienen valores corruptos?
SELECT
    COUNT(*) FILTER (WHERE ABS(coordenada_y) > 90)  AS fotos_lat_corrupta,
    COUNT(*) FILTER (WHERE ABS(coordenada_x) > 180) AS fotos_lng_corrupta,
    COUNT(*)                                         AS total_fotos_con_gps
FROM sf.inspeccion_fotografia
WHERE coordenada_x IS NOT NULL OR coordenada_y IS NOT NULL;

-- Distribución por dígitos enteros: ayuda a decidir el factor de división.
-- Si todos los valores están en 6 dígitos (p.ej. -334569) el factor es 10000.
-- Si hay mezcla, conviene revisar caso a caso.
SELECT
    FLOOR(LOG(ABS(coordenada_y)))::int + 1 AS digitos_enteros_lat,
    COUNT(*) AS filas,
    MIN(coordenada_y) AS min_lat,
    MAX(coordenada_y) AS max_lat
FROM sf.inspeccion_fotografia
WHERE coordenada_y IS NOT NULL AND ABS(coordenada_y) > 90
GROUP BY 1
ORDER BY 1;

SELECT
    FLOOR(LOG(ABS(coordenada_x)))::int + 1 AS digitos_enteros_lng,
    COUNT(*) AS filas,
    MIN(coordenada_x) AS min_lng,
    MAX(coordenada_x) AS max_lng
FROM sf.inspeccion_fotografia
WHERE coordenada_x IS NOT NULL AND ABS(coordenada_x) > 180
GROUP BY 1
ORDER BY 1;

-- Muestra de 20 filas corruptas con su corrección propuesta asumiendo
-- factor 10000 (4 decimales). AJUSTAR el factor en la UPDATE real si la
-- distribución anterior sugiere otro número de decimales.
SELECT
    id,
    inspeccion_id,
    nombre_archivo,
    coordenada_y                         AS lat_actual,
    coordenada_x                         AS lng_actual,
    coordenada_y / 10000                 AS lat_corregida_factor_10k,
    coordenada_x / 10000                 AS lng_corregida_factor_10k
FROM sf.inspeccion_fotografia
WHERE ABS(coordenada_y) > 90 OR ABS(coordenada_x) > 180
ORDER BY created_at DESC
LIMIT 20;

-- ─── UPDATE (comentado) ────────────────────────────────────────────────────
-- Verificar primero los SELECT anteriores. Descomentar y ejecutar dentro de
-- una transacción. Ajustar /10000 al factor correcto si la distribución lo
-- indica.
--
-- BEGIN;
-- UPDATE sf.inspeccion_fotografia
--     SET coordenada_y = coordenada_y / 10000
--     WHERE ABS(coordenada_y) > 90;
-- UPDATE sf.inspeccion_fotografia
--     SET coordenada_x = coordenada_x / 10000
--     WHERE ABS(coordenada_x) > 180;
-- -- Verificar que no queden corruptos:
-- SELECT COUNT(*) AS quedan_corruptos
--   FROM sf.inspeccion_fotografia
--  WHERE ABS(coordenada_y) > 90 OR ABS(coordenada_x) > 180;
-- COMMIT;
-- -- ROLLBACK si algo no cuadra.


-- ───────────────────────────────────────────────────────────────────────────
-- BUG 2 — inspeccion.total_respondidas = 0 con respuestas reales
-- ───────────────────────────────────────────────────────────────────────────
-- Escaneo: ¿cuántas inspecciones tienen el contador desincronizado?
SELECT
    COUNT(*) FILTER (WHERE i.total_respondidas <> r.real_count) AS inspecciones_desincronizadas,
    COUNT(*)                                                     AS total_inspecciones
FROM sf.inspeccion i
LEFT JOIN LATERAL (
    SELECT COUNT(*) AS real_count
    FROM sf.inspeccion_respuesta r
    WHERE r.inspeccion_id = i.id
) r ON TRUE;

-- Muestra de 20 inspecciones con contador incorrecto.
SELECT
    i.id,
    i.empresa_id,
    i.total_preguntas,
    i.total_respondidas    AS total_respondidas_actual,
    r.real_count           AS total_respondidas_real,
    i.created_at
FROM sf.inspeccion i
JOIN LATERAL (
    SELECT COUNT(*) AS real_count
    FROM sf.inspeccion_respuesta r
    WHERE r.inspeccion_id = i.id
) r ON TRUE
WHERE i.total_respondidas <> r.real_count
ORDER BY i.created_at DESC
LIMIT 20;

-- Escaneo adicional: total_preguntas también se vio en 0 para inspecciones
-- creadas antes del fix. Se repara contando las preguntas del flujo asociado,
-- excluyendo controles de foto y etiqueta (mismo criterio que SyncController).
SELECT
    LEFT(i.id::text, 8) AS insp,
    i.total_preguntas AS actual,
    (SELECT COUNT(*)
       FROM sf.flujo_pregunta p
      WHERE p.flujo_version_id = i.flujo_version_id
        AND p.tipo_control NOT IN ('foto_unica','fotos_multiples','etiqueta')) AS propuesto
FROM sf.inspeccion i
WHERE i.total_preguntas = 0
ORDER BY i.created_at DESC
LIMIT 20;

-- ─── UPDATE (comentado) ────────────────────────────────────────────────────
-- BEGIN;
-- -- (a) Recontar total_respondidas para inspecciones con respuestas reales
-- UPDATE sf.inspeccion i
--     SET total_respondidas = sub.real_count
-- FROM (
--     SELECT inspeccion_id, COUNT(*) AS real_count
--     FROM sf.inspeccion_respuesta
--     GROUP BY inspeccion_id
-- ) sub
-- WHERE i.id = sub.inspeccion_id
--   AND i.total_respondidas <> sub.real_count;
--
-- -- (b) Poner total_respondidas=0 donde no hay respuestas pero el contador > 0
-- UPDATE sf.inspeccion
--     SET total_respondidas = 0
-- WHERE total_respondidas <> 0
--   AND id NOT IN (SELECT DISTINCT inspeccion_id FROM sf.inspeccion_respuesta);
--
-- -- (c) Recontar total_preguntas desde el flujo asociado (excluye fotos/etiquetas)
-- UPDATE sf.inspeccion i
--     SET total_preguntas = sub.real_count
-- FROM (
--     SELECT i2.id AS inspeccion_id,
--            (SELECT COUNT(*)
--               FROM sf.flujo_pregunta p
--              WHERE p.flujo_version_id = i2.flujo_version_id
--                AND p.tipo_control NOT IN ('foto_unica','fotos_multiples','etiqueta')) AS real_count
--     FROM sf.inspeccion i2
--     WHERE i2.total_preguntas = 0
-- ) sub
-- WHERE i.id = sub.inspeccion_id
--   AND i.total_preguntas <> sub.real_count;
-- COMMIT;


-- ───────────────────────────────────────────────────────────────────────────
-- BUG 3 — inspeccion.total_fotografias desincronizado (pre-G8)
-- ───────────────────────────────────────────────────────────────────────────
-- Escaneo
SELECT
    COUNT(*) FILTER (WHERE i.total_fotografias <> COALESCE(f.real_count, 0)) AS inspecciones_fotos_desync,
    COUNT(*)                                                                  AS total_inspecciones
FROM sf.inspeccion i
LEFT JOIN (
    SELECT inspeccion_id, COUNT(*) AS real_count
    FROM sf.inspeccion_fotografia
    GROUP BY inspeccion_id
) f ON f.inspeccion_id = i.id;

-- Muestra de 20 inspecciones desincronizadas
SELECT
    i.id,
    i.empresa_id,
    i.total_fotografias                  AS total_fotografias_actual,
    COALESCE(f.real_count, 0)            AS total_fotografias_real,
    i.created_at
FROM sf.inspeccion i
LEFT JOIN (
    SELECT inspeccion_id, COUNT(*) AS real_count
    FROM sf.inspeccion_fotografia
    GROUP BY inspeccion_id
) f ON f.inspeccion_id = i.id
WHERE i.total_fotografias <> COALESCE(f.real_count, 0)
ORDER BY i.created_at DESC
LIMIT 20;

-- ─── UPDATE (comentado) ────────────────────────────────────────────────────
-- BEGIN;
-- UPDATE sf.inspeccion i
--     SET total_fotografias = COALESCE(sub.real_count, 0)
-- FROM (
--     SELECT i2.id AS inspeccion_id, COUNT(f.id) AS real_count
--     FROM sf.inspeccion i2
--     LEFT JOIN sf.inspeccion_fotografia f ON f.inspeccion_id = i2.id
--     GROUP BY i2.id
-- ) sub
-- WHERE i.id = sub.inspeccion_id
--   AND i.total_fotografias <> COALESCE(sub.real_count, 0);
-- COMMIT;


-- ───────────────────────────────────────────────────────────────────────────
-- VERIFICACIÓN FINAL POST-BACKFILL
-- ───────────────────────────────────────────────────────────────────────────
-- Ejecutar después de aplicar los 3 UPDATE. Debe retornar 0 en todas las filas.
SELECT
    (SELECT COUNT(*) FROM sf.inspeccion_fotografia
     WHERE ABS(coordenada_y) > 90 OR ABS(coordenada_x) > 180)          AS gps_corruptos,
    (SELECT COUNT(*) FROM sf.inspeccion i
     WHERE i.total_respondidas <> (
         SELECT COUNT(*) FROM sf.inspeccion_respuesta r
         WHERE r.inspeccion_id = i.id))                                 AS total_respondidas_desync,
    (SELECT COUNT(*) FROM sf.inspeccion i
     WHERE i.total_fotografias <> (
         SELECT COUNT(*) FROM sf.inspeccion_fotografia f
         WHERE f.inspeccion_id = i.id))                                 AS total_fotografias_desync;

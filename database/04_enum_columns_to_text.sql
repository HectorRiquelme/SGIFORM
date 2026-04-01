-- =============================================================================
-- MIGRACIÓN 04: Convertir columnas de tipo PostgreSQL ENUM nativo a TEXT
-- =============================================================================
-- Motivo: EF Core usa SnakeCaseEnumConverter que mapea enums C# a strings.
-- Npgsql envía los parámetros como NpgsqlDbType.Text; PostgreSQL rechaza esa
-- asignación en columnas de tipo ENUM nativo (error 42804: "expression is of
-- type text"). Al convertir a TEXT el driver puede insertar/actualizar
-- correctamente mientras la capa de aplicación sigue convirtiendo a snake_case.
-- Las restricciones CHECK garantizan la integridad sin perder los valores válidos.
-- =============================================================================

BEGIN;

-- ─── usuario.estado ───────────────────────────────────────────────────────────
ALTER TABLE sf.usuario
    ALTER COLUMN estado TYPE TEXT USING estado::text;

ALTER TABLE sf.usuario
    ADD CONSTRAINT chk_usuario_estado
    CHECK (estado IN ('activo','inactivo','bloqueado'));

-- ─── flujo_version.estado ─────────────────────────────────────────────────────
ALTER TABLE sf.flujo_version
    ALTER COLUMN estado TYPE TEXT USING estado::text;

ALTER TABLE sf.flujo_version
    ADD CONSTRAINT chk_flujo_version_estado
    CHECK (estado IN ('borrador','publicado','archivado'));

-- ─── flujo_pregunta.tipo_control ──────────────────────────────────────────────
ALTER TABLE sf.flujo_pregunta
    ALTER COLUMN tipo_control TYPE TEXT USING tipo_control::text;

ALTER TABLE sf.flujo_pregunta
    ADD CONSTRAINT chk_flujo_pregunta_tipo_control
    CHECK (tipo_control IN (
        'texto_corto','texto_largo','entero','decimal',
        'fecha','hora','fecha_hora','si_no',
        'seleccion_unica','seleccion_multiple','lista',
        'foto_unica','fotos_multiples','coordenadas','firma',
        'calculado','etiqueta','checkbox','qr_codigo','archivo'
    ));

-- ─── flujo_regla.operador ─────────────────────────────────────────────────────
ALTER TABLE sf.flujo_regla
    ALTER COLUMN operador TYPE TEXT USING operador::text;

ALTER TABLE sf.flujo_regla
    ADD CONSTRAINT chk_flujo_regla_operador
    CHECK (operador IN (
        'eq','neq','gt','lt','gte','lte',
        'contains','not_contains','in','not_in',
        'is_empty','is_not_empty','starts_with','ends_with'
    ));

-- ─── flujo_regla.accion ───────────────────────────────────────────────────────
ALTER TABLE sf.flujo_regla
    ALTER COLUMN accion TYPE TEXT USING accion::text;

ALTER TABLE sf.flujo_regla
    ADD CONSTRAINT chk_flujo_regla_accion
    CHECK (accion IN (
        'mostrar','ocultar','obligatorio','opcional',
        'saltar_seccion','bloquear_cierre','calcular',
        'asignar_valor','min_fotos','max_fotos'
    ));

-- ─── importacion_lote.estado ──────────────────────────────────────────────────
ALTER TABLE sf.importacion_lote
    ALTER COLUMN estado TYPE TEXT USING estado::text;

ALTER TABLE sf.importacion_lote
    ADD CONSTRAINT chk_importacion_lote_estado
    CHECK (estado IN (
        'pendiente','procesando','completado',
        'completado_con_errores','fallido'
    ));

-- ─── asignacion_inspeccion.estado ─────────────────────────────────────────────
ALTER TABLE sf.asignacion_inspeccion
    ALTER COLUMN estado TYPE TEXT USING estado::text;

ALTER TABLE sf.asignacion_inspeccion
    ADD CONSTRAINT chk_asignacion_estado
    CHECK (estado IN (
        'pendiente','asignada','descargada','en_ejecucion',
        'finalizada','sincronizada','observada','rechazada','cerrada'
    ));

-- ─── asignacion_inspeccion.prioridad ──────────────────────────────────────────
ALTER TABLE sf.asignacion_inspeccion
    ALTER COLUMN prioridad TYPE TEXT USING prioridad::text;

ALTER TABLE sf.asignacion_inspeccion
    ADD CONSTRAINT chk_asignacion_prioridad
    CHECK (prioridad IN ('baja','normal','alta','urgente'));

-- ─── inspeccion.estado ────────────────────────────────────────────────────────
ALTER TABLE sf.inspeccion
    ALTER COLUMN estado TYPE TEXT USING estado::text;

ALTER TABLE sf.inspeccion
    ADD CONSTRAINT chk_inspeccion_estado
    CHECK (estado IN (
        'borrador','en_progreso','completada','enviada',
        'aprobada','observada','rechazada'
    ));

-- ─── inspeccion_respuesta.tipo_control ────────────────────────────────────────
ALTER TABLE sf.inspeccion_respuesta
    ALTER COLUMN tipo_control TYPE TEXT USING tipo_control::text;

ALTER TABLE sf.inspeccion_respuesta
    ADD CONSTRAINT chk_respuesta_tipo_control
    CHECK (tipo_control IN (
        'texto_corto','texto_largo','entero','decimal',
        'fecha','hora','fecha_hora','si_no',
        'seleccion_unica','seleccion_multiple','lista',
        'foto_unica','fotos_multiples','coordenadas','firma',
        'calculado','etiqueta','checkbox','qr_codigo','archivo'
    ));

-- ─── inspeccion_historial.estado_anterior / estado_nuevo ─────────────────────
ALTER TABLE sf.inspeccion_historial
    ALTER COLUMN estado_anterior TYPE TEXT USING estado_anterior::text;

ALTER TABLE sf.inspeccion_historial
    ALTER COLUMN estado_nuevo TYPE TEXT USING estado_nuevo::text;

-- ─── sincronizacion_log.tipo ──────────────────────────────────────────────────
ALTER TABLE sf.sincronizacion_log
    ALTER COLUMN tipo TYPE TEXT USING tipo::text;

ALTER TABLE sf.sincronizacion_log
    ADD CONSTRAINT chk_sync_tipo
    CHECK (tipo IN ('download','upload','photos','confirm','full'));

-- ─── Eliminar tipos ENUM nativos ahora obsoletos ──────────────────────────────
-- (se puede omitir si existen dependencias externas; solo limpia el schema)
DROP TYPE IF EXISTS sf.estado_usuario          CASCADE;
DROP TYPE IF EXISTS sf.estado_flujo_version    CASCADE;
DROP TYPE IF EXISTS sf.tipo_control            CASCADE;
DROP TYPE IF EXISTS sf.operador_regla          CASCADE;
DROP TYPE IF EXISTS sf.accion_regla            CASCADE;
DROP TYPE IF EXISTS sf.estado_importacion      CASCADE;
DROP TYPE IF EXISTS sf.estado_asignacion       CASCADE;
DROP TYPE IF EXISTS sf.prioridad               CASCADE;
DROP TYPE IF EXISTS sf.estado_inspeccion       CASCADE;
DROP TYPE IF EXISTS sf.tipo_sync               CASCADE;

COMMIT;

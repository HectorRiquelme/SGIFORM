-- =============================================================================
-- Migración 03: Soporte de refresh token para operadores móviles
-- Versión: 1.1.0
-- Descripción:
--   Hace usuario_id nullable en refresh_token y agrega operador_id,
--   permitiendo que los tokens móviles sean revocables al igual que los web.
--   Antes: JWT de 7 días no revocable para operadores.
--   Después: JWT de 24h + refresh token rotativo de 30 días (revocable).
-- =============================================================================

BEGIN;

SET search_path TO sf, public;

-- 1. Hacer usuario_id nullable (tokens de operadores no tienen usuario_id)
ALTER TABLE sf.refresh_token
    ALTER COLUMN usuario_id DROP NOT NULL;

-- 2. Agregar columna operador_id (FK a operador)
ALTER TABLE sf.refresh_token
    ADD COLUMN IF NOT EXISTS operador_id UUID
        REFERENCES sf.operador(id) ON DELETE CASCADE;

-- 3. Restricción: al menos uno de usuario_id u operador_id debe ser no nulo
ALTER TABLE sf.refresh_token
    ADD CONSTRAINT chk_rt_sujeto_requerido
        CHECK (usuario_id IS NOT NULL OR operador_id IS NOT NULL);

-- 4. Índice para consultas por operador (búsqueda de tokens durante refresh)
CREATE INDEX IF NOT EXISTS idx_rt_operador_id
    ON sf.refresh_token(operador_id)
    WHERE operador_id IS NOT NULL;

-- 5. Comentarios documentados
COMMENT ON COLUMN sf.refresh_token.usuario_id IS
    'FK a usuario web. Nulo si el token pertenece a un operador móvil.';
COMMENT ON COLUMN sf.refresh_token.operador_id IS
    'FK a operador móvil. Nulo si el token pertenece a un usuario web.';

COMMIT;

-- Verificación post-migración
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'sf'
          AND table_name = 'refresh_token'
          AND column_name = 'operador_id'
    ) THEN
        RAISE EXCEPTION 'Migración 03 falló: columna operador_id no existe';
    END IF;
    RAISE NOTICE 'Migración 03 aplicada correctamente.';
END $$;

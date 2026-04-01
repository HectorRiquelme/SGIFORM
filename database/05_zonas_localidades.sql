-- ============================================================
-- 05_zonas_localidades.sql
-- Módulo: Zonas y Localidades
-- Permite clasificar servicios, operadores y asignaciones
-- con una jerarquía Zona → Localidad por empresa.
-- ============================================================

BEGIN;

-- ── ZONA ────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS sf.zona (
    id           UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    empresa_id   UUID NOT NULL REFERENCES sf.empresa(id) ON DELETE CASCADE,
    codigo       VARCHAR(50) NOT NULL,
    nombre       VARCHAR(200) NOT NULL,
    descripcion  TEXT,
    activo       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at   TIMESTAMPTZ,
    UNIQUE (empresa_id, codigo)
);

CREATE INDEX IF NOT EXISTS idx_zona_empresa ON sf.zona(empresa_id);

-- ── LOCALIDAD ───────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS sf.localidad (
    id           UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    empresa_id   UUID NOT NULL REFERENCES sf.empresa(id) ON DELETE CASCADE,
    zona_id      UUID REFERENCES sf.zona(id) ON DELETE SET NULL,
    codigo       VARCHAR(50) NOT NULL,
    nombre       VARCHAR(200) NOT NULL,
    activo       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at   TIMESTAMPTZ,
    UNIQUE (empresa_id, codigo)
);

CREATE INDEX IF NOT EXISTS idx_localidad_empresa ON sf.localidad(empresa_id);
CREATE INDEX IF NOT EXISTS idx_localidad_zona    ON sf.localidad(zona_id);

COMMIT;

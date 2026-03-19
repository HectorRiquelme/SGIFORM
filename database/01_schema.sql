-- =============================================================================
-- SanitasField - Schema PostgreSQL completo
-- Version: 1.0.0
-- Descripción: Sistema de inspecciones técnicas en terreno para sanitarias
-- =============================================================================

-- Extensiones requeridas
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";    -- búsqueda texto similaridad
CREATE EXTENSION IF NOT EXISTS "unaccent";   -- búsqueda sin tildes

-- =============================================================================
-- SCHEMA principal
-- =============================================================================
CREATE SCHEMA IF NOT EXISTS sf;
SET search_path TO sf, public;

-- =============================================================================
-- ENUMS
-- =============================================================================

CREATE TYPE sf.estado_usuario AS ENUM ('activo', 'inactivo', 'bloqueado');

CREATE TYPE sf.rol_sistema AS ENUM (
    'administrador',
    'supervisor',
    'operador',
    'auditor',
    'cliente_consulta'
);

CREATE TYPE sf.tipo_control AS ENUM (
    'texto_corto',
    'texto_largo',
    'entero',
    'decimal',
    'fecha',
    'hora',
    'fecha_hora',
    'si_no',
    'seleccion_unica',
    'seleccion_multiple',
    'lista',
    'foto_unica',
    'fotos_multiples',
    'coordenadas',
    'firma',
    'calculado',
    'etiqueta',
    'checkbox',
    'qr_codigo',
    'archivo'
);

CREATE TYPE sf.operador_regla AS ENUM (
    'eq',        -- igual
    'neq',       -- distinto
    'gt',        -- mayor que
    'lt',        -- menor que
    'gte',       -- mayor o igual
    'lte',       -- menor o igual
    'contains',  -- contiene
    'not_contains',
    'in',        -- está en lista
    'not_in',
    'is_empty',
    'is_not_empty',
    'starts_with',
    'ends_with'
);

CREATE TYPE sf.accion_regla AS ENUM (
    'mostrar',
    'ocultar',
    'obligatorio',
    'opcional',
    'saltar_seccion',
    'bloquear_cierre',
    'calcular',
    'asignar_valor',
    'min_fotos',
    'max_fotos'
);

CREATE TYPE sf.estado_flujo_version AS ENUM ('borrador', 'publicado', 'archivado');

CREATE TYPE sf.estado_importacion AS ENUM (
    'pendiente',
    'procesando',
    'completado',
    'completado_con_errores',
    'fallido'
);

CREATE TYPE sf.estado_asignacion AS ENUM (
    'pendiente',
    'asignada',
    'descargada',
    'en_ejecucion',
    'finalizada',
    'sincronizada',
    'observada',
    'rechazada',
    'cerrada'
);

CREATE TYPE sf.estado_inspeccion AS ENUM (
    'borrador',
    'en_progreso',
    'completada',
    'enviada',
    'aprobada',
    'observada',
    'rechazada'
);

CREATE TYPE sf.tipo_sync AS ENUM ('download', 'upload', 'photos', 'confirm', 'full');

CREATE TYPE sf.prioridad AS ENUM ('baja', 'normal', 'alta', 'urgente');

-- =============================================================================
-- TABLA: empresa  (tenant raíz del sistema multitenant)
-- =============================================================================
CREATE TABLE sf.empresa (
    id              UUID        NOT NULL DEFAULT uuid_generate_v4(),
    codigo          VARCHAR(50) NOT NULL,
    nombre          VARCHAR(200) NOT NULL,
    rut             VARCHAR(20),
    logo_url        TEXT,
    tenant_slug     VARCHAR(100) NOT NULL,  -- para subdomain o header
    activo          BOOLEAN     NOT NULL DEFAULT true,
    plan            VARCHAR(50) NOT NULL DEFAULT 'standard',
    configuracion   JSONB       NOT NULL DEFAULT '{}',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at      TIMESTAMPTZ,

    CONSTRAINT pk_empresa PRIMARY KEY (id),
    CONSTRAINT uq_empresa_codigo UNIQUE (codigo),
    CONSTRAINT uq_empresa_tenant_slug UNIQUE (tenant_slug)
);

COMMENT ON TABLE sf.empresa IS 'Tenant raíz. Cada sanitaria es una empresa independiente.';
COMMENT ON COLUMN sf.empresa.tenant_slug IS 'Identificador único para enrutamiento multitenant (header X-Tenant-Slug)';

-- =============================================================================
-- TABLA: rol
-- =============================================================================
CREATE TABLE sf.rol (
    id          UUID        NOT NULL DEFAULT uuid_generate_v4(),
    empresa_id  UUID        NOT NULL,
    nombre      VARCHAR(100) NOT NULL,
    codigo      VARCHAR(50)  NOT NULL,
    descripcion TEXT,
    es_sistema  BOOLEAN     NOT NULL DEFAULT false,  -- roles no editables
    activo      BOOLEAN     NOT NULL DEFAULT true,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_rol PRIMARY KEY (id),
    CONSTRAINT fk_rol_empresa FOREIGN KEY (empresa_id) REFERENCES sf.empresa(id),
    CONSTRAINT uq_rol_empresa_codigo UNIQUE (empresa_id, codigo)
);

-- =============================================================================
-- TABLA: permiso
-- =============================================================================
CREATE TABLE sf.permiso (
    id          UUID        NOT NULL DEFAULT uuid_generate_v4(),
    modulo      VARCHAR(100) NOT NULL,
    accion      VARCHAR(100) NOT NULL,  -- create, read, update, delete, approve, export
    descripcion TEXT,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_permiso PRIMARY KEY (id),
    CONSTRAINT uq_permiso_modulo_accion UNIQUE (modulo, accion)
);

-- =============================================================================
-- TABLA: rol_permiso  (relación M:N)
-- =============================================================================
CREATE TABLE sf.rol_permiso (
    rol_id      UUID NOT NULL,
    permiso_id  UUID NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_rol_permiso PRIMARY KEY (rol_id, permiso_id),
    CONSTRAINT fk_rp_rol     FOREIGN KEY (rol_id)     REFERENCES sf.rol(id)     ON DELETE CASCADE,
    CONSTRAINT fk_rp_permiso FOREIGN KEY (permiso_id) REFERENCES sf.permiso(id) ON DELETE CASCADE
);

-- =============================================================================
-- TABLA: usuario
-- =============================================================================
CREATE TABLE sf.usuario (
    id              UUID        NOT NULL DEFAULT uuid_generate_v4(),
    empresa_id      UUID        NOT NULL,
    rol_id          UUID        NOT NULL,
    email           VARCHAR(255) NOT NULL,
    password_hash   VARCHAR(500) NOT NULL,
    nombre          VARCHAR(150) NOT NULL,
    apellido        VARCHAR(150) NOT NULL,
    telefono        VARCHAR(30),
    avatar_url      TEXT,
    estado          sf.estado_usuario NOT NULL DEFAULT 'activo',
    ultimo_acceso   TIMESTAMPTZ,
    intentos_fallidos INTEGER   NOT NULL DEFAULT 0,
    bloqueado_hasta TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at      TIMESTAMPTZ,

    CONSTRAINT pk_usuario PRIMARY KEY (id),
    CONSTRAINT fk_usuario_empresa FOREIGN KEY (empresa_id) REFERENCES sf.empresa(id),
    CONSTRAINT fk_usuario_rol     FOREIGN KEY (rol_id)     REFERENCES sf.rol(id),
    CONSTRAINT uq_usuario_email   UNIQUE (email)  -- global, email único en sistema
);

COMMENT ON TABLE sf.usuario IS 'Usuarios del sistema web y administrativo.';

-- =============================================================================
-- TABLA: refresh_token  (seguridad JWT)
-- =============================================================================
CREATE TABLE sf.refresh_token (
    id          UUID        NOT NULL DEFAULT uuid_generate_v4(),
    usuario_id  UUID        NOT NULL,
    token       VARCHAR(500) NOT NULL,
    expira_en   TIMESTAMPTZ NOT NULL,
    revocado    BOOLEAN     NOT NULL DEFAULT false,
    ip_origen   INET,
    user_agent  TEXT,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_refresh_token PRIMARY KEY (id),
    CONSTRAINT fk_rt_usuario FOREIGN KEY (usuario_id) REFERENCES sf.usuario(id) ON DELETE CASCADE,
    CONSTRAINT uq_refresh_token UNIQUE (token)
);

-- =============================================================================
-- TABLA: operador  (usuario de campo, acceso solo a la app móvil)
-- =============================================================================
CREATE TABLE sf.operador (
    id                      UUID        NOT NULL DEFAULT uuid_generate_v4(),
    empresa_id              UUID        NOT NULL,
    usuario_id              UUID,       -- si tiene acceso web también
    codigo_operador         VARCHAR(50) NOT NULL,
    nombre                  VARCHAR(150) NOT NULL,
    apellido                VARCHAR(150) NOT NULL,
    rut                     VARCHAR(20),
    telefono                VARCHAR(30),
    email                   VARCHAR(255),
    zona                    VARCHAR(100),
    localidad               VARCHAR(200),
    password_hash           VARCHAR(500) NOT NULL,  -- login móvil
    activo                  BOOLEAN     NOT NULL DEFAULT true,
    fecha_ultima_sync       TIMESTAMPTZ,
    device_id_registrado    VARCHAR(200),           -- binding de dispositivo
    app_version_ultima      VARCHAR(50),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at              TIMESTAMPTZ,

    CONSTRAINT pk_operador PRIMARY KEY (id),
    CONSTRAINT fk_operador_empresa  FOREIGN KEY (empresa_id) REFERENCES sf.empresa(id),
    CONSTRAINT fk_operador_usuario  FOREIGN KEY (usuario_id) REFERENCES sf.usuario(id),
    CONSTRAINT uq_operador_empresa_codigo UNIQUE (empresa_id, codigo_operador)
);

COMMENT ON TABLE sf.operador IS 'Operadores de terreno. Login exclusivo en app móvil.';

-- =============================================================================
-- TABLA: tipo_inspeccion
-- =============================================================================
CREATE TABLE sf.tipo_inspeccion (
    id                      UUID        NOT NULL DEFAULT uuid_generate_v4(),
    empresa_id              UUID        NOT NULL,
    codigo                  VARCHAR(50) NOT NULL,
    nombre                  VARCHAR(200) NOT NULL,
    descripcion             TEXT,
    activo                  BOOLEAN     NOT NULL DEFAULT true,
    flujo_version_id_def    UUID,       -- flujo por defecto (FK circular, se agrega luego)
    icono                   VARCHAR(100),
    color                   VARCHAR(10),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at              TIMESTAMPTZ,

    CONSTRAINT pk_tipo_inspeccion PRIMARY KEY (id),
    CONSTRAINT fk_ti_empresa FOREIGN KEY (empresa_id) REFERENCES sf.empresa(id),
    CONSTRAINT uq_ti_empresa_codigo UNIQUE (empresa_id, codigo)
);

-- =============================================================================
-- TABLA: flujo
-- =============================================================================
CREATE TABLE sf.flujo (
    id                  UUID        NOT NULL DEFAULT uuid_generate_v4(),
    empresa_id          UUID        NOT NULL,
    tipo_inspeccion_id  UUID,
    nombre              VARCHAR(200) NOT NULL,
    descripcion         TEXT,
    activo              BOOLEAN     NOT NULL DEFAULT true,
    created_by          UUID,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at          TIMESTAMPTZ,

    CONSTRAINT pk_flujo PRIMARY KEY (id),
    CONSTRAINT fk_flujo_empresa         FOREIGN KEY (empresa_id)         REFERENCES sf.empresa(id),
    CONSTRAINT fk_flujo_tipo_inspeccion FOREIGN KEY (tipo_inspeccion_id) REFERENCES sf.tipo_inspeccion(id),
    CONSTRAINT fk_flujo_created_by      FOREIGN KEY (created_by)         REFERENCES sf.usuario(id)
);

-- =============================================================================
-- TABLA: flujo_version  (versionado inmutable una vez publicado)
-- =============================================================================
CREATE TABLE sf.flujo_version (
    id                  UUID        NOT NULL DEFAULT uuid_generate_v4(),
    flujo_id            UUID        NOT NULL,
    numero_version      INTEGER     NOT NULL DEFAULT 1,
    estado              sf.estado_flujo_version NOT NULL DEFAULT 'borrador',
    descripcion_cambio  TEXT,
    publicado_por       UUID,       -- usuario_id
    publicado_en        TIMESTAMPTZ,
    configuracion       JSONB       NOT NULL DEFAULT '{}',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_flujo_version PRIMARY KEY (id),
    CONSTRAINT fk_fv_flujo         FOREIGN KEY (flujo_id)       REFERENCES sf.flujo(id)    ON DELETE CASCADE,
    CONSTRAINT fk_fv_publicado_por FOREIGN KEY (publicado_por)  REFERENCES sf.usuario(id),
    CONSTRAINT uq_fv_flujo_version UNIQUE (flujo_id, numero_version)
);

COMMENT ON TABLE sf.flujo_version IS 'Versiones inmutables del flujo. Una vez publicada no se modifica.';

-- =============================================================================
-- TABLA: flujo_seccion
-- =============================================================================
CREATE TABLE sf.flujo_seccion (
    id                  UUID        NOT NULL DEFAULT uuid_generate_v4(),
    flujo_version_id    UUID        NOT NULL,
    codigo              VARCHAR(100) NOT NULL,
    titulo              VARCHAR(300) NOT NULL,
    descripcion         TEXT,
    orden               INTEGER     NOT NULL DEFAULT 0,
    visible             BOOLEAN     NOT NULL DEFAULT true,
    condicional_json    JSONB,      -- condición para mostrar/ocultar sección
    icono               VARCHAR(100),
    color               VARCHAR(10),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_flujo_seccion PRIMARY KEY (id),
    CONSTRAINT fk_fs_flujo_version FOREIGN KEY (flujo_version_id) REFERENCES sf.flujo_version(id) ON DELETE CASCADE,
    CONSTRAINT uq_fs_version_codigo UNIQUE (flujo_version_id, codigo)
);

-- =============================================================================
-- TABLA: flujo_pregunta
-- =============================================================================
CREATE TABLE sf.flujo_pregunta (
    id                  UUID        NOT NULL DEFAULT uuid_generate_v4(),
    flujo_version_id    UUID        NOT NULL,
    seccion_id          UUID        NOT NULL,
    codigo              VARCHAR(100) NOT NULL,
    texto               TEXT        NOT NULL,
    tipo_control        sf.tipo_control NOT NULL,
    placeholder         VARCHAR(300),
    ayuda               TEXT,
    obligatorio         BOOLEAN     NOT NULL DEFAULT false,
    orden               INTEGER     NOT NULL DEFAULT 0,
    visible             BOOLEAN     NOT NULL DEFAULT true,
    editable            BOOLEAN     NOT NULL DEFAULT true,
    valor_por_defecto   TEXT,
    validaciones_json   JSONB       NOT NULL DEFAULT '{}',
    configuracion_json  JSONB       NOT NULL DEFAULT '{}',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_flujo_pregunta PRIMARY KEY (id),
    CONSTRAINT fk_fp_flujo_version FOREIGN KEY (flujo_version_id) REFERENCES sf.flujo_version(id) ON DELETE CASCADE,
    CONSTRAINT fk_fp_seccion       FOREIGN KEY (seccion_id)        REFERENCES sf.flujo_seccion(id) ON DELETE CASCADE,
    CONSTRAINT uq_fp_version_codigo UNIQUE (flujo_version_id, codigo)
);

COMMENT ON COLUMN sf.flujo_pregunta.validaciones_json IS
    '{"min":0,"max":100,"min_length":null,"max_length":500,"regex":null,"min_fotos":1,"max_fotos":5}';
COMMENT ON COLUMN sf.flujo_pregunta.configuracion_json IS
    'Configuración específica por tipo: opciones de cámara, fórmulas, formato de fecha, etc.';

-- =============================================================================
-- TABLA: flujo_opcion  (opciones de selección unica/multiple/lista)
-- =============================================================================
CREATE TABLE sf.flujo_opcion (
    id              UUID        NOT NULL DEFAULT uuid_generate_v4(),
    pregunta_id     UUID        NOT NULL,
    codigo          VARCHAR(100) NOT NULL,
    texto           VARCHAR(500) NOT NULL,
    orden           INTEGER     NOT NULL DEFAULT 0,
    activo          BOOLEAN     NOT NULL DEFAULT true,
    valor_numerico  DECIMAL(18,6),  -- para cálculos
    metadata_json   JSONB,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_flujo_opcion PRIMARY KEY (id),
    CONSTRAINT fk_fo_pregunta FOREIGN KEY (pregunta_id) REFERENCES sf.flujo_pregunta(id) ON DELETE CASCADE,
    CONSTRAINT uq_fo_pregunta_codigo UNIQUE (pregunta_id, codigo)
);

-- =============================================================================
-- TABLA: flujo_regla  (lógica condicional entre preguntas)
-- =============================================================================
CREATE TABLE sf.flujo_regla (
    id                      UUID        NOT NULL DEFAULT uuid_generate_v4(),
    flujo_version_id        UUID        NOT NULL,
    codigo                  VARCHAR(100),
    descripcion             TEXT,
    pregunta_origen_id      UUID        NOT NULL,
    operador                sf.operador_regla NOT NULL,
    valor_comparacion       TEXT,       -- valor contra el que se evalúa
    valor_comparacion_json  JSONB,      -- para operador 'in' / comparaciones complejas
    accion                  sf.accion_regla NOT NULL,
    pregunta_destino_id     UUID,
    seccion_destino_id      UUID,
    parametros_json         JSONB       NOT NULL DEFAULT '{}',
    orden                   INTEGER     NOT NULL DEFAULT 0,
    activo                  BOOLEAN     NOT NULL DEFAULT true,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_flujo_regla PRIMARY KEY (id),
    CONSTRAINT fk_fr_flujo_version      FOREIGN KEY (flujo_version_id)   REFERENCES sf.flujo_version(id)  ON DELETE CASCADE,
    CONSTRAINT fk_fr_pregunta_origen    FOREIGN KEY (pregunta_origen_id) REFERENCES sf.flujo_pregunta(id),
    CONSTRAINT fk_fr_pregunta_destino   FOREIGN KEY (pregunta_destino_id) REFERENCES sf.flujo_pregunta(id),
    CONSTRAINT fk_fr_seccion_destino    FOREIGN KEY (seccion_destino_id) REFERENCES sf.flujo_seccion(id),
    CONSTRAINT ck_fr_destino CHECK (
        pregunta_destino_id IS NOT NULL OR seccion_destino_id IS NOT NULL
    )
);

COMMENT ON TABLE sf.flujo_regla IS
    'Reglas de lógica condicional. EJ: si medidor_visible=No → mostrar motivo_no_visible';

-- =============================================================================
-- FK circular: tipo_inspeccion → flujo_version
-- =============================================================================
ALTER TABLE sf.tipo_inspeccion
    ADD CONSTRAINT fk_ti_flujo_version_def
    FOREIGN KEY (flujo_version_id_def) REFERENCES sf.flujo_version(id)
    DEFERRABLE INITIALLY DEFERRED;

-- =============================================================================
-- TABLA: catalogo  (tablas de dominio configurables)
-- =============================================================================
CREATE TABLE sf.catalogo (
    id          UUID        NOT NULL DEFAULT uuid_generate_v4(),
    empresa_id  UUID,       -- NULL = catálogo global del sistema
    tipo        VARCHAR(100) NOT NULL,  -- 'marca_medidor', 'diametro', 'estado_servicio', etc.
    codigo      VARCHAR(100) NOT NULL,
    texto       VARCHAR(300) NOT NULL,
    orden       INTEGER     NOT NULL DEFAULT 0,
    activo      BOOLEAN     NOT NULL DEFAULT true,
    metadata    JSONB,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_catalogo PRIMARY KEY (id),
    CONSTRAINT uq_catalogo_tipo_codigo UNIQUE (empresa_id, tipo, codigo)
);

-- =============================================================================
-- TABLA: importacion_lote
-- =============================================================================
CREATE TABLE sf.importacion_lote (
    id                  UUID        NOT NULL DEFAULT uuid_generate_v4(),
    empresa_id          UUID        NOT NULL,
    nombre_archivo      VARCHAR(500) NOT NULL,
    nombre_original     VARCHAR(500) NOT NULL,
    hash_archivo        VARCHAR(64),    -- SHA256 para detectar duplicados
    total_filas         INTEGER,
    filas_validas       INTEGER         DEFAULT 0,
    filas_error         INTEGER         DEFAULT 0,
    filas_omitidas      INTEGER         DEFAULT 0,
    estado              sf.estado_importacion NOT NULL DEFAULT 'pendiente',
    tipo_inspeccion_id  UUID,
    flujo_version_id    UUID,
    usuario_id          UUID            NOT NULL,  -- quien subió el archivo
    notas               TEXT,
    error_general       TEXT,
    configuracion_mapeo JSONB           NOT NULL DEFAULT '{}',  -- mapeo de columnas
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT now(),
    procesado_en        TIMESTAMPTZ,
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT now(),

    CONSTRAINT pk_importacion_lote PRIMARY KEY (id),
    CONSTRAINT fk_il_empresa        FOREIGN KEY (empresa_id)        REFERENCES sf.empresa(id),
    CONSTRAINT fk_il_tipo_insp      FOREIGN KEY (tipo_inspeccion_id) REFERENCES sf.tipo_inspeccion(id),
    CONSTRAINT fk_il_flujo_version  FOREIGN KEY (flujo_version_id)  REFERENCES sf.flujo_version(id),
    CONSTRAINT fk_il_usuario        FOREIGN KEY (usuario_id)        REFERENCES sf.usuario(id)
);

-- =============================================================================
-- TABLA: importacion_detalle  (log de errores por fila)
-- =============================================================================
CREATE TABLE sf.importacion_detalle (
    id              UUID        NOT NULL DEFAULT uuid_generate_v4(),
    lote_id         UUID        NOT NULL,
    numero_fila     INTEGER     NOT NULL,
    estado          VARCHAR(20) NOT NULL DEFAULT 'error',  -- ok, error, omitido
    errores_json    JSONB,       -- lista de errores por columna
    datos_originales JSONB,     -- fila completa original
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_importacion_detalle PRIMARY KEY (id),
    CONSTRAINT fk_id_lote FOREIGN KEY (lote_id) REFERENCES sf.importacion_lote(id) ON DELETE CASCADE
);

-- =============================================================================
-- TABLA: servicio_inspeccion  (entidad de trabajo importada desde Excel)
-- =============================================================================
CREATE TABLE sf.servicio_inspeccion (
    id                  UUID        NOT NULL DEFAULT uuid_generate_v4(),
    empresa_id          UUID        NOT NULL,
    importacion_lote_id UUID,
    -- Campos del Excel (mapeados exactamente)
    id_servicio         VARCHAR(100) NOT NULL,
    numero_medidor      VARCHAR(100),
    marca               VARCHAR(150),
    diametro            VARCHAR(50),
    direccion           TEXT,
    nombre_cliente      VARCHAR(300),
    coordenada_x        DECIMAL(18, 8),
    coordenada_y        DECIMAL(18, 8),
    lote                VARCHAR(100),
    localidad           VARCHAR(200),
    ruta                VARCHAR(100),
    libreta             VARCHAR(100),
    observacion_libre   TEXT,
    -- Control
    activo              BOOLEAN     NOT NULL DEFAULT true,
    tiene_asignacion    BOOLEAN     NOT NULL DEFAULT false,  -- índice rápido
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_servicio_inspeccion PRIMARY KEY (id),
    CONSTRAINT fk_si_empresa    FOREIGN KEY (empresa_id)          REFERENCES sf.empresa(id),
    CONSTRAINT fk_si_lote       FOREIGN KEY (importacion_lote_id) REFERENCES sf.importacion_lote(id),
    CONSTRAINT uq_si_empresa_id_servicio UNIQUE (empresa_id, id_servicio)
);

COMMENT ON TABLE sf.servicio_inspeccion IS
    'Entidad de trabajo. Cada fila importada desde Excel representa un servicio a inspeccionar.';

-- =============================================================================
-- TABLA: asignacion_inspeccion
-- =============================================================================
CREATE TABLE sf.asignacion_inspeccion (
    id                      UUID        NOT NULL DEFAULT uuid_generate_v4(),
    empresa_id              UUID        NOT NULL,
    servicio_inspeccion_id  UUID        NOT NULL,
    operador_id             UUID        NOT NULL,
    tipo_inspeccion_id      UUID        NOT NULL,
    flujo_version_id        UUID        NOT NULL,
    estado                  sf.estado_asignacion NOT NULL DEFAULT 'pendiente',
    prioridad               sf.prioridad NOT NULL DEFAULT 'normal',
    fecha_asignacion        TIMESTAMPTZ NOT NULL DEFAULT now(),
    fecha_inicio_esperada   DATE,
    fecha_fin_esperada      DATE,
    fecha_descarga          TIMESTAMPTZ,
    fecha_inicio_ejecucion  TIMESTAMPTZ,
    fecha_finalizacion      TIMESTAMPTZ,
    observaciones           TEXT,
    asignado_por            UUID        NOT NULL,   -- usuario_id
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at              TIMESTAMPTZ,

    CONSTRAINT pk_asignacion_inspeccion PRIMARY KEY (id),
    CONSTRAINT fk_ai_empresa        FOREIGN KEY (empresa_id)             REFERENCES sf.empresa(id),
    CONSTRAINT fk_ai_servicio       FOREIGN KEY (servicio_inspeccion_id) REFERENCES sf.servicio_inspeccion(id),
    CONSTRAINT fk_ai_operador       FOREIGN KEY (operador_id)            REFERENCES sf.operador(id),
    CONSTRAINT fk_ai_tipo_insp      FOREIGN KEY (tipo_inspeccion_id)     REFERENCES sf.tipo_inspeccion(id),
    CONSTRAINT fk_ai_flujo_version  FOREIGN KEY (flujo_version_id)       REFERENCES sf.flujo_version(id),
    CONSTRAINT fk_ai_asignado_por   FOREIGN KEY (asignado_por)           REFERENCES sf.usuario(id)
);

COMMENT ON TABLE sf.asignacion_inspeccion IS
    'Asignación de un servicio a un operador con flujo específico.';

-- =============================================================================
-- TABLA: inspeccion  (cabecera de la inspección ejecutada)
-- =============================================================================
CREATE TABLE sf.inspeccion (
    id                      UUID        NOT NULL DEFAULT uuid_generate_v4(),
    empresa_id              UUID        NOT NULL,
    asignacion_id           UUID        NOT NULL,
    operador_id             UUID        NOT NULL,
    servicio_inspeccion_id  UUID        NOT NULL,
    flujo_version_id        UUID        NOT NULL,
    estado                  sf.estado_inspeccion NOT NULL DEFAULT 'borrador',
    -- Timestamps de ejecución
    fecha_inicio            TIMESTAMPTZ,
    fecha_fin               TIMESTAMPTZ,
    duracion_segundos       INTEGER,
    -- GPS inicio/fin
    coord_x_inicio          DECIMAL(18, 8),
    coord_y_inicio          DECIMAL(18, 8),
    coord_x_fin             DECIMAL(18, 8),
    coord_y_fin             DECIMAL(18, 8),
    precision_gps           DECIMAL(10, 2),  -- metros
    -- Control de dispositivo
    device_id               VARCHAR(200),
    app_version             VARCHAR(50),
    -- Sincronización
    sincronizado_en         TIMESTAMPTZ,
    revision_por            UUID,            -- usuario supervisor
    revision_en             TIMESTAMPTZ,
    revision_observacion    TEXT,
    -- Totales calculados
    total_preguntas         INTEGER         DEFAULT 0,
    total_respondidas       INTEGER         DEFAULT 0,
    total_fotografias       INTEGER         DEFAULT 0,
    -- Auditoría
    created_at              TIMESTAMPTZ     NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ     NOT NULL DEFAULT now(),

    CONSTRAINT pk_inspeccion PRIMARY KEY (id),
    CONSTRAINT fk_insp_empresa    FOREIGN KEY (empresa_id)             REFERENCES sf.empresa(id),
    CONSTRAINT fk_insp_asignacion FOREIGN KEY (asignacion_id)          REFERENCES sf.asignacion_inspeccion(id),
    CONSTRAINT fk_insp_operador   FOREIGN KEY (operador_id)            REFERENCES sf.operador(id),
    CONSTRAINT fk_insp_servicio   FOREIGN KEY (servicio_inspeccion_id) REFERENCES sf.servicio_inspeccion(id),
    CONSTRAINT fk_insp_flujo      FOREIGN KEY (flujo_version_id)       REFERENCES sf.flujo_version(id),
    CONSTRAINT fk_insp_revision   FOREIGN KEY (revision_por)           REFERENCES sf.usuario(id)
);

-- =============================================================================
-- TABLA: inspeccion_respuesta
-- =============================================================================
CREATE TABLE sf.inspeccion_respuesta (
    id                  UUID        NOT NULL DEFAULT uuid_generate_v4(),
    inspeccion_id       UUID        NOT NULL,
    pregunta_id         UUID        NOT NULL,
    tipo_control        sf.tipo_control NOT NULL,
    -- Valores por tipo (solo uno estará poblado)
    valor_texto         TEXT,
    valor_entero        BIGINT,
    valor_decimal       DECIMAL(18, 6),
    valor_fecha         DATE,
    valor_hora          TIME,
    valor_fecha_hora    TIMESTAMPTZ,
    valor_booleano      BOOLEAN,
    valor_json          JSONB,      -- selección múltiple, coordenadas, etc.
    -- Metadata
    es_valido           BOOLEAN     NOT NULL DEFAULT true,
    errores_validacion  JSONB,
    respondida_en       TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_inspeccion_respuesta PRIMARY KEY (id),
    CONSTRAINT fk_ir_inspeccion FOREIGN KEY (inspeccion_id) REFERENCES sf.inspeccion(id)    ON DELETE CASCADE,
    CONSTRAINT fk_ir_pregunta   FOREIGN KEY (pregunta_id)   REFERENCES sf.flujo_pregunta(id),
    CONSTRAINT uq_ir_insp_pregunta UNIQUE (inspeccion_id, pregunta_id)
);

COMMENT ON COLUMN sf.inspeccion_respuesta.valor_json IS
    'Para selección múltiple: ["op1","op2"]. Para coordenadas: {"x":...,"y":...,"precision":...}';

-- =============================================================================
-- TABLA: inspeccion_fotografia
-- =============================================================================
CREATE TABLE sf.inspeccion_fotografia (
    id                      UUID        NOT NULL DEFAULT uuid_generate_v4(),
    inspeccion_id           UUID        NOT NULL,
    pregunta_id             UUID,       -- NULL si es foto de cierre/general
    nombre_archivo          VARCHAR(500) NOT NULL,
    ruta_almacenamiento     TEXT        NOT NULL,
    url_publica             TEXT,       -- URL de acceso si está en S3/blob
    tamanio_bytes           INTEGER,
    ancho                   INTEGER,
    alto                    INTEGER,
    formato                 VARCHAR(20) DEFAULT 'jpg',
    coordenada_x            DECIMAL(18, 8),
    coordenada_y            DECIMAL(18, 8),
    precision_gps           DECIMAL(10, 2),
    tiene_marca_agua        BOOLEAN     NOT NULL DEFAULT false,
    hash_sha256             VARCHAR(64),         -- deduplicación
    orden                   INTEGER     NOT NULL DEFAULT 0,
    metadata_json           JSONB       NOT NULL DEFAULT '{}',
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_inspeccion_fotografia PRIMARY KEY (id),
    CONSTRAINT fk_if_inspeccion FOREIGN KEY (inspeccion_id) REFERENCES sf.inspeccion(id)    ON DELETE CASCADE,
    CONSTRAINT fk_if_pregunta   FOREIGN KEY (pregunta_id)   REFERENCES sf.flujo_pregunta(id)
);

COMMENT ON COLUMN sf.inspeccion_fotografia.metadata_json IS
    '{"tomada_en":"2024-01-15T10:30:00Z","operador_nombre":"Juan Pérez","id_servicio":"SRV001"}';

-- =============================================================================
-- TABLA: inspeccion_historial  (log de cambios de estado)
-- =============================================================================
CREATE TABLE sf.inspeccion_historial (
    id              UUID        NOT NULL DEFAULT uuid_generate_v4(),
    inspeccion_id   UUID        NOT NULL,
    usuario_id      UUID,       -- si fue acción web
    operador_id     UUID,       -- si fue acción móvil
    accion          VARCHAR(100) NOT NULL,
    estado_anterior sf.estado_inspeccion,
    estado_nuevo    sf.estado_inspeccion NOT NULL,
    observacion     TEXT,
    metadata_json   JSONB,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_inspeccion_historial PRIMARY KEY (id),
    CONSTRAINT fk_ih_inspeccion FOREIGN KEY (inspeccion_id) REFERENCES sf.inspeccion(id),
    CONSTRAINT fk_ih_usuario    FOREIGN KEY (usuario_id)    REFERENCES sf.usuario(id),
    CONSTRAINT fk_ih_operador   FOREIGN KEY (operador_id)   REFERENCES sf.operador(id)
);

-- =============================================================================
-- TABLA: sincronizacion_log
-- =============================================================================
CREATE TABLE sf.sincronizacion_log (
    id                      UUID        NOT NULL DEFAULT uuid_generate_v4(),
    empresa_id              UUID        NOT NULL,
    operador_id             UUID        NOT NULL,
    device_id               VARCHAR(200),
    tipo                    sf.tipo_sync NOT NULL,
    payload_hash            VARCHAR(64),
    registros_enviados      INTEGER     DEFAULT 0,
    registros_recibidos     INTEGER     DEFAULT 0,
    fotos_enviadas          INTEGER     DEFAULT 0,
    bytes_transferidos      BIGINT      DEFAULT 0,
    duracion_ms             INTEGER,
    exitoso                 BOOLEAN     NOT NULL DEFAULT true,
    errores_json            JSONB,
    ip_origen               INET,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_sync_log PRIMARY KEY (id),
    CONSTRAINT fk_sl_empresa  FOREIGN KEY (empresa_id) REFERENCES sf.empresa(id),
    CONSTRAINT fk_sl_operador FOREIGN KEY (operador_id) REFERENCES sf.operador(id)
);

-- =============================================================================
-- TABLA: auditoria  (log general de acciones del sistema)
-- =============================================================================
CREATE TABLE sf.auditoria (
    id              UUID        NOT NULL DEFAULT uuid_generate_v4(),
    empresa_id      UUID,
    usuario_id      UUID,
    operador_id     UUID,
    entidad         VARCHAR(100) NOT NULL,
    entidad_id      UUID,
    accion          VARCHAR(50)  NOT NULL,  -- CREATE, UPDATE, DELETE, LOGIN, etc.
    datos_anteriores JSONB,
    datos_nuevos    JSONB,
    ip              INET,
    user_agent      TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_auditoria PRIMARY KEY (id)
);

COMMENT ON TABLE sf.auditoria IS 'Log de auditoría inmutable. No tiene FK para no bloquear en cascada.';

-- =============================================================================
-- ÍNDICES
-- =============================================================================

-- empresa
CREATE INDEX idx_empresa_activo ON sf.empresa(activo) WHERE deleted_at IS NULL;
CREATE INDEX idx_empresa_tenant_slug ON sf.empresa(tenant_slug);

-- usuario
CREATE INDEX idx_usuario_empresa ON sf.usuario(empresa_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_usuario_email ON sf.usuario(email);
CREATE INDEX idx_usuario_estado ON sf.usuario(estado) WHERE deleted_at IS NULL;

-- operador
CREATE INDEX idx_operador_empresa ON sf.operador(empresa_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_operador_activo ON sf.operador(empresa_id, activo) WHERE deleted_at IS NULL;

-- flujo
CREATE INDEX idx_flujo_empresa ON sf.flujo(empresa_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_flujo_tipo_insp ON sf.flujo(tipo_inspeccion_id);

-- flujo_version
CREATE INDEX idx_flujo_version_flujo ON sf.flujo_version(flujo_id);
CREATE INDEX idx_flujo_version_estado ON sf.flujo_version(estado);

-- flujo_seccion
CREATE INDEX idx_seccion_version ON sf.flujo_seccion(flujo_version_id);
CREATE INDEX idx_seccion_orden ON sf.flujo_seccion(flujo_version_id, orden);

-- flujo_pregunta
CREATE INDEX idx_pregunta_version ON sf.flujo_pregunta(flujo_version_id);
CREATE INDEX idx_pregunta_seccion ON sf.flujo_pregunta(seccion_id);
CREATE INDEX idx_pregunta_orden ON sf.flujo_pregunta(seccion_id, orden);
CREATE INDEX idx_pregunta_tipo ON sf.flujo_pregunta(tipo_control);

-- flujo_regla
CREATE INDEX idx_regla_version ON sf.flujo_regla(flujo_version_id);
CREATE INDEX idx_regla_origen ON sf.flujo_regla(pregunta_origen_id);

-- importacion_lote
CREATE INDEX idx_il_empresa ON sf.importacion_lote(empresa_id);
CREATE INDEX idx_il_estado ON sf.importacion_lote(estado);
CREATE INDEX idx_il_usuario ON sf.importacion_lote(usuario_id);
CREATE INDEX idx_il_created ON sf.importacion_lote(created_at DESC);

-- servicio_inspeccion
CREATE INDEX idx_si_empresa ON sf.servicio_inspeccion(empresa_id);
CREATE INDEX idx_si_lote ON sf.servicio_inspeccion(importacion_lote_id);
CREATE INDEX idx_si_localidad ON sf.servicio_inspeccion(empresa_id, localidad);
CREATE INDEX idx_si_ruta ON sf.servicio_inspeccion(empresa_id, ruta);
CREATE INDEX idx_si_lote_campo ON sf.servicio_inspeccion(empresa_id, lote);
CREATE INDEX idx_si_activo ON sf.servicio_inspeccion(empresa_id, activo);
-- búsqueda de texto
CREATE INDEX idx_si_direccion_trgm ON sf.servicio_inspeccion USING gin (direccion gin_trgm_ops);
CREATE INDEX idx_si_nombre_trgm ON sf.servicio_inspeccion USING gin (nombre_cliente gin_trgm_ops);

-- asignacion_inspeccion
CREATE INDEX idx_ai_empresa ON sf.asignacion_inspeccion(empresa_id);
CREATE INDEX idx_ai_operador ON sf.asignacion_inspeccion(operador_id);
CREATE INDEX idx_ai_servicio ON sf.asignacion_inspeccion(servicio_inspeccion_id);
CREATE INDEX idx_ai_estado ON sf.asignacion_inspeccion(empresa_id, estado) WHERE deleted_at IS NULL;
CREATE INDEX idx_ai_localidad ON sf.asignacion_inspeccion(empresa_id)
    INCLUDE (operador_id, estado);

-- inspeccion
CREATE INDEX idx_insp_empresa ON sf.inspeccion(empresa_id);
CREATE INDEX idx_insp_operador ON sf.inspeccion(operador_id);
CREATE INDEX idx_insp_asignacion ON sf.inspeccion(asignacion_id);
CREATE INDEX idx_insp_estado ON sf.inspeccion(empresa_id, estado);
CREATE INDEX idx_insp_servicio ON sf.inspeccion(servicio_inspeccion_id);
CREATE INDEX idx_insp_sync ON sf.inspeccion(sincronizado_en) WHERE sincronizado_en IS NOT NULL;
CREATE INDEX idx_insp_created ON sf.inspeccion(created_at DESC);

-- inspeccion_respuesta
CREATE INDEX idx_ir_inspeccion ON sf.inspeccion_respuesta(inspeccion_id);
CREATE INDEX idx_ir_pregunta ON sf.inspeccion_respuesta(pregunta_id);

-- inspeccion_fotografia
CREATE INDEX idx_if_inspeccion ON sf.inspeccion_fotografia(inspeccion_id);
CREATE INDEX idx_if_pregunta ON sf.inspeccion_fotografia(pregunta_id);
CREATE INDEX idx_if_hash ON sf.inspeccion_fotografia(hash_sha256);

-- inspeccion_historial
CREATE INDEX idx_ih_inspeccion ON sf.inspeccion_historial(inspeccion_id);
CREATE INDEX idx_ih_created ON sf.inspeccion_historial(created_at DESC);

-- sincronizacion_log
CREATE INDEX idx_sl_operador ON sf.sincronizacion_log(operador_id);
CREATE INDEX idx_sl_created ON sf.sincronizacion_log(created_at DESC);

-- auditoria
CREATE INDEX idx_aud_empresa ON sf.auditoria(empresa_id);
CREATE INDEX idx_aud_usuario ON sf.auditoria(usuario_id);
CREATE INDEX idx_aud_entidad ON sf.auditoria(entidad, entidad_id);
CREATE INDEX idx_aud_created ON sf.auditoria(created_at DESC);

-- catalogo
CREATE INDEX idx_cat_tipo ON sf.catalogo(tipo);
CREATE INDEX idx_cat_empresa_tipo ON sf.catalogo(empresa_id, tipo) WHERE activo = true;

-- =============================================================================
-- FUNCIONES DE UTILIDAD
-- =============================================================================

-- Función: actualizar updated_at automáticamente
CREATE OR REPLACE FUNCTION sf.fn_set_updated_at()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$;

-- Triggers updated_at
CREATE TRIGGER trg_empresa_updated_at
    BEFORE UPDATE ON sf.empresa
    FOR EACH ROW EXECUTE FUNCTION sf.fn_set_updated_at();

CREATE TRIGGER trg_usuario_updated_at
    BEFORE UPDATE ON sf.usuario
    FOR EACH ROW EXECUTE FUNCTION sf.fn_set_updated_at();

CREATE TRIGGER trg_operador_updated_at
    BEFORE UPDATE ON sf.operador
    FOR EACH ROW EXECUTE FUNCTION sf.fn_set_updated_at();

CREATE TRIGGER trg_tipo_inspeccion_updated_at
    BEFORE UPDATE ON sf.tipo_inspeccion
    FOR EACH ROW EXECUTE FUNCTION sf.fn_set_updated_at();

CREATE TRIGGER trg_flujo_updated_at
    BEFORE UPDATE ON sf.flujo
    FOR EACH ROW EXECUTE FUNCTION sf.fn_set_updated_at();

CREATE TRIGGER trg_flujo_version_updated_at
    BEFORE UPDATE ON sf.flujo_version
    FOR EACH ROW EXECUTE FUNCTION sf.fn_set_updated_at();

CREATE TRIGGER trg_flujo_seccion_updated_at
    BEFORE UPDATE ON sf.flujo_seccion
    FOR EACH ROW EXECUTE FUNCTION sf.fn_set_updated_at();

CREATE TRIGGER trg_flujo_pregunta_updated_at
    BEFORE UPDATE ON sf.flujo_pregunta
    FOR EACH ROW EXECUTE FUNCTION sf.fn_set_updated_at();

CREATE TRIGGER trg_flujo_regla_updated_at
    BEFORE UPDATE ON sf.flujo_regla
    FOR EACH ROW EXECUTE FUNCTION sf.fn_set_updated_at();

CREATE TRIGGER trg_importacion_lote_updated_at
    BEFORE UPDATE ON sf.importacion_lote
    FOR EACH ROW EXECUTE FUNCTION sf.fn_set_updated_at();

CREATE TRIGGER trg_servicio_inspeccion_updated_at
    BEFORE UPDATE ON sf.servicio_inspeccion
    FOR EACH ROW EXECUTE FUNCTION sf.fn_set_updated_at();

CREATE TRIGGER trg_asignacion_inspeccion_updated_at
    BEFORE UPDATE ON sf.asignacion_inspeccion
    FOR EACH ROW EXECUTE FUNCTION sf.fn_set_updated_at();

CREATE TRIGGER trg_inspeccion_updated_at
    BEFORE UPDATE ON sf.inspeccion
    FOR EACH ROW EXECUTE FUNCTION sf.fn_set_updated_at();

-- =============================================================================
-- VISTAS ÚTILES
-- =============================================================================

-- Vista: resumen de asignaciones por operador
CREATE OR REPLACE VIEW sf.v_resumen_operador AS
SELECT
    o.empresa_id,
    o.id AS operador_id,
    o.codigo_operador,
    o.nombre || ' ' || o.apellido AS nombre_completo,
    o.zona,
    o.localidad,
    COUNT(ai.id) FILTER (WHERE ai.estado = 'pendiente')      AS pendientes,
    COUNT(ai.id) FILTER (WHERE ai.estado = 'asignada')       AS asignadas,
    COUNT(ai.id) FILTER (WHERE ai.estado = 'en_ejecucion')   AS en_ejecucion,
    COUNT(ai.id) FILTER (WHERE ai.estado = 'finalizada')     AS finalizadas,
    COUNT(ai.id) FILTER (WHERE ai.estado = 'sincronizada')   AS sincronizadas,
    COUNT(ai.id) FILTER (WHERE ai.estado IN ('observada','rechazada')) AS observadas,
    COUNT(ai.id) AS total,
    o.fecha_ultima_sync
FROM sf.operador o
LEFT JOIN sf.asignacion_inspeccion ai
    ON ai.operador_id = o.id AND ai.deleted_at IS NULL
WHERE o.deleted_at IS NULL
GROUP BY o.empresa_id, o.id, o.codigo_operador, o.nombre, o.apellido, o.zona, o.localidad, o.fecha_ultima_sync;

-- Vista: avance por localidad
CREATE OR REPLACE VIEW sf.v_avance_localidad AS
SELECT
    si.empresa_id,
    si.localidad,
    COUNT(DISTINCT si.id) AS total_servicios,
    COUNT(DISTINCT ai.id) FILTER (WHERE ai.deleted_at IS NULL) AS total_asignados,
    COUNT(DISTINCT i.id) FILTER (WHERE i.estado IN ('completada','aprobada','enviada')) AS total_inspeccionados,
    COUNT(DISTINCT i.id) FILTER (WHERE i.estado = 'aprobada') AS total_aprobados,
    COUNT(DISTINCT if2.id) AS total_fotografias
FROM sf.servicio_inspeccion si
LEFT JOIN sf.asignacion_inspeccion ai ON ai.servicio_inspeccion_id = si.id
LEFT JOIN sf.inspeccion i ON i.asignacion_id = ai.id
LEFT JOIN sf.inspeccion_fotografia if2 ON if2.inspeccion_id = i.id
WHERE si.activo = true
GROUP BY si.empresa_id, si.localidad;

-- Vista: dashboard resumen empresa
CREATE OR REPLACE VIEW sf.v_dashboard_empresa AS
SELECT
    e.id AS empresa_id,
    e.nombre AS empresa_nombre,
    COUNT(DISTINCT si.id) AS total_servicios,
    COUNT(DISTINCT ai.id) FILTER (WHERE ai.deleted_at IS NULL) AS total_asignaciones,
    COUNT(DISTINCT ai.id) FILTER (WHERE ai.estado = 'pendiente') AS pendientes,
    COUNT(DISTINCT ai.id) FILTER (WHERE ai.estado = 'en_ejecucion') AS en_ejecucion,
    COUNT(DISTINCT i.id) FILTER (WHERE i.estado IN ('completada','aprobada')) AS completadas,
    COUNT(DISTINCT i.id) FILTER (WHERE i.estado = 'aprobada') AS aprobadas,
    COUNT(DISTINCT i.id) FILTER (WHERE i.estado IN ('observada','rechazada')) AS con_observacion,
    COUNT(DISTINCT i.id) FILTER (WHERE i.sincronizado_en IS NOT NULL) AS sincronizadas,
    COUNT(DISTINCT if2.id) AS total_fotografias,
    COUNT(DISTINCT o.id) FILTER (WHERE o.activo = true) AS operadores_activos
FROM sf.empresa e
LEFT JOIN sf.servicio_inspeccion si ON si.empresa_id = e.id AND si.activo = true
LEFT JOIN sf.asignacion_inspeccion ai ON ai.empresa_id = e.id
LEFT JOIN sf.inspeccion i ON i.empresa_id = e.id
LEFT JOIN sf.inspeccion_fotografia if2 ON if2.inspeccion_id = i.id
LEFT JOIN sf.operador o ON o.empresa_id = e.id AND o.deleted_at IS NULL
WHERE e.deleted_at IS NULL
GROUP BY e.id, e.nombre;

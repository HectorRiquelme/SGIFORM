-- =============================================================================
-- SanitasField - Datos semilla (seed)
-- Version: 1.0.0
-- =============================================================================

SET search_path TO sf, public;

-- =============================================================================
-- PERMISOS DEL SISTEMA
-- =============================================================================
INSERT INTO sf.permiso (modulo, accion, descripcion) VALUES
-- Empresas
('empresas',            'read',     'Ver empresas'),
('empresas',            'create',   'Crear empresas'),
('empresas',            'update',   'Editar empresas'),
('empresas',            'delete',   'Eliminar empresas'),
-- Usuarios
('usuarios',            'read',     'Ver usuarios'),
('usuarios',            'create',   'Crear usuarios'),
('usuarios',            'update',   'Editar usuarios'),
('usuarios',            'delete',   'Eliminar usuarios'),
-- Operadores
('operadores',          'read',     'Ver operadores'),
('operadores',          'create',   'Crear operadores'),
('operadores',          'update',   'Editar operadores'),
('operadores',          'delete',   'Eliminar operadores'),
-- Tipos de inspección
('tipos_inspeccion',    'read',     'Ver tipos de inspección'),
('tipos_inspeccion',    'create',   'Crear tipos de inspección'),
('tipos_inspeccion',    'update',   'Editar tipos de inspección'),
('tipos_inspeccion',    'delete',   'Eliminar tipos de inspección'),
-- Flujos
('flujos',              'read',     'Ver flujos'),
('flujos',              'create',   'Crear flujos'),
('flujos',              'update',   'Editar flujos'),
('flujos',              'delete',   'Eliminar flujos'),
('flujos',              'publish',  'Publicar versiones de flujo'),
-- Importación
('importaciones',       'read',     'Ver importaciones'),
('importaciones',       'create',   'Importar archivos Excel'),
('importaciones',       'delete',   'Eliminar lotes de importación'),
-- Servicios
('servicios',           'read',     'Ver servicios de inspección'),
('servicios',           'update',   'Editar servicios de inspección'),
-- Asignaciones
('asignaciones',        'read',     'Ver asignaciones'),
('asignaciones',        'create',   'Crear asignaciones'),
('asignaciones',        'update',   'Editar asignaciones'),
('asignaciones',        'delete',   'Eliminar asignaciones'),
('asignaciones',        'massive',  'Asignación masiva'),
-- Inspecciones
('inspecciones',        'read',     'Ver inspecciones'),
('inspecciones',        'approve',  'Aprobar inspecciones'),
('inspecciones',        'observe',  'Observar inspecciones'),
('inspecciones',        'reject',   'Rechazar inspecciones'),
-- Dashboard
('dashboard',           'read',     'Ver dashboard'),
-- Reportes
('reportes',            'excel',    'Exportar Excel'),
('reportes',            'pdf',      'Exportar PDF'),
('reportes',            'photos',   'Ver fotografías en reportes'),
-- Admin
('admin',               'full',     'Acceso total al sistema')
ON CONFLICT DO NOTHING;

-- =============================================================================
-- EMPRESA DEMO (tenant de prueba)
-- =============================================================================
INSERT INTO sf.empresa (id, codigo, nombre, rut, tenant_slug, plan)
VALUES (
    '00000000-0000-0000-0000-000000000001',
    'SANITARIA-DEMO',
    'Sanitaria Demo S.A.',
    '76.000.001-K',
    'sanitaria-demo',
    'enterprise'
) ON CONFLICT DO NOTHING;

-- =============================================================================
-- ROLES DEL SISTEMA para empresa demo
-- =============================================================================
INSERT INTO sf.rol (id, empresa_id, nombre, codigo, descripcion, es_sistema) VALUES
(
    '10000000-0000-0000-0000-000000000001',
    '00000000-0000-0000-0000-000000000001',
    'Administrador', 'admin',
    'Acceso total al sistema. Gestión de usuarios, flujos, reportes.', true
),
(
    '10000000-0000-0000-0000-000000000002',
    '00000000-0000-0000-0000-000000000001',
    'Supervisor', 'supervisor',
    'Gestión de asignaciones, revisión y control de calidad de inspecciones.', true
),
(
    '10000000-0000-0000-0000-000000000003',
    '00000000-0000-0000-0000-000000000001',
    'Auditor', 'auditor',
    'Solo lectura. Revisión y exportación de reportes.', true
),
(
    '10000000-0000-0000-0000-000000000004',
    '00000000-0000-0000-0000-000000000001',
    'Cliente Consulta', 'cliente_consulta',
    'Acceso limitado de solo lectura para clientes externos.', true
)
ON CONFLICT DO NOTHING;

-- =============================================================================
-- ASIGNAR PERMISOS A ROLES
-- =============================================================================

-- Administrador: todos los permisos
INSERT INTO sf.rol_permiso (rol_id, permiso_id)
SELECT '10000000-0000-0000-0000-000000000001', id FROM sf.permiso
ON CONFLICT DO NOTHING;

-- Supervisor: operadores (read), asignaciones (all), inspecciones (all), reportes, dashboard
INSERT INTO sf.rol_permiso (rol_id, permiso_id)
SELECT '10000000-0000-0000-0000-000000000002', id
FROM sf.permiso
WHERE (modulo, accion) IN (
    ('operadores',       'read'),
    ('tipos_inspeccion', 'read'),
    ('flujos',           'read'),
    ('importaciones',    'read'),
    ('servicios',        'read'),
    ('asignaciones',     'read'),
    ('asignaciones',     'create'),
    ('asignaciones',     'update'),
    ('asignaciones',     'massive'),
    ('inspecciones',     'read'),
    ('inspecciones',     'approve'),
    ('inspecciones',     'observe'),
    ('inspecciones',     'reject'),
    ('dashboard',        'read'),
    ('reportes',         'excel'),
    ('reportes',         'pdf'),
    ('reportes',         'photos')
) ON CONFLICT DO NOTHING;

-- Auditor: solo lectura + reportes
INSERT INTO sf.rol_permiso (rol_id, permiso_id)
SELECT '10000000-0000-0000-0000-000000000003', id
FROM sf.permiso
WHERE (modulo, accion) IN (
    ('servicios',        'read'),
    ('asignaciones',     'read'),
    ('inspecciones',     'read'),
    ('dashboard',        'read'),
    ('reportes',         'excel'),
    ('reportes',         'pdf'),
    ('reportes',         'photos')
) ON CONFLICT DO NOTHING;

-- Cliente Consulta: solo dashboard e inspecciones (sin fotos)
INSERT INTO sf.rol_permiso (rol_id, permiso_id)
SELECT '10000000-0000-0000-0000-000000000004', id
FROM sf.permiso
WHERE (modulo, accion) IN (
    ('inspecciones', 'read'),
    ('dashboard',    'read'),
    ('reportes',     'excel')
) ON CONFLICT DO NOTHING;

-- =============================================================================
-- USUARIO ADMINISTRADOR DEMO
-- password: Admin@2024! (bcrypt hash)
-- =============================================================================
INSERT INTO sf.usuario (id, empresa_id, rol_id, email, password_hash, nombre, apellido, estado)
VALUES (
    '20000000-0000-0000-0000-000000000001',
    '00000000-0000-0000-0000-000000000001',
    '10000000-0000-0000-0000-000000000001',
    'admin@sanitaria-demo.cl',
    '$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj4J/HS.i8Gu',  -- placeholder hash
    'Administrador',
    'Sistema',
    'activo'
) ON CONFLICT DO NOTHING;

-- =============================================================================
-- CATÁLOGOS DEL SISTEMA (globales)
-- =============================================================================

-- Marcas de medidores
INSERT INTO sf.catalogo (empresa_id, tipo, codigo, texto, orden) VALUES
(NULL, 'marca_medidor', 'ACTARIS',   'Actaris',      1),
(NULL, 'marca_medidor', 'ELSTER',    'Elster',       2),
(NULL, 'marca_medidor', 'ITRON',     'Itron',        3),
(NULL, 'marca_medidor', 'ZENNER',    'Zenner',       4),
(NULL, 'marca_medidor', 'SENSUS',    'Sensus',       5),
(NULL, 'marca_medidor', 'KAMSTRUP',  'Kamstrup',     6),
(NULL, 'marca_medidor', 'ABB',       'ABB',          7),
(NULL, 'marca_medidor', 'BADGER',    'Badger',       8),
(NULL, 'marca_medidor', 'AMCO',      'Amco',         9),
(NULL, 'marca_medidor', 'OTRO',      'Otro',         99)
ON CONFLICT DO NOTHING;

-- Diámetros de medidor
INSERT INTO sf.catalogo (empresa_id, tipo, codigo, texto, orden) VALUES
(NULL, 'diametro_medidor', '13',  '13 mm',   1),
(NULL, 'diametro_medidor', '15',  '15 mm',   2),
(NULL, 'diametro_medidor', '20',  '20 mm',   3),
(NULL, 'diametro_medidor', '25',  '25 mm',   4),
(NULL, 'diametro_medidor', '32',  '32 mm',   5),
(NULL, 'diametro_medidor', '40',  '40 mm',   6),
(NULL, 'diametro_medidor', '50',  '50 mm',   7),
(NULL, 'diametro_medidor', '80',  '80 mm',   8),
(NULL, 'diametro_medidor', '100', '100 mm',  9),
(NULL, 'diametro_medidor', 'NE',  'No especificado', 99)
ON CONFLICT DO NOTHING;

-- Tipos de anomalía
INSERT INTO sf.catalogo (empresa_id, tipo, codigo, texto, orden) VALUES
(NULL, 'tipo_anomalia', 'MEDIDOR_DANADO',         'Medidor dañado',                1),
(NULL, 'tipo_anomalia', 'LECTURA_IMPOSIBLE',       'Lectura imposible',             2),
(NULL, 'tipo_anomalia', 'SELLO_ROTO',              'Sello roto o faltante',         3),
(NULL, 'tipo_anomalia', 'CONEXION_IRREGULAR',      'Conexión irregular',            4),
(NULL, 'tipo_anomalia', 'FUGA_VISIBLE',            'Fuga visible',                  5),
(NULL, 'tipo_anomalia', 'MEDIDOR_INVERTIDO',       'Medidor invertido',             6),
(NULL, 'tipo_anomalia', 'CAJA_DANIADA',            'Caja de medidor dañada',        7),
(NULL, 'tipo_anomalia', 'ACCESO_DENEGADO',         'Sin acceso al domicilio',       8),
(NULL, 'tipo_anomalia', 'PREDIO_DESHABITADO',      'Predio deshabitado',            9),
(NULL, 'tipo_anomalia', 'PREDIO_DEMOLIDO',         'Predio demolido/inexistente',  10),
(NULL, 'tipo_anomalia', 'OTRO',                    'Otra anomalía',                99)
ON CONFLICT DO NOTHING;

-- Motivos de no inspección
INSERT INTO sf.catalogo (empresa_id, tipo, codigo, texto, orden) VALUES
(NULL, 'motivo_no_inspeccion', 'PERRO_BRAVO',    'Perro agresivo en el predio',   1),
(NULL, 'motivo_no_inspeccion', 'SIN_ACCESO',     'Sin acceso / reja cerrada',     2),
(NULL, 'motivo_no_inspeccion', 'AUSENTE',        'Propietario ausente',           3),
(NULL, 'motivo_no_inspeccion', 'RECHAZA',        'Propietario se niega',          4),
(NULL, 'motivo_no_inspeccion', 'OTRO',           'Otro motivo',                   99)
ON CONFLICT DO NOTHING;

-- =============================================================================
-- TIPOS DE INSPECCIÓN DEMO
-- =============================================================================
INSERT INTO sf.tipo_inspeccion (id, empresa_id, codigo, nombre, descripcion, activo) VALUES
(
    '30000000-0000-0000-0000-000000000001',
    '00000000-0000-0000-0000-000000000001',
    'INSP-MED', 'Inspección de Medidor',
    'Inspección técnica completa de medidor de agua. Verifica estado, lectura, sellos y anomalías.',
    true
),
(
    '30000000-0000-0000-0000-000000000002',
    '00000000-0000-0000-0000-000000000001',
    'VER-LEC', 'Verificación de Lectura',
    'Verificación de la lectura del medidor y comparación con lectura anterior.',
    true
),
(
    '30000000-0000-0000-0000-000000000003',
    '00000000-0000-0000-0000-000000000001',
    'DET-ANOM', 'Detección de Anomalías',
    'Inspección enfocada en detectar conexiones irregulares, fugas y anomalías técnicas.',
    true
),
(
    '30000000-0000-0000-0000-000000000004',
    '00000000-0000-0000-0000-000000000001',
    'INSP-DOM', 'Inspección Técnica Domiciliaria',
    'Inspección integral del servicio domiciliario incluyendo medidor, conexiones y estado general.',
    true
),
(
    '30000000-0000-0000-0000-000000000005',
    '00000000-0000-0000-0000-000000000001',
    'VAL-CAT', 'Validación Catastral',
    'Validación y actualización de datos catastrales del servicio: dirección, nombre, datos técnicos.',
    true
)
ON CONFLICT DO NOTHING;

-- =============================================================================
-- FLUJO DE EJEMPLO: Inspección de Medidor completa
-- =============================================================================

-- Flujo
INSERT INTO sf.flujo (id, empresa_id, tipo_inspeccion_id, nombre, descripcion) VALUES
(
    '40000000-0000-0000-0000-000000000001',
    '00000000-0000-0000-0000-000000000001',
    '30000000-0000-0000-0000-000000000001',
    'Flujo Inspección de Medidor v1',
    'Flujo completo para inspección técnica de medidor de agua domiciliario'
) ON CONFLICT DO NOTHING;

-- Versión 1 del flujo (publicada)
INSERT INTO sf.flujo_version (id, flujo_id, numero_version, estado, descripcion_cambio) VALUES
(
    '50000000-0000-0000-0000-000000000001',
    '40000000-0000-0000-0000-000000000001',
    1, 'publicado',
    'Versión inicial del flujo de inspección de medidor'
) ON CONFLICT DO NOTHING;

-- Actualizar tipo_inspeccion con flujo por defecto
UPDATE sf.tipo_inspeccion
SET flujo_version_id_def = '50000000-0000-0000-0000-000000000001'
WHERE id = '30000000-0000-0000-0000-000000000001';

-- SECCIONES del flujo
INSERT INTO sf.flujo_seccion (id, flujo_version_id, codigo, titulo, orden) VALUES
('60000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000001', 'SEC_ACCESO',    'Acceso al Domicilio',     1),
('60000000-0000-0000-0000-000000000002', '50000000-0000-0000-0000-000000000001', 'SEC_MEDIDOR',   'Estado del Medidor',      2),
('60000000-0000-0000-0000-000000000003', '50000000-0000-0000-0000-000000000001', 'SEC_LECTURA',   'Lectura del Medidor',     3),
('60000000-0000-0000-0000-000000000004', '50000000-0000-0000-0000-000000000001', 'SEC_ANOMALIAS', 'Anomalías Detectadas',    4),
('60000000-0000-0000-0000-000000000005', '50000000-0000-0000-0000-000000000001', 'SEC_EVIDENCIA', 'Evidencia Fotográfica',   5),
('60000000-0000-0000-0000-000000000006', '50000000-0000-0000-0000-000000000001', 'SEC_CIERRE',    'Cierre de Inspección',    6)
ON CONFLICT DO NOTHING;

-- PREGUNTAS SECCIÓN 1: ACCESO
INSERT INTO sf.flujo_pregunta (id, flujo_version_id, seccion_id, codigo, texto, tipo_control, obligatorio, orden, validaciones_json) VALUES
(
    '70000000-0000-0000-0000-000000000001',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000001',
    'p_acceso_domicilio', 'Se pudo acceder al domicilio?',
    'si_no', true, 1, '{}'
),
(
    '70000000-0000-0000-0000-000000000002',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000001',
    'p_motivo_sin_acceso', 'Motivo de no acceso',
    'seleccion_unica', false, 2, '{}'
),
(
    '70000000-0000-0000-0000-000000000003',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000001',
    'p_foto_fachada', 'Fotografía de la fachada del domicilio',
    'foto_unica', true, 3, '{"min_fotos": 1, "max_fotos": 1}'
)
ON CONFLICT DO NOTHING;

-- Opciones para motivo sin acceso
INSERT INTO sf.flujo_opcion (pregunta_id, codigo, texto, orden) VALUES
('70000000-0000-0000-0000-000000000002', 'PERRO_BRAVO',  'Perro agresivo',        1),
('70000000-0000-0000-0000-000000000002', 'SIN_ACCESO',   'Sin acceso / reja',     2),
('70000000-0000-0000-0000-000000000002', 'AUSENTE',      'Propietario ausente',   3),
('70000000-0000-0000-0000-000000000002', 'RECHAZA',      'Propietario se niega',  4),
('70000000-0000-0000-0000-000000000002', 'OTRO',         'Otro',                  99)
ON CONFLICT DO NOTHING;

-- PREGUNTAS SECCIÓN 2: MEDIDOR
INSERT INTO sf.flujo_pregunta (id, flujo_version_id, seccion_id, codigo, texto, tipo_control, obligatorio, orden, validaciones_json) VALUES
(
    '70000000-0000-0000-0000-000000000010',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000002',
    'p_medidor_visible', 'El medidor es visible y accesible?',
    'si_no', true, 1, '{}'
),
(
    '70000000-0000-0000-0000-000000000011',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000002',
    'p_estado_medidor', 'Estado físico del medidor',
    'seleccion_unica', true, 2, '{}'
),
(
    '70000000-0000-0000-0000-000000000012',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000002',
    'p_sello_presente', 'El sello de seguridad está presente e intacto?',
    'si_no', true, 3, '{}'
),
(
    '70000000-0000-0000-0000-000000000013',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000002',
    'p_numero_serie_medidor', 'Número de serie del medidor (verificar con el registrado)',
    'texto_corto', true, 4, '{"max_length": 50}'
),
(
    '70000000-0000-0000-0000-000000000014',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000002',
    'p_medidor_danado', 'Se detectan daños en el medidor?',
    'si_no', true, 5, '{}'
),
(
    '70000000-0000-0000-0000-000000000015',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000002',
    'p_tipo_dano', 'Tipo de daño detectado',
    'seleccion_multiple', false, 6,
    '{"mensaje_error": "Debe seleccionar al menos un tipo de daño"}'
)
ON CONFLICT DO NOTHING;

-- Opciones estado medidor
INSERT INTO sf.flujo_opcion (pregunta_id, codigo, texto, orden) VALUES
('70000000-0000-0000-0000-000000000011', 'BUENO',    'Bueno / Sin daños',     1),
('70000000-0000-0000-0000-000000000011', 'REGULAR',  'Regular / Desgaste',    2),
('70000000-0000-0000-0000-000000000011', 'MALO',     'Malo / Con daños',      3),
('70000000-0000-0000-0000-000000000011', 'CRITICO',  'Crítico / Inutilizable',4)
ON CONFLICT DO NOTHING;

-- Opciones tipos de daño
INSERT INTO sf.flujo_opcion (pregunta_id, codigo, texto, orden) VALUES
('70000000-0000-0000-0000-000000000015', 'GOLPE',       'Golpe / Impacto',       1),
('70000000-0000-0000-0000-000000000015', 'CORROSION',   'Corrosión / Oxidación', 2),
('70000000-0000-0000-0000-000000000015', 'FISURA',      'Fisura / Grieta',       3),
('70000000-0000-0000-0000-000000000015', 'ROTACION',    'Dígitos trabados',      4),
('70000000-0000-0000-0000-000000000015', 'HUMEDAD',     'Humedad interna',       5),
('70000000-0000-0000-0000-000000000015', 'MODIFICADO',  'Posible manipulación',  6)
ON CONFLICT DO NOTHING;

-- PREGUNTAS SECCIÓN 3: LECTURA
INSERT INTO sf.flujo_pregunta (id, flujo_version_id, seccion_id, codigo, texto, tipo_control, obligatorio, orden, validaciones_json, configuracion_json) VALUES
(
    '70000000-0000-0000-0000-000000000020',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000003',
    'p_lectura_posible', 'Es posible tomar la lectura del medidor?',
    'si_no', true, 1, '{}', '{}'
),
(
    '70000000-0000-0000-0000-000000000021',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000003',
    'p_lectura_actual', 'Lectura actual del medidor (m³)',
    'decimal', false, 2,
    '{"min": 0, "max": 999999, "mensaje_error": "Ingrese la lectura en m³"}',
    '{"decimales": 2, "unidad": "m³"}'
),
(
    '70000000-0000-0000-0000-000000000022',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000003',
    'p_foto_lectura', 'Fotografía del display de lectura',
    'foto_unica', false, 3,
    '{"min_fotos": 1, "max_fotos": 1}',
    '{"marca_agua": true, "campos_marca_agua": ["fecha","hora","id_servicio","operador"]}'
)
ON CONFLICT DO NOTHING;

-- PREGUNTAS SECCIÓN 4: ANOMALÍAS
INSERT INTO sf.flujo_pregunta (id, flujo_version_id, seccion_id, codigo, texto, tipo_control, obligatorio, orden) VALUES
(
    '70000000-0000-0000-0000-000000000030',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000004',
    'p_hay_anomalias', 'Se detectan anomalías en la instalación?',
    'si_no', true, 1
),
(
    '70000000-0000-0000-0000-000000000031',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000004',
    'p_tipo_anomalia', 'Tipo(s) de anomalía detectada(s)',
    'seleccion_multiple', false, 2
),
(
    '70000000-0000-0000-0000-000000000032',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000004',
    'p_descripcion_anomalia', 'Descripción detallada de la anomalía',
    'texto_largo', false, 3
)
ON CONFLICT DO NOTHING;

-- Opciones tipo anomalía (sección 4)
INSERT INTO sf.flujo_opcion (pregunta_id, codigo, texto, orden) VALUES
('70000000-0000-0000-0000-000000000031', 'MEDIDOR_DANADO',    'Medidor dañado',            1),
('70000000-0000-0000-0000-000000000031', 'LECTURA_IMP',       'Lectura imposible',         2),
('70000000-0000-0000-0000-000000000031', 'SELLO_ROTO',        'Sello roto',                3),
('70000000-0000-0000-0000-000000000031', 'CONN_IRREGULAR',    'Conexión irregular',        4),
('70000000-0000-0000-0000-000000000031', 'FUGA',              'Fuga visible',              5),
('70000000-0000-0000-0000-000000000031', 'OTRO',              'Otra anomalía',             99)
ON CONFLICT DO NOTHING;

-- PREGUNTAS SECCIÓN 5: EVIDENCIA
INSERT INTO sf.flujo_pregunta (id, flujo_version_id, seccion_id, codigo, texto, tipo_control, obligatorio, orden, validaciones_json, configuracion_json) VALUES
(
    '70000000-0000-0000-0000-000000000040',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000005',
    'p_fotos_medidor', 'Fotografías del medidor (mínimo 2)',
    'fotos_multiples', true, 1,
    '{"min_fotos": 2, "max_fotos": 8}',
    '{"marca_agua": true, "campos_marca_agua": ["fecha","hora","id_servicio","numero_medidor","operador"]}'
),
(
    '70000000-0000-0000-0000-000000000041',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000005',
    'p_fotos_anomalia', 'Fotografías de anomalías detectadas',
    'fotos_multiples', false, 2,
    '{"min_fotos": 1, "max_fotos": 10}',
    '{"marca_agua": true, "campos_marca_agua": ["fecha","hora","id_servicio","operador"]}'
),
(
    '70000000-0000-0000-0000-000000000042',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000005',
    'p_coordenadas', 'Coordenadas GPS de la ubicación del medidor',
    'coordenadas', true, 3,
    '{}',
    '{"auto_capturar": true, "precision_minima_metros": 30}'
)
ON CONFLICT DO NOTHING;

-- PREGUNTAS SECCIÓN 6: CIERRE
INSERT INTO sf.flujo_pregunta (id, flujo_version_id, seccion_id, codigo, texto, tipo_control, obligatorio, orden) VALUES
(
    '70000000-0000-0000-0000-000000000050',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000006',
    'p_observaciones_finales', 'Observaciones finales del inspector',
    'texto_largo', false, 1
),
(
    '70000000-0000-0000-0000-000000000051',
    '50000000-0000-0000-0000-000000000001',
    '60000000-0000-0000-0000-000000000006',
    'p_firma_operador', 'Firma digital del operador',
    'firma', true, 2
)
ON CONFLICT DO NOTHING;

-- =============================================================================
-- REGLAS LÓGICAS DEL FLUJO
-- =============================================================================

-- R001: Si acceso=No → mostrar motivo_sin_acceso y hacerlo obligatorio
INSERT INTO sf.flujo_regla (flujo_version_id, codigo, pregunta_origen_id, operador, valor_comparacion, accion, pregunta_destino_id, orden)
VALUES
(
    '50000000-0000-0000-0000-000000000001', 'R001_MOSTRAR_MOTIVO',
    '70000000-0000-0000-0000-000000000001', 'eq', 'false',
    'mostrar', '70000000-0000-0000-0000-000000000002', 1
),
(
    '50000000-0000-0000-0000-000000000001', 'R002_OBLIGAR_MOTIVO',
    '70000000-0000-0000-0000-000000000001', 'eq', 'false',
    'obligatorio', '70000000-0000-0000-0000-000000000002', 2
),

-- R003: Si medidor_danado=Si → mostrar tipo_dano y hacerlo obligatorio
(
    '50000000-0000-0000-0000-000000000001', 'R003_MOSTRAR_DANO',
    '70000000-0000-0000-0000-000000000014', 'eq', 'true',
    'mostrar', '70000000-0000-0000-0000-000000000015', 3
),
(
    '50000000-0000-0000-0000-000000000001', 'R004_OBLIGAR_DANO',
    '70000000-0000-0000-0000-000000000014', 'eq', 'true',
    'obligatorio', '70000000-0000-0000-0000-000000000015', 4
),

-- R005: Si medidor_danado=Si → mínimo 3 fotos de anomalía
(
    '50000000-0000-0000-0000-000000000001', 'R005_MIN_FOTOS_DANO',
    '70000000-0000-0000-0000-000000000014', 'eq', 'true',
    'min_fotos', '70000000-0000-0000-0000-000000000041', 5
),

-- R006: Si lectura_posible=Si → hacer lectura_actual obligatoria
(
    '50000000-0000-0000-0000-000000000001', 'R006_OBLIGAR_LECTURA',
    '70000000-0000-0000-0000-000000000020', 'eq', 'true',
    'obligatorio', '70000000-0000-0000-0000-000000000021', 6
),
(
    '50000000-0000-0000-0000-000000000001', 'R007_OBLIGAR_FOTO_LECTURA',
    '70000000-0000-0000-0000-000000000020', 'eq', 'true',
    'obligatorio', '70000000-0000-0000-0000-000000000022', 7
),

-- R008: Si hay_anomalias=Si → mostrar y obligar tipo_anomalia
(
    '50000000-0000-0000-0000-000000000001', 'R008_MOSTRAR_TIPO_ANOMALIA',
    '70000000-0000-0000-0000-000000000030', 'eq', 'true',
    'mostrar', '70000000-0000-0000-0000-000000000031', 8
),
(
    '50000000-0000-0000-0000-000000000001', 'R009_OBLIGAR_TIPO_ANOMALIA',
    '70000000-0000-0000-0000-000000000030', 'eq', 'true',
    'obligatorio', '70000000-0000-0000-0000-000000000031', 9
),
(
    '50000000-0000-0000-0000-000000000001', 'R010_OBLIGAR_FOTOS_ANOMALIA',
    '70000000-0000-0000-0000-000000000030', 'eq', 'true',
    'obligatorio', '70000000-0000-0000-0000-000000000041', 10
)
ON CONFLICT DO NOTHING;

-- =============================================================================
-- OPERADORES DE EJEMPLO
-- =============================================================================
INSERT INTO sf.operador (id, empresa_id, codigo_operador, nombre, apellido, rut, telefono, email, zona, localidad, password_hash) VALUES
(
    'A0000000-0000-0000-0000-000000000001',
    '00000000-0000-0000-0000-000000000001',
    'OP001', 'Carlos', 'Muñoz', '12.345.678-9',
    '+56912345678', 'carlos.munoz@sanitaria-demo.cl',
    'Norte', 'La Serena',
    '$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj4J/HS.i8Gu'
),
(
    'A0000000-0000-0000-0000-000000000002',
    '00000000-0000-0000-0000-000000000001',
    'OP002', 'Ana', 'Rojas', '15.432.100-5',
    '+56987654321', 'ana.rojas@sanitaria-demo.cl',
    'Sur', 'Coquimbo',
    '$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj4J/HS.i8Gu'
),
(
    'A0000000-0000-0000-0000-000000000003',
    '00000000-0000-0000-0000-000000000001',
    'OP003', 'Pedro', 'González', '11.111.111-1',
    '+56911111111', 'pedro.gonzalez@sanitaria-demo.cl',
    'Centro', 'Ovalle',
    '$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj4J/HS.i8Gu'
)
ON CONFLICT DO NOTHING;

-- =============================================================================
-- FIN DEL SCRIPT DE SEED
-- =============================================================================

-- Verificación de carga
SELECT 'SEED CARGADO:' AS info;
SELECT 'Permisos: '   || COUNT(*) FROM sf.permiso;
SELECT 'Roles: '      || COUNT(*) FROM sf.rol;
SELECT 'Usuarios: '   || COUNT(*) FROM sf.usuario;
SELECT 'Operadores: ' || COUNT(*) FROM sf.operador;
SELECT 'Tipos insp: ' || COUNT(*) FROM sf.tipo_inspeccion;
SELECT 'Flujos: '     || COUNT(*) FROM sf.flujo;
SELECT 'Secciones: '  || COUNT(*) FROM sf.flujo_seccion;
SELECT 'Preguntas: '  || COUNT(*) FROM sf.flujo_pregunta;
SELECT 'Reglas: '     || COUNT(*) FROM sf.flujo_regla;
SELECT 'Catálogos: '  || COUNT(*) FROM sf.catalogo;

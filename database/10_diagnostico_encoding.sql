-- =============================================================================
-- DIAGNÓSTICO: Verificar estado real del encoding en BD de producción
-- Ejecutar con:
--   & "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U sgiform -d sgiform -f "database\10_diagnostico_encoding.sql"
-- =============================================================================

-- 1. Encoding del servidor y cliente
SHOW server_encoding;
SHOW client_encoding;

-- 2. Estado ACTUAL de las preguntas (hex de los primeros 20 bytes del texto)
SELECT codigo,
       encode(convert_to(texto, 'UTF8'), 'hex') AS texto_hex,
       length(texto) AS len
FROM sf.flujo_pregunta
WHERE codigo IN ('p_foto_fachada', 'p_estado_medidor', 'p_fotos_medidor')
ORDER BY codigo;

-- 3. Texto legible
SELECT codigo, texto
FROM sf.flujo_pregunta
WHERE codigo IN ('p_foto_fachada', 'p_estado_medidor', 'p_fotos_medidor')
ORDER BY codigo;

-- =============================================================================
-- FIX: Actualizar directamente usando bytestring para evitar problemas de encoding
-- (usa E'\xc3\xad' etc para insertar bytes UTF-8 explícitos)
-- =============================================================================
SET client_encoding = 'UTF8';

UPDATE sf.flujo_pregunta SET texto = E'Fotograf\u00EDa de la fachada del domicilio'
WHERE id = '70000000-0000-0000-0000-000000000003';

UPDATE sf.flujo_pregunta SET texto = E'Estado f\u00EDsico del medidor'
WHERE id = '70000000-0000-0000-0000-000000000011';

UPDATE sf.flujo_pregunta SET texto = E'El sello de seguridad est\u00E1 presente e intacto?'
WHERE id = '70000000-0000-0000-0000-000000000012';

UPDATE sf.flujo_pregunta SET texto = E'N\u00FAmero de serie del medidor (verificar con el registrado)'
WHERE id = '70000000-0000-0000-0000-000000000013';

UPDATE sf.flujo_pregunta SET texto = E'Se detectan da\u00F1os en el medidor?'
WHERE id = '70000000-0000-0000-0000-000000000014';

UPDATE sf.flujo_pregunta SET texto = E'Fotograf\u00EDa del display de lectura'
WHERE id = '70000000-0000-0000-0000-000000000022';

UPDATE sf.flujo_pregunta SET texto = E'Lectura actual del medidor (m\u00B3)'
WHERE id = '70000000-0000-0000-0000-000000000021';

UPDATE sf.flujo_pregunta SET texto = E'Se detectan anomal\u00EDas en la instalaci\u00F3n?'
WHERE id = '70000000-0000-0000-0000-000000000030';

UPDATE sf.flujo_pregunta SET texto = E'Tipo(s) de anomal\u00EDa detectada(s)'
WHERE id = '70000000-0000-0000-0000-000000000031';

UPDATE sf.flujo_pregunta SET texto = E'Descripci\u00F3n detallada de la anomal\u00EDa'
WHERE id = '70000000-0000-0000-0000-000000000032';

UPDATE sf.flujo_pregunta SET texto = E'Fotograf\u00EDas del medidor (m\u00EDnimo 2)'
WHERE id = '70000000-0000-0000-0000-000000000040';

UPDATE sf.flujo_pregunta SET texto = E'Fotograf\u00EDas de anomal\u00EDas detectadas'
WHERE id = '70000000-0000-0000-0000-000000000041';

UPDATE sf.flujo_pregunta SET texto = E'Coordenadas GPS de la ubicaci\u00F3n del medidor'
WHERE id = '70000000-0000-0000-0000-000000000042';

-- Secciones
UPDATE sf.flujo_seccion SET titulo = E'Anomal\u00EDas Detectadas'
WHERE id = '60000000-0000-0000-0000-000000000004';

UPDATE sf.flujo_seccion SET titulo = E'Evidencia Fotogr\u00E1fica'
WHERE id = '60000000-0000-0000-0000-000000000005';

UPDATE sf.flujo_seccion SET titulo = E'Cierre de Inspecci\u00F3n'
WHERE id = '60000000-0000-0000-0000-000000000006';

-- Opciones
UPDATE sf.flujo_opcion SET texto = E'Cr\u00EDtico / Inutilizable'
WHERE pregunta_id = '70000000-0000-0000-0000-000000000011' AND codigo = 'CRITICO';

UPDATE sf.flujo_opcion SET texto = E'Corrosi\u00F3n / Oxidaci\u00F3n'
WHERE pregunta_id = '70000000-0000-0000-0000-000000000015' AND codigo = 'CORROSION';

UPDATE sf.flujo_opcion SET texto = E'Medidor da\u00F1ado'
WHERE pregunta_id = '70000000-0000-0000-0000-000000000031' AND codigo = 'MEDIDOR_DANADO';

UPDATE sf.flujo_opcion SET texto = E'Otra anomal\u00EDa'
WHERE pregunta_id = '70000000-0000-0000-0000-000000000031' AND codigo = 'OTRO';

-- tipo_inspeccion
UPDATE sf.tipo_inspeccion SET
    nombre = E'Inspecci\u00F3n de Medidor',
    descripcion = E'Inspecci\u00F3n t\u00E9cnica completa de medidor de agua. Verifica estado, lectura, sellos y anomal\u00EDas.'
WHERE codigo = 'INSP-MED';

UPDATE sf.tipo_inspeccion SET
    nombre = E'Verificaci\u00F3n de Lectura',
    descripcion = E'Verificaci\u00F3n de la lectura del medidor y comparaci\u00F3n con lectura anterior.'
WHERE codigo = 'VER-LEC';

UPDATE sf.tipo_inspeccion SET
    nombre = E'Detecci\u00F3n de Anomal\u00EDas',
    descripcion = E'Inspecci\u00F3n enfocada en detectar conexiones irregulares, fugas y anomal\u00EDas t\u00E9cnicas.'
WHERE codigo = 'DET-ANOM';

UPDATE sf.tipo_inspeccion SET
    nombre = E'Inspecci\u00F3n T\u00E9cnica Domiciliaria',
    descripcion = E'Inspecci\u00F3n integral del servicio domiciliario incluyendo medidor, conexiones y estado general.'
WHERE codigo = 'INSP-DOM';

UPDATE sf.tipo_inspeccion SET
    nombre = E'Validaci\u00F3n Catastral',
    descripcion = E'Validaci\u00F3n y actualizaci\u00F3n de datos catastrales del servicio: direcci\u00F3n, nombre, datos t\u00E9cnicos.'
WHERE codigo = 'VAL-CAT';

-- =============================================================================
-- VERIFICACIÓN POST-FIX
-- =============================================================================
SELECT codigo, encode(convert_to(texto,'UTF8'),'hex') AS hex, texto
FROM sf.flujo_pregunta
WHERE codigo IN ('p_foto_fachada','p_estado_medidor','p_fotos_medidor')
ORDER BY codigo;

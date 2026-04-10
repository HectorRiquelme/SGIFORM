-- =============================================================================
-- FIX: Restaurar caracteres españoles en datos del flujo de ejemplo
-- Causa: seed ejecutado con client_encoding incorrecto → caracteres acentuados
--        almacenados como U+FFFD (carácter de reemplazo Unicode)
-- Servidor 2: apps.solucionescloud.cl
-- Ejecutar con:
--   psql -U sgiform -d sgiform -f 09_fix_encoding_flujos.sql
-- O desde PowerShell en el servidor:
--   & "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U sgiform -d sgiform -f "C:\Aplicaciones\sgiform\src\database\09_fix_encoding_flujos.sql"
-- =============================================================================

SET client_encoding = 'UTF8';

-- -----------------------------------------------------------------------------
-- tipo_inspeccion (nombre + descripcion)
-- -----------------------------------------------------------------------------
UPDATE sf.tipo_inspeccion SET
    nombre      = 'Inspección de Medidor',
    descripcion = 'Inspección técnica completa de medidor de agua. Verifica estado, lectura, sellos y anomalías.'
WHERE codigo = 'INSP-MED';

UPDATE sf.tipo_inspeccion SET
    nombre      = 'Verificación de Lectura',
    descripcion = 'Verificación de la lectura del medidor y comparación con lectura anterior.'
WHERE codigo = 'VER-LEC';

UPDATE sf.tipo_inspeccion SET
    nombre      = 'Detección de Anomalías',
    descripcion = 'Inspección enfocada en detectar conexiones irregulares, fugas y anomalías técnicas.'
WHERE codigo = 'DET-ANOM';

UPDATE sf.tipo_inspeccion SET
    nombre      = 'Inspección Técnica Domiciliaria',
    descripcion = 'Inspección integral del servicio domiciliario incluyendo medidor, conexiones y estado general.'
WHERE codigo = 'INSP-DOM';

UPDATE sf.tipo_inspeccion SET
    nombre      = 'Validación Catastral',
    descripcion = 'Validación y actualización de datos catastrales del servicio: dirección, nombre, datos técnicos.'
WHERE codigo = 'VAL-CAT';

-- -----------------------------------------------------------------------------
-- flujo (nombre + descripcion)
-- -----------------------------------------------------------------------------
UPDATE sf.flujo SET
    nombre      = 'Flujo Inspección de Medidor v1',
    descripcion = 'Flujo completo para inspección técnica de medidor de agua domiciliario'
WHERE id = '40000000-0000-0000-0000-000000000001';

-- -----------------------------------------------------------------------------
-- flujo_version (descripcion_cambio)
-- -----------------------------------------------------------------------------
UPDATE sf.flujo_version SET
    descripcion_cambio = 'Versión inicial del flujo de inspección de medidor'
WHERE id = '50000000-0000-0000-0000-000000000001';

-- -----------------------------------------------------------------------------
-- flujo_seccion (titulo)
-- -----------------------------------------------------------------------------
UPDATE sf.flujo_seccion SET titulo = 'Anomalías Detectadas'
WHERE id = '60000000-0000-0000-0000-000000000004';

UPDATE sf.flujo_seccion SET titulo = 'Evidencia Fotográfica'
WHERE id = '60000000-0000-0000-0000-000000000005';

UPDATE sf.flujo_seccion SET titulo = 'Cierre de Inspección'
WHERE id = '60000000-0000-0000-0000-000000000006';

-- -----------------------------------------------------------------------------
-- flujo_pregunta (texto)
-- -----------------------------------------------------------------------------
UPDATE sf.flujo_pregunta SET texto = 'Fotografía de la fachada del domicilio'
WHERE id = '70000000-0000-0000-0000-000000000003';

UPDATE sf.flujo_pregunta SET texto = 'Estado físico del medidor'
WHERE id = '70000000-0000-0000-0000-000000000011';

UPDATE sf.flujo_pregunta SET texto = 'El sello de seguridad está presente e intacto?'
WHERE id = '70000000-0000-0000-0000-000000000012';

UPDATE sf.flujo_pregunta SET texto = 'Número de serie del medidor (verificar con el registrado)'
WHERE id = '70000000-0000-0000-0000-000000000013';

UPDATE sf.flujo_pregunta SET texto = 'Fotografía del display de lectura'
WHERE id = '70000000-0000-0000-0000-000000000022';

UPDATE sf.flujo_pregunta SET texto = 'Se detectan anomalías en la instalación?'
WHERE id = '70000000-0000-0000-0000-000000000030';

UPDATE sf.flujo_pregunta SET texto = 'Tipo(s) de anomalía detectada(s)'
WHERE id = '70000000-0000-0000-0000-000000000031';

UPDATE sf.flujo_pregunta SET texto = 'Descripción detallada de la anomalía'
WHERE id = '70000000-0000-0000-0000-000000000032';

UPDATE sf.flujo_pregunta SET texto = 'Fotografías del medidor (mínimo 2)'
WHERE id = '70000000-0000-0000-0000-000000000040';

UPDATE sf.flujo_pregunta SET texto = 'Fotografías de anomalías detectadas'
WHERE id = '70000000-0000-0000-0000-000000000041';

UPDATE sf.flujo_pregunta SET texto = 'Coordenadas GPS de la ubicación del medidor'
WHERE id = '70000000-0000-0000-0000-000000000042';

-- -----------------------------------------------------------------------------
-- flujo_opcion (texto)
-- -----------------------------------------------------------------------------
UPDATE sf.flujo_opcion SET texto = 'Crítico / Inutilizable'
WHERE pregunta_id = '70000000-0000-0000-0000-000000000011' AND codigo = 'CRITICO';

UPDATE sf.flujo_opcion SET texto = 'Corrosión / Oxidación'
WHERE pregunta_id = '70000000-0000-0000-0000-000000000015' AND codigo = 'CORROSION';

UPDATE sf.flujo_opcion SET texto = 'Medidor dañado'
WHERE pregunta_id = '70000000-0000-0000-0000-000000000031' AND codigo = 'MEDIDOR_DANADO';

UPDATE sf.flujo_opcion SET texto = 'Otra anomalía'
WHERE pregunta_id = '70000000-0000-0000-0000-000000000031' AND codigo = 'OTRO';

-- -----------------------------------------------------------------------------
-- Verificación
-- -----------------------------------------------------------------------------
SELECT 'flujo_seccion' AS tabla, codigo, titulo AS texto
FROM sf.flujo_seccion
WHERE flujo_version_id = '50000000-0000-0000-0000-000000000001'
ORDER BY orden;

SELECT 'flujo_pregunta' AS tabla, codigo, texto
FROM sf.flujo_pregunta
WHERE flujo_version_id = '50000000-0000-0000-0000-000000000001'
  AND texto ~ '[áéíóúñÁÉÍÓÚÑ]'
ORDER BY orden;

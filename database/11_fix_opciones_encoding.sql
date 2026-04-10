-- =============================================================================
-- FIX: Opciones con doble-encoding (Ã±, Ã­, Ã³)
-- Causa: bytes UTF-8 C3+B1/AD/B3 almacenados como 2 chars Latin-1 en vez de 1 char Unicode
-- Servidor 2: apps.solucionescloud.cl
-- Ejecutar con:
--   & "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U sgiform -d sgiform -f "database\11_fix_opciones_encoding.sql"
-- =============================================================================

SET client_encoding = 'UTF8';

-- p_estado_medidor: BUENO, MALO (ñ = U+00F1)
UPDATE sf.flujo_opcion SET texto = E'Bueno / Sin da\u00F1os'
WHERE pregunta_id = '70000000-0000-0000-0000-000000000011' AND codigo = 'BUENO';

UPDATE sf.flujo_opcion SET texto = E'Malo / Con da\u00F1os'
WHERE pregunta_id = '70000000-0000-0000-0000-000000000011' AND codigo = 'MALO';

-- p_tipo_dano: ROTACION (í = U+00ED), MODIFICADO (ó = U+00F3)
UPDATE sf.flujo_opcion SET texto = E'D\u00EDgitos trabados'
WHERE pregunta_id = '70000000-0000-0000-0000-000000000015' AND codigo = 'ROTACION';

UPDATE sf.flujo_opcion SET texto = E'Posible manipulaci\u00F3n'
WHERE pregunta_id = '70000000-0000-0000-0000-000000000015' AND codigo = 'MODIFICADO';

-- p_tipo_anomalia: CONN_IRREGULAR (ó = U+00F3)
UPDATE sf.flujo_opcion SET texto = E'Conexi\u00F3n irregular'
WHERE pregunta_id = '70000000-0000-0000-0000-000000000031' AND codigo = 'CONN_IRREGULAR';

-- Verificación
SELECT p.codigo AS pregunta, o.codigo, o.texto,
       encode(convert_to(o.texto,'UTF8'),'hex') AS hex
FROM sf.flujo_opcion o
JOIN sf.flujo_pregunta p ON o.pregunta_id = p.id
WHERE p.codigo IN ('p_estado_medidor','p_tipo_dano','p_tipo_anomalia')
ORDER BY p.orden, o.orden;

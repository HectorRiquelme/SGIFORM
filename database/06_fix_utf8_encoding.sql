-- ============================================================
-- 06_fix_utf8_encoding.sql
-- Fix de doble-codificación UTF-8 / Latin-1
--
-- Síntoma: textos como "Ã³" en lugar de "ó", "Ã­" en lugar de "í"
-- Causa: psql en Windows leyó el .sql como cp1252 y envió bytes
--   UTF-8 directamente a PostgreSQL que los almacenó como Latin-1.
--   Resultado: 2 bytes (C3 B3 = ó en UTF-8) → 2 caracteres Unicode
--   (U+00C3 = Ã, U+00B3 = ³).
-- Fix: convert_from(convert_to(texto, 'LATIN1'), 'UTF8')
--   Convierte el string a bytes Latin-1 (recupera los bytes originales)
--   y los reinterpreta como UTF-8, obteniendo el carácter correcto.
--
-- IMPORTANTE: ejecutar UNA SOLA VEZ. Si se ejecuta dos veces en
-- texto ya correcto, puede corromperse.
-- La condición WHERE nombre ~ '[À-ÿ]{2}' evita re-procesar texto sano.
-- ============================================================

BEGIN;

-- Función de ayuda para aplicar el fix sólo a strings que contengan
-- secuencias de doble-codificación (detectado por presencia de Ã seguido
-- de un carácter de control o símbolo Latin-1 extendido).
CREATE OR REPLACE FUNCTION sf.fix_double_encoded(t TEXT) RETURNS TEXT AS $$
BEGIN
    -- Solo procesar si contiene el patrón de doble-codificación
    IF t ~ '[ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞß]' THEN
        RETURN convert_from(convert_to(t, 'LATIN1'), 'UTF8');
    END IF;
    RETURN t;
END;
$$ LANGUAGE plpgsql IMMUTABLE;

-- tipo_inspeccion
UPDATE sf.tipo_inspeccion
SET nombre      = sf.fix_double_encoded(nombre),
    descripcion = CASE WHEN descripcion IS NOT NULL THEN sf.fix_double_encoded(descripcion) ELSE NULL END
WHERE nombre ~ '[ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞß]'
   OR descripcion ~ '[ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞß]';

-- flujo
UPDATE sf.flujo
SET nombre      = sf.fix_double_encoded(nombre),
    descripcion = CASE WHEN descripcion IS NOT NULL THEN sf.fix_double_encoded(descripcion) ELSE NULL END
WHERE nombre ~ '[ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞß]'
   OR descripcion ~ '[ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞß]';

-- flujo_version
UPDATE sf.flujo_version
SET notas = CASE WHEN notas IS NOT NULL THEN sf.fix_double_encoded(notas) ELSE NULL END
WHERE notas ~ '[ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞß]';

-- flujo_seccion
UPDATE sf.flujo_seccion
SET titulo      = sf.fix_double_encoded(titulo),
    descripcion = CASE WHEN descripcion IS NOT NULL THEN sf.fix_double_encoded(descripcion) ELSE NULL END
WHERE titulo ~ '[ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞß]'
   OR descripcion ~ '[ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞß]';

-- flujo_pregunta
UPDATE sf.flujo_pregunta
SET texto       = sf.fix_double_encoded(texto),
    placeholder = CASE WHEN placeholder IS NOT NULL THEN sf.fix_double_encoded(placeholder) ELSE NULL END,
    ayuda       = CASE WHEN ayuda IS NOT NULL THEN sf.fix_double_encoded(ayuda) ELSE NULL END
WHERE texto ~ '[ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞß]'
   OR ayuda ~ '[ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞß]';

-- flujo_opcion
UPDATE sf.flujo_opcion
SET texto = sf.fix_double_encoded(texto)
WHERE texto ~ '[ÀÁÂÃÄåæçèéêëìíîïðñòóôõö×ØÙÚÛÜÝÞß]';

-- operador (nombres y dirección)
UPDATE sf.operador
SET nombre    = sf.fix_double_encoded(nombre),
    apellido  = sf.fix_double_encoded(apellido)
WHERE nombre ~ '[ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞß]'
   OR apellido ~ '[ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞß]';

-- usuario (nombres)
UPDATE sf.usuario
SET nombre   = sf.fix_double_encoded(nombre),
    apellido = sf.fix_double_encoded(apellido)
WHERE nombre ~ '[ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞß]'
   OR apellido ~ '[ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞß]';

-- servicio_inspeccion (nombre_cliente, dirección, localidad)
UPDATE sf.servicio_inspeccion
SET nombre_cliente = CASE WHEN nombre_cliente IS NOT NULL THEN sf.fix_double_encoded(nombre_cliente) ELSE NULL END,
    direccion      = CASE WHEN direccion IS NOT NULL THEN sf.fix_double_encoded(direccion) ELSE NULL END,
    localidad      = CASE WHEN localidad IS NOT NULL THEN sf.fix_double_encoded(localidad) ELSE NULL END
WHERE nombre_cliente ~ '[ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞß]'
   OR direccion ~ '[ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞß]'
   OR localidad ~ '[ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞß]';

-- Limpiar función auxiliar
DROP FUNCTION IF EXISTS sf.fix_double_encoded(TEXT);

COMMIT;

-- Verificar resultado
SELECT codigo, nombre FROM sf.tipo_inspeccion ORDER BY codigo;

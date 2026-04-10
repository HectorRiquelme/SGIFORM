-- =============================================================================
-- FIX: Actualizar hashes de contraseñas en producción
-- Servidor 2: apps.solucionescloud.cl
-- Ejecutar con:
--   psql -U sgiform -d sgiform -f 08_fix_passwords.sql
-- O desde PowerShell en el servidor:
--   & "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U sgiform -d sgiform -f "C:\Aplicaciones\sgiform\src\database\08_fix_passwords.sql"
-- =============================================================================

-- Admin web: admin@sanitaria-demo.cl / Admin@2024!
UPDATE sf.usuario
SET password_hash = '$2a$12$60/BqND1zCUTF9gj2JlL0uJaTDnc0lUlcPjPXFCNXQ64/dMggPSgS'
WHERE email = 'admin@sanitaria-demo.cl';

-- Operadores móvil: OP001 y OP002 / Op@123
UPDATE sf.operador
SET password_hash = '$2a$12$4P/X98WmyJCHIHhYs4uzsOZAAZi/N0TOD7umsb7V3jPVWQWT7BK66'
WHERE codigo_operador IN ('OP001', 'OP002');

-- Verificar
SELECT 'usuario' as tabla, email, LEFT(password_hash, 20) as hash_preview FROM sf.usuario WHERE email = 'admin@sanitaria-demo.cl'
UNION ALL
SELECT 'operador', codigo_operador, LEFT(password_hash, 20) FROM sf.operador WHERE codigo_operador IN ('OP001','OP002');

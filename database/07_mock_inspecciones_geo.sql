-- =============================================================================
-- DATOS MOCK: Inspecciones con GPS para módulo Georreferencia Histórica
-- =============================================================================

DO
LANGUAGE plpgsql
$SEED$
DECLARE
  v_empresa    UUID := '00000000-0000-0000-0000-000000000001';
  v_op1        UUID := 'A0000000-0000-0000-0000-000000000001';
  v_op2        UUID := 'A0000000-0000-0000-0000-000000000002';
  v_flujo_ver  UUID := '50000000-0000-0000-0000-000000000001';
  v_tipo_insp  UUID := '30000000-0000-0000-0000-000000000001';
  v_usuario    UUID;
  v_srv        UUID[];
  v_asig1      UUID := uuid_generate_v4();
  v_asig2      UUID := uuid_generate_v4();
  v_asig3      UUID := uuid_generate_v4();
  v_asig4      UUID := uuid_generate_v4();
  v_asig5      UUID := uuid_generate_v4();
  v_asig6      UUID := uuid_generate_v4();
BEGIN

  SELECT id INTO v_usuario FROM sf.usuario WHERE empresa_id = v_empresa LIMIT 1;

  SELECT ARRAY(
    SELECT id FROM sf.servicio_inspeccion
    WHERE empresa_id = v_empresa AND deleted_at IS NULL
    LIMIT 6
  ) INTO v_srv;

  -- Si no hay suficientes servicios, crear mock
  IF array_length(v_srv, 1) IS NULL OR array_length(v_srv, 1) < 6 THEN
    FOR i IN 1..6 LOOP
      IF v_srv[i] IS NULL THEN
        v_srv[i] := uuid_generate_v4();
        INSERT INTO sf.servicio_inspeccion
          (id, empresa_id, id_servicio, nombre_cliente, direccion, localidad, estado)
        VALUES
          (v_srv[i], v_empresa, 'SRV-MOCK-'||i, 'Cliente Demo '||i,
           'Av. Del Mar '||i||' #'||(100+i*10), 'Coquimbo', 'activo')
        ON CONFLICT DO NOTHING;
      END IF;
    END LOOP;
  END IF;

  -- Asignaciones
  INSERT INTO sf.asignacion_inspeccion
    (id, empresa_id, servicio_inspeccion_id, operador_id, tipo_inspeccion_id,
     flujo_version_id, estado, asignado_por,
     fecha_descarga, fecha_inicio_ejecucion, fecha_finalizacion)
  VALUES
    (v_asig1, v_empresa, v_srv[1], v_op1, v_tipo_insp, v_flujo_ver, 'completada', v_usuario,
     now()-'10 days'::interval, now()-'10 days'::interval+'08:15:00'::interval, now()-'10 days'::interval+'09:42:00'::interval),
    (v_asig2, v_empresa, v_srv[2], v_op1, v_tipo_insp, v_flujo_ver, 'completada', v_usuario,
     now()-'8 days'::interval,  now()-'8 days'::interval+'10:00:00'::interval,  now()-'8 days'::interval+'11:20:00'::interval),
    (v_asig3, v_empresa, v_srv[3], v_op2, v_tipo_insp, v_flujo_ver, 'completada', v_usuario,
     now()-'6 days'::interval,  now()-'6 days'::interval+'09:30:00'::interval,  now()-'6 days'::interval+'10:45:00'::interval),
    (v_asig4, v_empresa, v_srv[4], v_op2, v_tipo_insp, v_flujo_ver, 'completada', v_usuario,
     now()-'4 days'::interval,  now()-'4 days'::interval+'14:00:00'::interval,  now()-'4 days'::interval+'15:10:00'::interval),
    (v_asig5, v_empresa, v_srv[5], v_op1, v_tipo_insp, v_flujo_ver, 'completada', v_usuario,
     now()-'2 days'::interval,  now()-'2 days'::interval+'08:00:00'::interval,  now()-'2 days'::interval+'09:15:00'::interval),
    (v_asig6, v_empresa, v_srv[6], v_op2, v_tipo_insp, v_flujo_ver, 'completada', v_usuario,
     now()-'1 day'::interval,   now()-'1 day'::interval+'11:30:00'::interval,   now()-'1 day'::interval+'12:50:00'::interval)
  ON CONFLICT DO NOTHING;

  -- Inspecciones con GPS (coordenadas zona Coquimbo / La Serena)
  INSERT INTO sf.inspeccion
    (id, empresa_id, asignacion_id, operador_id, servicio_inspeccion_id,
     flujo_version_id, estado,
     fecha_inicio, fecha_fin, duracion_segundos,
     coord_x_inicio, coord_y_inicio, coord_x_fin, coord_y_fin, precision_gps,
     total_preguntas, total_respondidas, device_id, app_version, sincronizado_en)
  VALUES
    -- coord_x = longitud (~-71), coord_y = latitud (~-29)  [convención cartesiana X=lng, Y=lat]
    (uuid_generate_v4(), v_empresa, v_asig1, v_op1, v_srv[1], v_flujo_ver, 'aprobada',
     now()-'10 days'::interval+'08:15:00'::interval,
     now()-'10 days'::interval+'09:42:00'::interval, 5220,
     -71.3380, -29.9520, -71.3381, -29.9522, 4.5,
     12, 12, 'DEVICE-OP1-001', '2.1.0',
     now()-'10 days'::interval+'10:00:00'::interval),

    (uuid_generate_v4(), v_empresa, v_asig2, v_op1, v_srv[2], v_flujo_ver, 'aprobada',
     now()-'8 days'::interval+'10:00:00'::interval,
     now()-'8 days'::interval+'11:20:00'::interval, 4800,
     -71.3450, -29.9610, -71.3452, -29.9612, 3.2,
     12, 12, 'DEVICE-OP1-001', '2.1.0',
     now()-'8 days'::interval+'12:00:00'::interval),

    (uuid_generate_v4(), v_empresa, v_asig3, v_op2, v_srv[3], v_flujo_ver, 'aprobada',
     now()-'6 days'::interval+'09:30:00'::interval,
     now()-'6 days'::interval+'10:45:00'::interval, 4500,
     -71.2490, -29.9055, -71.2491, -29.9057, 5.0,
     12, 11, 'DEVICE-OP2-001', '2.1.0',
     now()-'6 days'::interval+'11:00:00'::interval),

    (uuid_generate_v4(), v_empresa, v_asig4, v_op2, v_srv[4], v_flujo_ver, 'completada',
     now()-'4 days'::interval+'14:00:00'::interval,
     now()-'4 days'::interval+'15:10:00'::interval, 4200,
     -71.2550, -29.9130, -71.2552, -29.9132, 6.1,
     12, 10, 'DEVICE-OP2-001', '2.1.0', NULL),

    (uuid_generate_v4(), v_empresa, v_asig5, v_op1, v_srv[5], v_flujo_ver, 'aprobada',
     now()-'2 days'::interval+'08:00:00'::interval,
     now()-'2 days'::interval+'09:15:00'::interval, 4500,
     -71.3310, -29.9480, -71.3312, -29.9481, 3.8,
     12, 12, 'DEVICE-OP1-001', '2.1.0',
     now()-'2 days'::interval+'10:00:00'::interval),

    (uuid_generate_v4(), v_empresa, v_asig6, v_op2, v_srv[6], v_flujo_ver, 'completada',
     now()-'1 day'::interval+'11:30:00'::interval,
     now()-'1 day'::interval+'12:50:00'::interval, 4800,
     -71.2620, -29.9200, -71.2622, -29.9202, 4.0,
     12, 9, 'DEVICE-OP2-001', '2.1.0', NULL);

  RAISE NOTICE 'Mock geo insertado: 6 asignaciones + 6 inspecciones con GPS';
END;
$SEED$;

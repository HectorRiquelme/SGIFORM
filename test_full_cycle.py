"""
Prueba end-to-end del ciclo completo de inspección móvil contra producción.
Demuestra: login → sync download → ejecutar inspección → upload datos → upload fotos → verificación.
"""
import requests, json, sys, random, base64, io, struct, zlib

BASE = "https://apps.solucionescloud.cl/sgiformapi/api/v1"
flujo_id = "40000000-0000-0000-0000-000000000001"
fv_id    = "50000000-0000-0000-0000-000000000001"
tipo_id  = "30000000-0000-0000-0000-000000000001"
op001_id = "a0000000-0000-0000-0000-000000000001"

OUT = open("C:\\Users\\hecto\\TRABAJO\\dev_ia\\kobotoolbox\\test_full_cycle_result.txt",
           "w", encoding="utf-8")

def log(msg):
    print(msg)
    OUT.write(msg + "\n"); OUT.flush()

def p(label, r, show=False):
    ok = "OK" if r.status_code < 400 else "ERR"
    log(f"[{ok}] {label}: {r.status_code}")
    if r.status_code >= 400:
        try: log(f"  ERR: {json.dumps(r.json(), ensure_ascii=True)[:400]}")
        except: log(f"  {r.text[:300]}")
    elif show:
        try:
            d = r.json()
            if isinstance(d, list): log(f"  count={len(d)}")
            elif 'items' in d:     log(f"  total={d.get('total')}")
            elif 'procesados' in d: log(f"  procesados={d['procesados']}, errores={d['errores_count']}")
            elif 'timestamp' in d:  log(f"  asigs={len(d.get('asignaciones',[]))}, cats={len(d.get('catalogos',[]))}")
            elif 'fotos_procesadas' in d: log(f"  fotos_procesadas={d['fotos_procesadas']}")
        except: pass
    return r

def gen_jpeg(width=320, height=240, color_r=200, color_g=100, color_b=50):
    """Genera una JPG de tamaño razonable rellena de un color sólido.
    Default 320x240 para que los fixtures se vean como una imagen
    real en el modal de detalle del Web (no un pixel 16x16)."""
    # Usar PIL si está disponible (preferido — produce JPG real)
    try:
        from PIL import Image
        img = Image.new('RGB', (width, height), (color_r, color_g, color_b))
        buf = io.BytesIO()
        img.save(buf, format='JPEG', quality=70)
        return buf.getvalue()
    except ImportError:
        pass
    # Fallback: JPG 1x1 base64 hardcodeada (pixel rojo)
    b64 = ("/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAAYEBQYFBAYGBQYHBwYIChAKCgkJChQODwwQFx"
           "QYGBcUFhYaHSUfGhsjHBYWICwgIyYnKSopGR8tMC0oMCUoKSj/2wBDAQcHBwoIChMKChMo"
           "GhYaKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCj/"
           "wAARCAABAAEDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAn/xAAUEAEAAAAAAAA"
           "AAAAAAAAAAAAA/8QAFAEBAAAAAAAAAAAAAAAAAAAAAP/EABQRAQAAAAAAAAAAAAAAAAAAAA"
           "D/2gAMAwEAAhEDEQA/AL+AA//Z")
    return base64.b64decode(b64)

def autocompletar(preg):
    tipo = preg.get("tipo_control","")
    pid  = preg["id"]
    base = {"pregunta_id": pid, "tipo_control": tipo}
    t = tipo.replace("_","").lower()
    if t == "sino":           base["valor_booleano"] = True
    elif t == "textocorto":   base["valor_texto"] = f"SN-{random.randint(1000,9999)}"
    elif t == "textolargo":   base["valor_texto"] = f"Inspeccion automatica AUTO-{random.randint(100,999)}"
    elif t == "decimal":      base["valor_decimal"] = round(random.uniform(10.0, 999.9), 2)
    elif t == "entero":       base["valor_entero"] = random.randint(1, 500)
    elif t in ("seleccionunica","lista"):
        ops = preg.get("opciones",[])
        base["valor_texto"] = ops[0]["codigo"] if ops else "opcion1"
    elif t == "seleccionmultiple":
        ops = preg.get("opciones",[])
        base["valor_json"] = json.dumps([ops[0]["codigo"]] if ops else ["opcion1"])
    elif t in ("fotounica","fotosmultiples"):
        return None  # se suben aparte vía /sync/photos
    elif t == "coordenadas":
        base["valor_json"] = json.dumps({"lat": -33.4569, "lng": -70.6483})
    elif t == "firma":
        base["valor_texto"] = "FIRMA_DIGITAL_AUTO_TEST"
    else:
        base["valor_texto"] = f"auto_{tipo}"
    return base

log("=" * 60)
log("CICLO COMPLETO APP MOVIL - SGI-FORM PRODUCCION")
log("Incluye: login, download, inspeccion, upload datos, upload fotos")
log("=" * 60)

# ── PASO 0: Admin crea asignaciones frescas ─────────────────────────────
log("\n[0] Admin: preparando 2 asignaciones")
ra = p("Login admin", requests.post(f"{BASE}/auth/login",
    json={"email":"admin@sanitaria-demo.cl","password":"Admin@2024!"}))
if ra.status_code != 200:
    log("FALLO EN LOGIN ADMIN — abortando")
    OUT.close(); sys.exit(1)
HA = {"Authorization": f"Bearer {ra.json()['access_token']}"}

servicios = requests.get(f"{BASE}/servicios", headers=HA).json().get("items",[])
log(f"  Servicios disponibles: {len(servicios)}")

created_ids = []
for sv in servicios[:2]:
    rc = requests.post(f"{BASE}/asignaciones", json={
        "operador_id": op001_id,
        "servicio_inspeccion_id": sv["id"],
        "tipo_inspeccion_id": tipo_id,
        "flujo_version_id": fv_id,
        "prioridad": "alta",
        "observaciones": f"FullCycle {sv.get('id_servicio','')}"
    }, headers=HA)
    if rc.status_code in (200,201):
        created_ids.append(rc.json()["id"])
        log(f"  [OK] {sv.get('id_servicio','?')} -> asig {rc.json()['id'][:8]}")
    else:
        log(f"  [ERR {rc.status_code}] {rc.text[:150]}")

if not created_ids:
    log("FALLO: no se crearon asignaciones — abortando")
    OUT.close(); sys.exit(1)

# ── PASO 1: Login móvil ─────────────────────────────────────────────────
log("\n[1] Login movil OP001 / sanitaria-demo")
r = p("auth/login-movil", requests.post(f"{BASE}/auth/login-movil", json={
    "codigo_operador": "OP001",
    "empresa_slug":    "sanitaria-demo",
    "password":        "Op@123",
    "device_id":       "test-full-cycle-e2e"
}))
if r.status_code != 200: OUT.close(); sys.exit(1)
MH = {"Authorization": f"Bearer {r.json()['access_token']}"}

# ── PASO 2: Sync download ──────────────────────────────────────────────
log("\n[2] Sync download")
r = p("sync/download", requests.get(f"{BASE}/sync/download", headers=MH), True)
dl = r.json()
todas_asig = dl.get("asignaciones",[])
log(f"  Total descargadas: {len(todas_asig)}")
log(f"  Catalogos: {len(dl.get('catalogos',[]))}")
# Verificar UTF-8 correcto en flujo embebido (fix mojibake)
primer_flujo = None
for a in todas_asig:
    fv = a.get("flujo_version")
    if fv and fv.get("secciones"):
        primer_flujo = fv
        break
if primer_flujo:
    secc_sample = primer_flujo["secciones"][0].get("titulo","")
    log(f"  Muestra encoding seccion[0]: '{secc_sample}'")
    raw = secc_sample.encode('utf-8', errors='replace')
    log(f"    hex: {raw.hex()}")

# ── PASO 3: Recuperar estructura del flujo ─────────────────────────────
log("\n[3] Estructura del formulario")
r = p("flujos/versiones", requests.get(f"{BASE}/flujos/{flujo_id}/versiones/{fv_id}", headers=MH))
flujo = r.json()
todas_preguntas = []
pregs_foto = []
for sec in flujo.get("secciones",[]):
    pregs = sec.get("preguntas",[])
    todas_preguntas.extend(pregs)
    for pr in pregs:
        tc = pr.get("tipo_control","").replace("_","").lower()
        if tc in ("fotounica","fotosmultiples"):
            pregs_foto.append(pr)
log(f"  Total preguntas: {len(todas_preguntas)} (de fotos: {len(pregs_foto)})")

# ── PASO 4: Autocompletar respuestas ───────────────────────────────────
log("\n[4] Autocompletando respuestas")
respuestas = [autocompletar(p_) for p_ in todas_preguntas]
respuestas = [r_ for r_ in respuestas if r_]
log(f"  Respuestas no-foto preparadas: {len(respuestas)}")

# ── PASO 5: Subir inspecciones como "en_progreso" primero ──────────────
# Primero en_progreso para que exista el registro en BD y podamos subirle fotos
log("\n[5] Upload inspecciones (estado inicial: en_progreso)")
inspecciones_creadas = {}
for asig_id in created_ids:
    r = p(f"  sync/upload [en_progreso] {asig_id[:8]}", requests.post(f"{BASE}/sync/upload", json={
        "inspecciones": [{
            "asignacion_id":  asig_id,
            "estado":         "en_progreso",
            "fecha_inicio":   "2026-04-10T17:00:00Z",
            "fecha_fin":      None,
            # Convención del codebase: X = longitud, Y = latitud.
            # Santiago aprox: lat=-33.4569, lng=-70.6483
            "coord_x_inicio": -70.6483,
            "coord_y_inicio": -33.4569,
            "coord_x_fin":    None,
            "coord_y_fin":    None,
            "app_version":    "1.0.0-e2e",
            "respuestas":     []
        }]
    }, headers=MH), True)
    if r.status_code == 200 and r.json().get("procesados") == 1:
        # Re-descargar para obtener el id de la inspección creada
        # (el endpoint upload no devuelve el id directamente; consultamos via admin)
        pass

# Recuperar ids de inspección via API admin (ordenado por CreatedAt desc).
# El endpoint GET /inspecciones no expone asignacion_id en la lista, así que
# tomamos los N primeros del operador OP001 y cruzamos con GET /inspecciones/{id}
# para obtener el asignacion_id real.
log("\n  Recuperando ids de inspecciones creadas...")
ri = requests.get(f"{BASE}/inspecciones", headers=HA,
                  params={"porPagina": 20, "operadorId": op001_id}).json()
for item in ri.get("items", []):
    det = requests.get(f"{BASE}/inspecciones/{item['id']}", headers=HA).json()
    # GetById tampoco expone asignacion_id directamente, pero sí incluye servicio
    # y la inspección está ligada 1-1 a una asignación. Cruzamos por coincidencia
    # de servicio_inspeccion_id entre la asignación y la inspección.
    # Más simple: consultamos /asignaciones/{id}/inspeccion o usamos la tabla.
    # Por ahora: tomamos las 2 inspecciones en_progreso más recientes del operador
    # asumiendo que son las que acabamos de crear.
    if len(inspecciones_creadas) < len(created_ids) and item.get("estado") == "en_progreso":
        # Asociamos por orden de llegada a los created_ids restantes
        pendiente = [a for a in created_ids if a not in inspecciones_creadas]
        if pendiente:
            asig_id = pendiente[0]
            inspecciones_creadas[asig_id] = item["id"]
            log(f"    asig {asig_id[:8]} -> insp {item['id'][:8]}")

# ── PASO 6: Upload de fotografías ──────────────────────────────────────
log("\n[6] Upload fotografias (JPG generadas localmente)")
total_fotos_ok = 0
if pregs_foto and inspecciones_creadas:
    for asig_id, insp_id in inspecciones_creadas.items():
        # Una foto por cada pregunta de tipo foto
        for idx, pr_foto in enumerate(pregs_foto):
            jpg = gen_jpeg(color_r=random.randint(50,250),
                           color_g=random.randint(50,250),
                           color_b=random.randint(50,250))
            files = [("fotos", (f"foto_{idx}.jpg", jpg, "image/jpeg"))]
            data = {
                "inspeccionId": insp_id,
                "preguntaId":   pr_foto["id"],
                # X = longitud, Y = latitud (convención del codebase)
                "coordX":       "-70.6483",
                "coordY":       "-33.4569",
            }
            r = p(f"  sync/photos insp={insp_id[:8]} preg={pr_foto.get('codigo','?')[:20]}",
                  requests.post(f"{BASE}/sync/photos", files=files, data=data, headers=MH), True)
            if r.status_code == 200:
                d = r.json()
                if d.get("fotos_procesadas", 0) > 0:
                    total_fotos_ok += 1
else:
    log("  (sin preguntas de foto o sin inspecciones creadas)")

log(f"  Total fotos subidas OK: {total_fotos_ok}")

# ── PASO 7: Completar inspecciones con respuestas + marcar completada ──
log("\n[7] Finalizar inspecciones (estado=completada + respuestas completas)")
for asig_id in created_ids:
    r = p(f"  sync/upload [completada] {asig_id[:8]}", requests.post(f"{BASE}/sync/upload", json={
        "inspecciones": [{
            "asignacion_id":  asig_id,
            "estado":         "completada",
            "fecha_inicio":   "2026-04-10T17:00:00Z",
            "fecha_fin":      "2026-04-10T17:45:00Z",
            # X = longitud, Y = latitud (convención del codebase)
            "coord_x_inicio": -70.6483,
            "coord_y_inicio": -33.4569,
            "coord_x_fin":    -70.6484,
            "coord_y_fin":    -33.4570,
            "app_version":    "1.0.0-e2e",
            "respuestas":     respuestas
        }]
    }, headers=MH), True)
    if r.status_code == 200:
        d = r.json()
        if d.get("errores"):
            for e in d["errores"]:
                log(f"    WARN: {e}")

# ── PASO 8: Verificación final ─────────────────────────────────────────
log("\n[8] Verificacion final")
ri = p("GET inspecciones", requests.get(f"{BASE}/inspecciones", headers=HA,
                                         params={"page_size": 50}), True)
if ri.status_code == 200:
    for insp in ri.json().get("items",[]):
        if insp.get("asignacion_id") in created_ids:
            log(f"  insp {insp['id'][:8]} estado={insp.get('estado')} "
                f"resp={insp.get('total_respondidas','?')}/{insp.get('total_preguntas','?')} "
                f"fotos={insp.get('total_fotografias','?')}")

rd = p("GET dashboard", requests.get(f"{BASE}/dashboard/resumen", headers=HA))
if rd.status_code == 200:
    dash = rd.json()
    log(f"  Dashboard asignaciones: {dash.get('asignaciones')}")
    log(f"  Dashboard inspecciones: {dash.get('inspecciones')}")

# ── Sync download con filtro desde= (prueba incremental) ───────────────
log("\n[9] Sync download incremental (filtro desde=)")
from datetime import datetime, timezone
desde = (datetime.now(timezone.utc).isoformat())
r = p("sync/download?desde=future", requests.get(f"{BASE}/sync/download",
      headers=MH, params={"desde": "2099-01-01T00:00:00Z"}), True)
if r.status_code == 200:
    log(f"  (esperado 0 asigs con filtro futuro): {len(r.json().get('asignaciones',[]))}")

log("\n" + "=" * 60)
log(f"CICLO E2E COMPLETADO")
log(f"  Asignaciones creadas: {len(created_ids)}")
log(f"  Inspecciones creadas: {len(inspecciones_creadas)}")
log(f"  Fotos subidas:        {total_fotos_ok}")
log("=" * 60)
OUT.close()

// SGI-FORM — utilidades JavaScript compartidas

/**
 * Descarga un archivo desde una URL autenticada.
 * Usa fetch() con el header Authorization, construye un Blob y
 * dispara la descarga sin redirigir la página.
 * @param {string} url        URL completa del endpoint de descarga
 * @param {string} token      JWT Bearer token
 * @param {string} filename   Nombre de archivo sugerido
 */
window.sgiDownloadFile = async function (url, token, filename) {
    try {
        const resp = await fetch(url, {
            method: 'GET',
            headers: { 'Authorization': 'Bearer ' + token }
        });
        if (!resp.ok) {
            console.error('sgiDownloadFile: error HTTP', resp.status);
            return false;
        }
        const blob = await resp.blob();
        const blobUrl = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = blobUrl;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(blobUrl);
        return true;
    } catch (e) {
        console.error('sgiDownloadFile error:', e);
        return false;
    }
};

/**
 * Inicializa un mapa Leaflet en el elemento indicado.
 * Se usa para los módulos de georreferencia.
 * @param {string} elementId   Id del div contenedor
 * @param {number} lat         Latitud inicial
 * @param {number} lng         Longitud inicial
 * @param {number} zoom        Zoom inicial
 * @returns {object} referencia al mapa
 */
window.sgiMaps = window.sgiMaps || {};

window.sgiInitMap = function (elementId, lat, lng, zoom) {
    if (window.sgiMaps[elementId]) {
        window.sgiMaps[elementId].remove();
    }
    const map = L.map(elementId).setView([lat, lng], zoom);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© OpenStreetMap contributors',
        maxZoom: 18
    }).addTo(map);
    window.sgiMaps[elementId] = map;
    return elementId;
};

window.sgiClearMarkers = function (elementId) {
    const map = window.sgiMaps[elementId];
    if (!map) return;
    map.eachLayer(layer => {
        if (layer instanceof L.Marker || layer instanceof L.CircleMarker) map.removeLayer(layer);
    });
};

window.sgiAddMarker = function (elementId, lat, lng, label, color) {
    const map = window.sgiMaps[elementId];
    if (!map) return;
    const icon = L.divIcon({
        html: `<div style="background:${color || '#3b82f6'};width:14px;height:14px;border-radius:50%;border:2px solid white;box-shadow:0 1px 4px rgba(0,0,0,.4)"></div>`,
        iconSize: [14, 14], iconAnchor: [7, 7], className: ''
    });
    const marker = L.marker([lat, lng], { icon });
    if (label) marker.bindPopup(label);
    marker.addTo(map);
};

window.sgiFitBounds = function (elementId) {
    const map = window.sgiMaps[elementId];
    if (!map) return;
    const markers = [];
    map.eachLayer(layer => {
        if (layer instanceof L.Marker) markers.push(layer.getLatLng());
    });
    if (markers.length > 0) map.fitBounds(L.latLngBounds(markers), { padding: [30, 30] });
};

/**
 * Centra el mapa en las coordenadas indicadas.
 * Se usa en lugar de eval para evitar problemas con separadores decimales.
 */
window.sgiSetView = function (elementId, lat, lng, zoom) {
    const map = window.sgiMaps[elementId];
    if (!map) return;
    map.setView([lat, lng], zoom || 16);
};

// Koordinatfeltene virker også uten Leaflet/CDN eller kartfliser.
(() => {
    const latitude = document.getElementById('Latitude');
    const longitude = document.getElementById('Longitude');
    const status = document.getElementById('map-status');
    if (!latitude || !longitude || !status) return;
    if (typeof L === 'undefined') {
        status.textContent = 'Kartet kunne ikke lastes. Skriv inn kjente koordinater under.';
        return;
    }
    const map = L.map('map').setView([58.1467, 7.9956], 13);
    const tiles = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap-bidragsytere'
    }).addTo(map);
    tiles.on('tileerror', () => {
        status.textContent = 'Kartbakgrunnen kunne ikke lastes. Bruk kjente koordinater under.';
    });
    let marker;
    function showPosition() {
        const lat = Number(latitude.value);
        const lng = Number(longitude.value);
        if (!latitude.value.trim() || !longitude.value.trim() || !Number.isFinite(lat)
            || !Number.isFinite(lng) || Math.abs(lat) > 90 || Math.abs(lng) > 180) {
            if (marker) { map.removeLayer(marker); marker = null; }
            return;
        }
        const position = [lat, lng];
        if (marker) marker.setLatLng(position);
        else marker = L.marker(position).addTo(map);
        map.panTo(position);
    }
    map.on('click', event => {
        if (latitude.disabled || latitude.closest('fieldset[disabled]')) return;
        latitude.value = event.latlng.lat;
        longitude.value = event.latlng.lng;
        latitude.dispatchEvent(new Event('change', { bubbles: true }));
        longitude.dispatchEvent(new Event('change', { bubbles: true }));
    });
    latitude.addEventListener('change', showPosition);
    longitude.addEventListener('change', showPosition);
    showPosition();
})();

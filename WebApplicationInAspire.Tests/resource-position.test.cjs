const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const script = fs.readFileSync(path.join(__dirname, '../WebApplicationInAspire/wwwroot/js/resource-position.js'), 'utf8');

// Tester lokalt kartbortfall og koordinatsynkronisering uten eksterne karttjenester.
function setup(leaflet = true) {
    const handlers = {}, tileHandlers = {}, positions = [];
    const input = () => ({ _value: '', get value() { return this._value; }, set value(value) { this._value = String(value); },
        addEventListener(name, callback) { this[name] = callback; },
        closest() { return null; }, dispatchEvent() { this.change?.(); } });
    const latitude = input(), longitude = input(), status = {};
    const elements = { Latitude: latitude, Longitude: longitude, 'map-status': status };
    const map = { setView() { return this; }, on(name, callback) { handlers[name] = callback; },
        panTo(position) { positions.push(position); }, removeLayer() {} };
    const context = { document: { getElementById: id => elements[id] }, Event: class { } };
    if (leaflet) context.L = { map: () => map, tileLayer: () => ({ addTo() { return this; },
        on(name, callback) { tileHandlers[name] = callback; } }), marker: () => ({ addTo() { return this; }, setLatLng() {} }) };
    vm.runInNewContext(script, context);
    return { latitude, longitude, status, handlers, tileHandlers, positions };
}

test('Missing Leaflet shows fallback and leaves coordinates usable', () => {
    const s = setup(false);
    assert.match(s.status.textContent, /kjente koordinater/);
    s.latitude.value = '58'; s.longitude.value = '8';
    assert.equal(s.latitude.value, '58');
});
test('Tile failure explains manual fallback', () => {
    const s = setup(); s.tileHandlers.tileerror();
    assert.match(s.status.textContent, /Kartbakgrunnen/);
});
test('Typed valid coordinates update marker while invalid values do not', () => {
    const s = setup(); s.latitude.value = '100'; s.longitude.value = '8'; s.latitude.change();
    assert.equal(s.positions.length, 0);
    s.latitude.value = '58.1467'; s.latitude.change();
    assert.equal(s.positions.length, 1); assert.equal(s.positions[0][0], 58.1467);
});
test('Map click writes coordinates but cannot modify an assigned disabled form', () => {
    const s = setup(); s.handlers.click({ latlng: { lat: 58, lng: 8 } });
    assert.equal(s.latitude.value, '58'); assert.equal(s.longitude.value, '8');
    s.latitude.disabled = true; s.handlers.click({ latlng: { lat: 59, lng: 9 } });
    assert.equal(s.latitude.value, '58');
});

const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const script = fs.readFileSync(path.join(__dirname, '../WebApplicationInAspire/wwwroot/js/area-picker.js'), 'utf8');

// Isolert komponenttest: ingen nettleser, kartfliser eller faktiske brukerdata.
function setup({ leaflet = true, open = true, bounds = ['', '', '', ''] } = {}) {
    const element = () => ({ value: '', hidden: false, disabled: false, textContent: '', events: {}, attributes: {},
        addEventListener(name, callback) { this.events[name] = callback; },
        setAttribute(name, value) { this.attributes[name] = value; }, focus() { this.focused = true; } });
    const fields = Object.fromEntries(['AreaSouth', 'AreaWest', 'AreaNorth', 'AreaEast', 'Latitude', 'Longitude'].map(n => [n, element()]));
    ['AreaSouth', 'AreaWest', 'AreaNorth', 'AreaEast'].forEach((n, i) => fields[n].value = bounds[i]);
    const form = element(), status = element(), draw = element(), point = element(), cancel = element(), clear = element(), pointFields = element();
    const selectors = { '[data-area-status]': status, '[data-draw-area]': draw, '[data-pick-point]': point,
        '[data-cancel-area]': cancel, '[data-clear-area]': clear, '[data-point-fields]': pointFields, '[data-area-map]': element() };
    const root = Object.assign(element(), { open, closest: () => form,
        querySelector(selector) { const name = /^\[name="(.+)"\]$/.exec(selector)?.[1]; return name ? fields[name] : selectors[selector]; } });
    const mapEvents = {}, tileEvents = {}, rectangles = [], fitted = [];
    const map = { setView() { return this; }, invalidateSize() {}, removeLayer() {}, fitBounds(bounds) { fitted.push(bounds); },
        getContainer: () => ({ classList: { toggle() {} } }), on(name, callback) { mapEvents[name] = callback; return this; } };
    const layer = () => ({ addTo() { return this; } });
    let mapsCreated = 0;
    const context = { document: { querySelectorAll: selector => selector === '[data-area-picker]' ? [root] : [] } };
    if (leaflet) context.L = { map: () => { mapsCreated++; return map; }, tileLayer: () => ({ addTo() { return this; }, on(name, cb) { tileEvents[name] = cb; return this; } }),
        rectangle(bounds) { rectangles.push(bounds); return { ...layer(), getBounds: () => bounds }; }, marker: layer, circleMarker: layer };
    vm.runInNewContext(script, context);
    return { fields, form, root, status, draw, point, cancel, clear, pointFields, mapEvents, tileEvents, rectangles, fitted,
        get mapsCreated() { return mapsCreated; }, click(lat, lng) { mapEvents.click({ latlng: { lat, lng } }); },
        values() { return ['AreaSouth', 'AreaWest', 'AreaNorth', 'AreaEast'].map(n => String(fields[n].value)); } };
}

test('Incomplete drawing cannot submit and cancel preserves the previous selection', () => {
    const s = setup({ bounds: ['58', '7', '59', '8'] });
    s.draw.events.click(); s.click(60, 9);
    assert.deepEqual(s.values(), ['58', '7', '59', '8']);
    const event = { defaultPrevented: false, preventDefault() { this.defaultPrevented = true; } };
    s.form.events.submit(event);
    assert.equal(event.defaultPrevented, true); assert.equal(s.cancel.focused, true);
    s.cancel.events.click(); assert.deepEqual(s.values(), ['58', '7', '59', '8']);
});

test('Two opposite corners normalize a rectangle; switching to a point clears the area', () => {
    const s = setup(); s.fields.Latitude.value = '58.5'; s.fields.Longitude.value = '7.5';
    s.draw.events.click(); s.click(59, 8); s.click(58, 7);
    assert.deepEqual(s.values(), ['58.000000', '7.000000', '59.000000', '8.000000']);
    assert.equal(s.fields.Latitude.value, ''); assert.equal(s.fields.Longitude.value, ''); assert.equal(s.pointFields.hidden, true);
    assert.match(s.status.textContent, /Område valgt/);
    s.point.events.click(); s.click(58.2, 7.8);
    assert.deepEqual(s.values(), ['', '', '', '']); assert.equal(s.fields.Latitude.value, '58.200000');
    assert.equal(s.pointFields.hidden, false);
});

test('Degenerate corners and Escape do not overwrite a saved area', () => {
    const s = setup({ bounds: ['58', '7', '59', '8'] });
    s.draw.events.click(); s.click(60, 9); s.click(60, 10);
    assert.deepEqual(s.values(), ['58', '7', '59', '8']); assert.match(s.status.textContent, /bredde og høyde/);
    s.root.events.keydown({ key: 'Escape' }); assert.equal(s.cancel.hidden, true);
    assert.deepEqual(s.values(), ['58', '7', '59', '8']);
});

test('Missing Leaflet retains manual coordinates and explains the fallback', () => {
    const s = setup({ leaflet: false, bounds: ['58', '7', '59', '8'] });
    assert.match(s.status.textContent, /kjente koordinater/); assert.equal(s.draw.disabled, true);
    s.fields.AreaNorth.value = '60'; s.fields.AreaNorth.events.change();
    assert.deepEqual(s.values(), ['58', '7', '60', '8']);
});

test('Tile errors preserve the area; clearing removes every coordinate', () => {
    const s = setup({ bounds: ['58', '7', '59', '8'] });
    s.tileEvents.tileerror(); assert.match(s.status.textContent, /Kartbakgrunnen/);
    assert.deepEqual(s.values(), ['58', '7', '59', '8']);
    s.clear.events.click(); assert.deepEqual(s.values(), ['', '', '', '']); assert.match(s.status.textContent, /Regionfilteret/);
});

test('Hidden maps initialize only when opened and reuse the same map on reopening', () => {
    const s = setup({ open: false, bounds: ['58', '7', '59', '8'] });
    assert.equal(s.mapsCreated, 0); s.root.open = true; s.root.events.toggle();
    assert.equal(s.mapsCreated, 1); s.root.events.toggle(); assert.equal(s.mapsCreated, 1);
    assert.ok(s.fitted.length > 0);
});

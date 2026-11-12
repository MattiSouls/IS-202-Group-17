// Kartet som arbeidsflate: behov og ressurser som punkter, lister og registrering i sidepanelet.
(() => {
    const root = document.querySelector('.map-app');
    const dataNode = document.getElementById('map-data');
    if (!root || !dataNode) return;
    const status = document.getElementById('map-status');
    const data = JSON.parse(dataNode.textContent);
    if (typeof L === 'undefined') {
        root.querySelector('.map-app-canvas').textContent = 'Kartet kunne ikke lastes. Bruk listene i sidepanelet.';
    }

    const valid = (lat, lng) => Number.isFinite(lat) && Number.isFinite(lng) && Math.abs(lat) <= 90 && Math.abs(lng) <= 180;
    const el = (tag, cls, text) => { const n = document.createElement(tag); if (cls) n.className = cls; if (text != null) n.textContent = text; return n; };
    const link = (href, text) => { const a = el('a', null, text); a.href = href; return a; };

    let map = null;
    const markers = { needs: new Map(), resources: new Map() };
    const layers = {};
    if (typeof L !== 'undefined') {
        map = L.map('workMap', { zoomControl: false }).setView([58.1467, 7.9956], 12);
        L.control.zoom({ position: 'topright' }).addTo(map);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '© OpenStreetMap-bidragsytere', maxZoom: 19 })
            .addTo(map).on('tileerror', () => { status.textContent = 'Kartbakgrunnen er utilgjengelig. Listene fungerer fortsatt.'; });
        layers.needs = L.layerGroup().addTo(map);
        layers.resources = L.layerGroup().addTo(map);
    }

    const needPopup = n => {
        const box = el('div', 'map-popup');
        box.append(el('p', 'eyebrow', `BEHOV #${n.id} · ${n.priority.toUpperCase()}`), el('strong', null, n.type), el('div', 'muted', `${n.region} · ${n.neededAt}`));
        const actions = el('div', 'map-popup-actions');
        actions.append(link(n.matches, 'Finn ressurser →'), link(n.url, 'Detaljer'));
        box.append(actions);
        return box;
    };
    const resourcePopup = r => {
        const box = el('div', 'map-popup');
        box.append(el('p', 'eyebrow', `RESSURS #${r.id}`), el('strong', null, r.name), el('div', 'muted', `${r.type} · ${r.availability}`));
        const actions = el('div', 'map-popup-actions');
        actions.append(link(r.url, 'Se detaljer →'));
        box.append(actions);
        return box;
    };

    const points = [];
    // På stor skjerm ligger sidepanelet over venstre del av kartet.
    const sidePad = () => matchMedia('(min-width: 992px)').matches ? root.querySelector('.map-side').offsetWidth + 32 : 0;
    if (map) {
        for (const n of data.needs) {
            if (!valid(n.lat, n.lng)) continue;
            const urgent = n.priorityClass === 'urgent';
            const group = L.featureGroup();
            if (n.area) L.rectangle([[n.area[0], n.area[1]], [n.area[2], n.area[3]]], { color: '#c98a12', weight: 2, fillOpacity: .08, dashArray: '6 6' }).addTo(group);
            L.circleMarker([n.lat, n.lng], { radius: urgent ? 11 : 9, color: '#1d2b24', weight: 2, fillColor: urgent ? '#c7432f' : '#efc24b', fillOpacity: 1 }).addTo(group);
            group.bindPopup(needPopup(n)).addTo(layers.needs);
            markers.needs.set(n.id, group);
            points.push([n.lat, n.lng]);
        }
        for (const r of data.resources) {
            if (!valid(r.lat, r.lng)) continue;
            const m = L.circleMarker([r.lat, r.lng], { radius: 8, color: '#fff', weight: 2.5, fillColor: '#24503f', fillOpacity: 1 }).bindPopup(resourcePopup(r));
            m.addTo(layers.resources);
            markers.resources.set(r.id, m);
            points.push([r.lat, r.lng]);
        }
        if (points.length) map.fitBounds(points, { paddingTopLeft: [sidePad() + 30, 40], paddingBottomRight: [40, 40], maxZoom: 13 });
    }

    const focus = (kind, id) => {
        const m = markers[kind].get(id);
        if (!m || !map) return;
        const target = m.getBounds ? m.getBounds().getCenter() : m.getLatLng();
        if (!sidePad()) root.querySelector('.map-app-canvas').scrollIntoView({ block: 'start' });
        map.once('moveend', () => m.openPopup(target));
        map.flyToBounds(L.latLngBounds(target, target), { paddingTopLeft: [sidePad(), 0], maxZoom: Math.max(map.getZoom(), 14), duration: matchMedia('(prefers-reduced-motion: reduce)').matches ? 0 : .6 });
    };

    const renderList = (kind, query) => {
        const list = root.querySelector(`[data-list="${kind}"]`);
        if (!list) return;
        const q = query.trim().toLowerCase();
        const items = data[kind].filter(x => !q || [x.type, x.name, x.region].some(v => v && v.toLowerCase().includes(q)));
        list.replaceChildren();
        for (const x of items) {
            const li = el('li');
            const button = el('button', 'map-item');
            button.type = 'button';
            if (kind === 'needs') {
                button.append(el('span', `priority-dot ${x.priorityClass}`), el('span', 'map-item-main'));
                button.lastChild.append(el('strong', null, x.type), el('small', null, `${x.region} · ${x.neededAt}`));
                button.append(el('span', `map-item-tag ${x.priorityClass}`, x.priority));
            } else {
                button.append(el('span', 'priority-dot resource'), el('span', 'map-item-main'));
                button.lastChild.append(el('strong', null, x.name), el('small', null, `${x.type} · ${x.region}`));
                button.append(el('span', 'map-item-tag', x.availability));
            }
            if (!markers[kind].has(x.id)) { button.title = 'Mangler kartpunkt'; }
            button.addEventListener('click', () => markers[kind].has(x.id) ? focus(kind, x.id) : (location.href = x.url));
            li.append(button);
            list.append(li);
        }
        if (!items.length) list.append(el('li', 'map-empty', q ? 'Ingen treff på søket.' : kind === 'needs' ? 'Ingen udekkede behov.' : 'Ingen ressurser å vise.'));
        // Kartet viser bare treffene i listen.
        if (map) for (const [id, m] of markers[kind]) {
            const show = items.some(x => x.id === id);
            if (show && !layers[kind].hasLayer(m)) layers[kind].addLayer(m);
            if (!show && layers[kind].hasLayer(m)) layers[kind].removeLayer(m);
        }
    };
    const search = root.querySelector('.map-search');
    const renderAll = () => { renderList('needs', search.value); renderList('resources', search.value); };
    search.addEventListener('input', renderAll);
    renderAll();

    // Faner
    const tabs = [...root.querySelectorAll('[data-tab]')];
    tabs.forEach(tab => tab.addEventListener('click', () => {
        tabs.forEach(t => { const on = t === tab; t.setAttribute('aria-selected', on); document.getElementById(t.getAttribute('aria-controls')).hidden = !on; });
    }));

    // Registrering: punkt eller område i kartet, deretter videre til skjemaet med valget utfylt.
    const panel = root.querySelector('[data-place-panel]');
    const body = root.querySelector('[data-side-body]');
    const coords = panel.querySelector('[data-place-coords]');
    const next = panel.querySelector('[data-place-continue]');
    const title = panel.querySelector('[data-place-title]');
    const help = panel.querySelector('[data-place-help]');
    const modes = panel.querySelector('[data-place-modes]');
    let placing = null, shape = 'point', draft = null, corner = null, firstCorner = null, drag = null;
    const fmt = v => v.toFixed(5);
    const boundsOf = (a, b) => [Math.min(a.lat, b.lat), Math.min(a.lng, b.lng), Math.max(a.lat, b.lat), Math.max(a.lng, b.lng)];
    const validBounds = v => v.every(Number.isFinite) && v[0] >= -90 && v[2] <= 90 && v[1] >= -180 && v[3] <= 180 && v[0] < v[2] && v[1] < v[3];
    const clearDraft = () => { if (draft) { draft.remove(); draft = null; } if (corner) { corner.remove(); corner = null; } firstCorner = null; };
    const target = () => placing === 'need' ? data.registerNeed : data.registerResource;
    const texts = () => {
        if (placing === 'resource') return ['Klikk i kartet der ressursen står', 'Du kan flytte punktet etterpå. Resten fyller du ut på neste side.'];
        if (shape === 'drag') return ['Dra for å markere område', 'Trykk og hold i kartet, dra ut et rektangel og slipp. Kartflytting er pauset mens du tegner.'];
        if (shape === 'corners') return ['Velg to hjørner', 'Trykk på to motsatte hjørner for å markere et rektangel.'];
        return ['Klikk i kartet der hjelpen trengs', 'Du kan flytte punktet etterpå. Resten av opplysningene fyller du ut på neste side.'];
    };
    const clearButton = panel.querySelector('[data-place-clear]');
    const resetTexts = () => { [title.textContent, help.textContent] = texts(); coords.textContent = 'Ingen områdeavgrensning er valgt.'; coords.hidden = placing !== 'need'; next.hidden = true; clearButton.disabled = true; };
    const setPoint = latlng => {
        coords.textContent = `Valgt punkt: ${fmt(latlng.lat)}, ${fmt(latlng.lng)}`;
        coords.hidden = false;
        next.href = `${target()}?latitude=${fmt(latlng.lat)}&longitude=${fmt(latlng.lng)}`;
        next.hidden = false; clearButton.disabled = false; title.textContent = 'Punktet er valgt';
        status.textContent = coords.textContent;
    };
    const setArea = v => {
        clearDraft();
        draft = L.rectangle([[v[0], v[1]], [v[2], v[3]]], { color: '#c98a12', weight: 3, fillOpacity: .15, dashArray: '6 5', interactive: false }).addTo(map);
        const km = (a, b, c, d) => (PortalDistance(a, b, c, d)).toFixed(1);
        coords.textContent = `Valgt område: ca. ${km(v[0], v[1], v[0], v[3])} × ${km(v[0], v[1], v[2], v[1])} km`;
        coords.hidden = false;
        next.href = `${target()}?areaSouth=${fmt(v[0])}&areaWest=${fmt(v[1])}&areaNorth=${fmt(v[2])}&areaEast=${fmt(v[3])}`;
        next.hidden = false; clearButton.disabled = false; title.textContent = 'Området er valgt'; help.textContent = 'Marker et nytt område hvis du vil endre det, eller fjern kartvalget.';
        status.textContent = coords.textContent;
    };
    const PortalDistance = (lat1, lng1, lat2, lng2) => {
        const r = Math.PI / 180, a = Math.sin((lat2 - lat1) * r / 2) ** 2 + Math.cos(lat1 * r) * Math.cos(lat2 * r) * Math.sin((lng2 - lng1) * r / 2) ** 2;
        return 6371 * 2 * Math.asin(Math.sqrt(Math.min(1, a)));
    };
    const setShape = value => {
        shape = value; clearDraft(); resetTexts();
        modes.querySelectorAll('[data-mode]').forEach(b => b.setAttribute('aria-pressed', b.dataset.mode === value));
        if (map) { if (value === 'drag') map.dragging.disable(); else map.dragging.enable(); }
        root.classList.toggle('is-drawing', value === 'drag');
    };
    const stopPlacing = () => {
        placing = null; drag = null; panel.hidden = true; body.hidden = false; root.classList.remove('is-placing', 'is-drawing');
        clearDraft(); if (map) map.dragging.enable();
    };
    modes.querySelectorAll('[data-mode]').forEach(b => b.addEventListener('click', () => setShape(b.dataset.mode)));
    clearButton.addEventListener('click', () => setShape(shape));
    root.querySelectorAll('[data-place]').forEach(button => button.addEventListener('click', () => {
        const kind = button.dataset.place;
        if (!map) { location.href = kind === 'need' ? data.registerNeed : data.registerResource; return; }
        stopPlacing();
        placing = kind;
        panel.querySelector('[data-place-kicker]').textContent = kind === 'need' ? 'NYTT BEHOV' : 'NY RESSURS';
        modes.hidden = kind !== 'need';
        modes.querySelectorAll('[data-mode]:not([data-mode=point])').forEach(b => b.hidden = kind !== 'need');
        panel.hidden = false; body.hidden = true; root.classList.add('is-placing');
        setShape('point');
        panel.querySelector('[data-place-cancel]').focus();
    }));
    panel.querySelector('[data-place-cancel]').addEventListener('click', stopPlacing);
    document.addEventListener('keydown', e => { if (e.key === 'Escape' && placing) stopPlacing(); });

    if (map) {
        map.on('click', e => {
            if (!placing) return;
            if (shape === 'point') {
                clearDraft();
                draft = L.marker(e.latlng, { draggable: true, title: 'Nytt punkt' }).addTo(map);
                draft.on('dragend', () => setPoint(draft.getLatLng()));
                setPoint(e.latlng);
                return;
            }
            if (shape !== 'corners') return;
            if (!firstCorner) {
                clearDraft();
                firstCorner = e.latlng;
                corner = L.circleMarker(e.latlng, { radius: 6, color: '#c98a12', weight: 3 }).addTo(map);
                help.textContent = 'Første hjørne er valgt. Klikk det motsatte hjørnet.';
                return;
            }
            const v = boundsOf(firstCorner, e.latlng);
            if (validBounds(v)) setArea(v); else help.textContent = 'Velg et annet hjørne slik at området får både bredde og høyde.';
        });
        const el = map.getContainer();
        el.addEventListener('pointerdown', e => {
            if (placing !== 'need' || shape !== 'drag' || drag || e.button !== 0 || e.isPrimary === false || e.target.closest('.leaflet-control')) return;
            drag = { id: e.pointerId, start: map.mouseEventToLatLng(e), x: e.clientX, y: e.clientY };
            try { el.setPointerCapture(e.pointerId); } catch { drag = null; }
        });
        el.addEventListener('pointermove', e => {
            if (!drag || e.pointerId !== drag.id || Math.hypot(e.clientX - drag.x, e.clientY - drag.y) < 6) return;
            e.preventDefault();
            const v = boundsOf(drag.start, map.mouseEventToLatLng(e));
            if (!validBounds(v)) return;
            if (corner) { corner.remove(); corner = null; firstCorner = null; }
            if (draft) draft.setBounds([[v[0], v[1]], [v[2], v[3]]]);
            else draft = L.rectangle([[v[0], v[1]], [v[2], v[3]]], { color: '#c98a12', weight: 3, fillOpacity: .15, dashArray: '6 5', interactive: false }).addTo(map);
        });
        const endDrag = e => {
            if (!drag || e.pointerId !== drag.id) return;
            const moved = Math.hypot(e.clientX - drag.x, e.clientY - drag.y) >= 6;
            const v = boundsOf(drag.start, map.mouseEventToLatLng(e));
            drag = null;
            if (moved && e.type === 'pointerup' && validBounds(v)) {
                setArea(v);
                // Hindrer at slipp-klikket blir tolket som første hjørne.
                const swallow = ev => { ev.stopPropagation(); };
                el.addEventListener('click', swallow, { capture: true, once: true });
                setTimeout(() => el.removeEventListener('click', swallow, { capture: true }), 0);
            }
        };
        el.addEventListener('pointerup', endDrag);
        el.addEventListener('pointercancel', endDrag);
    }
})();

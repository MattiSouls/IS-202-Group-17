(() => {
    document.querySelectorAll('[data-area-preview]').forEach(root => {
        let map;
        root.addEventListener('toggle', () => {
            if (!root.open) return;
            if (map) { map.invalidateSize(); return; }
            const status = root.querySelector('[data-preview-status]');
            if (typeof L === 'undefined') { status.textContent += ' Kartvisningen er utilgjengelig; koordinatgrensene gjelder fortsatt.'; return; }
            const bounds = [[Number(root.dataset.south), Number(root.dataset.west)], [Number(root.dataset.north), Number(root.dataset.east)]];
            map = L.map(root.querySelector('[data-preview-map]'));
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {attribution:'© OpenStreetMap-bidragsytere',noWrap:true}).addTo(map)
                .on('tileerror',()=>{status.textContent='Kartbakgrunnen er utilgjengelig. Den markerte områdegrensen gjelder fortsatt.';});
            L.rectangle(bounds, {color:'#156b5b',weight:3,fillOpacity:.15}).addTo(map);
            map.fitBounds(bounds, {padding:[24,24],maxZoom:15});
        });
    });
    document.querySelectorAll('[data-area-picker]').forEach(root => {
        const form = root.closest('form');
        const field = name => root.querySelector(`[name="${name}"]`);
        const boundsFields = ['AreaSouth', 'AreaWest', 'AreaNorth', 'AreaEast'].map(field);
        const status = root.querySelector('[data-area-status]');
        const drawButton = root.querySelector('[data-draw-area]');
        const cornersButton = root.querySelector('[data-pick-corners]');
        const pointButton = root.querySelector('[data-pick-point]');
        const cancelButton = root.querySelector('[data-cancel-area]');
        const pointFields = root.querySelector('[data-point-fields]');
        drawButton.disabled = false;
        cornersButton.disabled = false;
        if(pointButton) pointButton.disabled = false;
        root.querySelector('[data-clear-area]').disabled = false;
        let map, rectangle, marker, corner, firstCorner, mode = null, drag = null, draft = null, navigation = null;
        const read = input => !input || input.value.trim() === '' ? null : Number(input.value);
        const readBounds = () => boundsFields.map(read);
        const validBounds = values => values.every(v => v !== null && Number.isFinite(v))
            && values[0] >= -90 && values[2] <= 90 && values[1] >= -180 && values[3] <= 180
            && values[0] < values[2] && values[1] < values[3];
        function setMode(next) {
            mode = next; firstCorner = null;
            const previousDrag = drag; drag = null;
            if(map && previousDrag && map.getContainer().hasPointerCapture?.(previousDrag.id)) map.getContainer().releasePointerCapture(previousDrag.id);
            if(draft && map) { map.removeLayer(draft); draft = null; }
            if(pointFields) pointFields.hidden = mode !== 'point' && readBounds().some(v=>v!==null) && read(field('Latitude')) === null && read(field('Longitude')) === null;
            if (corner && map) { map.removeLayer(corner); corner = null; }
            drawButton.setAttribute('aria-pressed', String(mode === 'drag'));
            cornersButton.setAttribute('aria-pressed', String(mode === 'area'));
            pointButton?.setAttribute('aria-pressed', String(mode === 'point'));
            cancelButton.hidden = mode === null;
            if (map) {
                map.getContainer().classList.toggle('picking-location', mode !== null);
                map.getContainer().classList.toggle('drawing-area', mode === 'drag');
                if(mode === 'drag' && !navigation) {
                    navigation = [map.dragging,map.doubleClickZoom,map.boxZoom,map.touchZoom,map.scrollWheelZoom].map(handler=>({handler,enabled:handler.enabled()}));
                    navigation.forEach(({handler})=>handler.disable());
                } else if(mode !== 'drag' && navigation) {
                    navigation.forEach(({handler,enabled})=>{if(enabled)handler.enable();}); navigation=null;
                }
            }
        }
        const rectangleBounds = (first, last) => [Math.min(first.lat,last.lat),Math.min(first.lng,last.lng),Math.max(first.lat,last.lat),Math.max(first.lng,last.lng)];
        function commitBounds(values) {
            const rounded=values.map(v=>Number(v.toFixed(6)));
            if(!validBounds(rounded)) return false;
            boundsFields.forEach((input,i)=>input.value=rounded[i].toFixed(6));
            clearPoint();setMode(null);changed();return true;
        }
        function render(fit = false) {
            if (!map) return;
            if (rectangle) { map.removeLayer(rectangle); rectangle = null; }
            if (marker) { map.removeLayer(marker); marker = null; }
            const values = readBounds();
            if (validBounds(values)) {
                rectangle = L.rectangle([[values[0], values[1]], [values[2], values[3]]], {color:'#156b5b',weight:3,fillOpacity:.15}).addTo(map);
                if (fit) map.fitBounds(rectangle.getBounds(), {padding:[24,24],maxZoom:15});
            } else {
                const lat = read(field('Latitude')), lng = read(field('Longitude'));
                if (lat !== null && lng !== null && Number.isFinite(lat) && Number.isFinite(lng) && Math.abs(lat)<=90 && Math.abs(lng)<=180) {
                    marker = L.marker([lat,lng]).addTo(map);
                    if (fit) map.setView([lat,lng],13);
                }
            }
        }
        function clearPoint() { if (field('Latitude')) field('Latitude').value = ''; if (field('Longitude')) field('Longitude').value = ''; }
        function changed() {
            const values = readBounds();
            const hasArea = values.some(v => v !== null);
            if (pointFields) pointFields.hidden = hasArea;
            if (hasArea) clearPoint();
            status.textContent = validBounds(values) ? 'Område valgt. Avgrensningen brukes når du sender skjemaet.'
                : hasArea ? 'Fyll ut fire gyldige grenser. Sør må være mindre enn nord, og vest mindre enn øst.' : 'Ingen områdeavgrensning er valgt.';
            render(true);
        }
        function initialize() {
            if (map) { map.invalidateSize(); render(true); return; }
            if (typeof L === 'undefined') {
                status.textContent = 'Kartet kunne ikke lastes. Skriv kjente koordinater under; skjema og søk fungerer fortsatt.';
                drawButton.disabled = true; cornersButton.disabled = true; if(pointButton) pointButton.disabled = true; return;
            }
            map = L.map(root.querySelector('[data-area-map]'), {worldCopyJump:false}).setView([58.1467,7.9956],11);
            const mapElement = map.getContainer();
            if(typeof mapElement.setPointerCapture !== 'function') drawButton.disabled = true;
            mapElement.addEventListener('pointerdown', event => {
                if(mode !== 'drag' || drag || event.isPrimary === false || event.button !== 0 || event.target.closest?.('.leaflet-control')) return;
                const start=map.mouseEventToLatLng(event);
                if(!Number.isFinite(start.lat)||!Number.isFinite(start.lng)||Math.abs(start.lat)>90||Math.abs(start.lng)>180)return;
                event.preventDefault();event.stopPropagation();
                drag={id:event.pointerId,start,x:event.clientX,y:event.clientY};
                try { mapElement.setPointerCapture(event.pointerId); }
                catch { setMode(null);status.textContent='Dra-verktøyet er utilgjengelig. Velg to hjørner eller bruk koordinatfeltene.';return; }
                status.textContent='Dra til motsatt hjørne og slipp. Det forrige området beholdes til du slipper.';
            },{passive:false});
            mapElement.addEventListener('pointermove', event => {
                if(!drag || event.pointerId!==drag.id)return;
                event.preventDefault();const values=rectangleBounds(drag.start,map.mouseEventToLatLng(event));
                if(!validBounds(values))return;
                if(draft)map.removeLayer(draft);
                draft=L.rectangle([[values[0],values[1]],[values[2],values[3]]],{color:'#156b5b',weight:3,fillOpacity:.2,dashArray:'6 5',interactive:false}).addTo(map);
            },{passive:false});
            mapElement.addEventListener('pointerup', event => {
                if(!drag || event.pointerId!==drag.id)return;
                event.preventDefault();const stroke=drag;
                const values=rectangleBounds(stroke.start,map.mouseEventToLatLng(event));
                if(Math.hypot(event.clientX-stroke.x,event.clientY-stroke.y)<6 || !validBounds(values) || !commitBounds(values)) {
                    setMode('drag');render();status.textContent='Området ble for lite eller har ugyldige grenser. Dra et nytt felt i kartet, eller velg to hjørner.';
                }
            },{passive:false});
            const cancelStroke = event => {
                if(!drag || event.pointerId!==drag.id)return;
                setMode(null);render();status.textContent='Tegningen ble avbrutt. Det forrige kartvalget er beholdt.';
            };
            mapElement.addEventListener('pointercancel',cancelStroke);
            mapElement.addEventListener('lostpointercapture',cancelStroke);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {attribution:'© OpenStreetMap-bidragsytere',noWrap:true}).addTo(map)
                .on('tileerror',()=> {status.textContent='Kartbakgrunnen er utilgjengelig. Bruk kjente koordinater i feltene under.';});
            map.on('click', event => {
                const {lat,lng} = event.latlng;
                if (!Number.isFinite(lat) || !Number.isFinite(lng) || Math.abs(lat)>90 || Math.abs(lng)>180 || mode === null || mode === 'drag') return;
                if (mode === 'point') {
                    boundsFields.forEach(input=>input.value='');
                    field('Latitude').value=lat.toFixed(6);field('Longitude').value=lng.toFixed(6);
                    if(pointFields) pointFields.hidden=false;
                    setMode(null);render();status.textContent='Punkt valgt. Matching sorterer etter luftlinje fra dette punktet.';return;
                }
                if (!firstCorner) {
                    firstCorner={lat,lng};corner=L.circleMarker([lat,lng],{radius:6,color:'#156b5b'}).addTo(map);
                    status.textContent='Første hjørne er valgt. Trykk på det motsatte hjørnet, eller avbryt markeringen.';return;
                }
                const values=rectangleBounds(firstCorner,{lat,lng});
                if(!validBounds(values) || !commitBounds(values)) {status.textContent='Velg et annet hjørne slik at området får både bredde og høyde.';return;}
            });
            render(true);
        }
        drawButton.addEventListener('click',()=> {initialize();if(!map||drawButton.disabled)return;setMode('drag');status.textContent='Trykk og hold i kartet, dra ut et rektangel og slipp for å velge området. Kartflytting er pauset mens du tegner.';});
        cornersButton.addEventListener('click',()=> {initialize();if(!map)return;setMode('area');status.textContent='Trykk på to motsatte hjørner for å markere et rektangel. Det gamle kartvalget beholdes til du er ferdig.';});
        pointButton?.addEventListener('click',()=> {initialize();if(!map)return;setMode('point');status.textContent='Trykk på kartet for å velge et punkt. Det gamle valget beholdes til du velger et nytt.';});
        cancelButton.addEventListener('click',()=>{setMode(null);render();status.textContent='Markeringen er avbrutt. Det forrige kartvalget er beholdt.';});
        root.querySelector('[data-clear-area]').addEventListener('click',()=>{boundsFields.forEach(input=>input.value='');clearPoint();setMode(null);changed();status.textContent='Kartvalget er fjernet. Regionfilteret gjelder igjen.';});
        boundsFields.forEach(input=>input.addEventListener('change',()=>{setMode(null);changed();}));
        [field('Latitude'),field('Longitude')].filter(Boolean).forEach(input=>input.addEventListener('change',()=>{
            boundsFields.forEach(bound=>bound.value='');setMode(null);render(true);
            status.textContent='Punktkoordinater er valgt. Områdegrensene er fjernet. Oppgi både breddegrad og lengdegrad.';
        }));
        root.addEventListener('toggle',()=>{if(root.open)initialize();else{setMode(null);render();}});
        root.addEventListener('keydown',event=>{if(event.key==='Escape' && mode!==null){setMode(null);render();status.textContent='Markeringen er avbrutt. Det forrige kartvalget er beholdt.';}});
        form?.addEventListener('submit',event=>{
            if(mode!==null){event.preventDefault();status.textContent='Fullfør kartvalget eller velg «Avbryt markering» før du fortsetter.';cancelButton.focus();}
        },true);
        setMode(null);
        if(root.open)initialize();
    });
})();

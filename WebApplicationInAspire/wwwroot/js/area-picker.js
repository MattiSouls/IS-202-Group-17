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
        const pointButton = root.querySelector('[data-pick-point]');
        const cancelButton = root.querySelector('[data-cancel-area]');
        const pointFields = root.querySelector('[data-point-fields]');
        drawButton.disabled = false;
        if(pointButton) pointButton.disabled = false;
        root.querySelector('[data-clear-area]').disabled = false;
        let map, rectangle, marker, corner, firstCorner, mode = null;
        const read = input => !input || input.value.trim() === '' ? null : Number(input.value);
        const readBounds = () => boundsFields.map(read);
        const validBounds = values => values.every(v => v !== null && Number.isFinite(v))
            && values[0] >= -90 && values[2] <= 90 && values[1] >= -180 && values[3] <= 180
            && values[0] < values[2] && values[1] < values[3];
        function setMode(next) {
            mode = next; firstCorner = null;
            if(pointFields) pointFields.hidden = mode !== 'point' && readBounds().some(v=>v!==null) && read(field('Latitude')) === null && read(field('Longitude')) === null;
            if (corner && map) { map.removeLayer(corner); corner = null; }
            drawButton.setAttribute('aria-pressed', String(mode === 'area'));
            pointButton?.setAttribute('aria-pressed', String(mode === 'point'));
            cancelButton.hidden = mode === null;
            if (map) map.getContainer().classList.toggle('picking-location', mode !== null);
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
                drawButton.disabled = true; if(pointButton) pointButton.disabled = true; return;
            }
            map = L.map(root.querySelector('[data-area-map]'), {worldCopyJump:false}).setView([58.1467,7.9956],11);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {attribution:'© OpenStreetMap-bidragsytere',noWrap:true}).addTo(map)
                .on('tileerror',()=> {status.textContent='Kartbakgrunnen er utilgjengelig. Bruk kjente koordinater i feltene under.';});
            map.on('click', event => {
                const {lat,lng} = event.latlng;
                if (!Number.isFinite(lat) || !Number.isFinite(lng) || Math.abs(lat)>90 || Math.abs(lng)>180 || mode === null) return;
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
                const values=[Math.min(lat,firstCorner.lat),Math.min(lng,firstCorner.lng),Math.max(lat,firstCorner.lat),Math.max(lng,firstCorner.lng)];
                if(!validBounds(values)) {status.textContent='Velg et annet hjørne slik at området får både bredde og høyde.';return;}
                boundsFields.forEach((input,i)=>input.value=values[i].toFixed(6));
                clearPoint();setMode(null);changed();
            });
            render(true);
        }
        drawButton.addEventListener('click',()=> {initialize();if(!map)return;setMode('area');status.textContent='Trykk på to motsatte hjørner for å markere et rektangel. Det gamle kartvalget beholdes til du er ferdig.';});
        pointButton?.addEventListener('click',()=> {initialize();if(!map)return;setMode('point');status.textContent='Trykk på kartet for å velge et punkt. Det gamle valget beholdes til du velger et nytt.';});
        cancelButton.addEventListener('click',()=>{setMode(null);render();status.textContent='Markeringen er avbrutt. Det forrige kartvalget er beholdt.';});
        root.querySelector('[data-clear-area]').addEventListener('click',()=>{boundsFields.forEach(input=>input.value='');clearPoint();setMode(null);changed();status.textContent='Kartvalget er fjernet. Regionfilteret gjelder igjen.';});
        boundsFields.forEach(input=>input.addEventListener('change',()=>{setMode(null);changed();}));
        [field('Latitude'),field('Longitude')].filter(Boolean).forEach(input=>input.addEventListener('change',()=>{
            boundsFields.forEach(bound=>bound.value='');setMode(null);render(true);
            status.textContent='Punktkoordinater er valgt. Områdegrensene er fjernet. Oppgi både breddegrad og lengdegrad.';
        }));
        root.addEventListener('toggle',()=>{if(root.open)initialize();});
        root.addEventListener('keydown',event=>{if(event.key==='Escape' && mode!==null){setMode(null);render();status.textContent='Markeringen er avbrutt. Det forrige kartvalget er beholdt.';}});
        form?.addEventListener('submit',event=>{
            if(mode!==null){event.preventDefault();status.textContent='Fullfør kartvalget eller velg «Avbryt markering» før du fortsetter.';cancelButton.focus();}
        },true);
        setMode(null);
        if(root.open)initialize();
    });
})();

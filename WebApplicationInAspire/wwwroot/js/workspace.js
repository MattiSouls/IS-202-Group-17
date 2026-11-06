(() => {
    const errors = document.querySelector('[data-focus-errors]');
    if (errors) errors.focus();
    document.querySelectorAll('[data-valmsg-for]').forEach(message => {
        const field = document.getElementById(message.dataset.valmsgFor.replaceAll('.', '_'));
        if (!field) return;
        message.id ||= field.id + '-error';
        field.setAttribute('aria-describedby', [field.getAttribute('aria-describedby'), message.id].filter(Boolean).join(' '));
        if (message.textContent.trim()) field.setAttribute('aria-invalid', 'true');
        if (field.dataset.valRequired) field.setAttribute('aria-required', 'true');
    });
    if (window.jQuery) $('form').on('invalid-form.validate', function (_, validator) {
        validator.settings.focusInvalid = false;
        const summary = document.getElementById('client-errors');
        if (!summary) return;
        summary.replaceChildren();
        const heading = document.createElement('h2'); heading.className = 'h5'; heading.textContent = 'Kontroller opplysningene'; summary.append(heading);
        const list = document.createElement('ul');
        for (const error of validator.errorList) {
            const item = document.createElement('li'), link = document.createElement('a');
            link.href = '#' + error.element.id; link.textContent = error.message; item.append(link); list.append(item);
            error.element.setAttribute('aria-invalid', 'true');
        }
        summary.append(list); summary.hidden = false; summary.focus();
    });
    const originals = new Map();
    document.querySelectorAll('form[method="post"]').forEach(form => form.addEventListener('submit', event => {
        if (event.defaultPrevented) return;
        if (window.jQuery && $(form).data('validator') && !$(form).valid()) return;
        if (form.dataset.submitting) { event.preventDefault(); return; }
        form.dataset.submitting = 'true'; form.setAttribute('aria-busy', 'true');
        for (const button of form.querySelectorAll('button[type="submit"]')) {
            originals.set(button, button.textContent); button.disabled = true; button.textContent = 'Lagrer …';
        }
    }));
    window.addEventListener('pageshow', () => {
        for (const [button, text] of originals) { button.disabled = false; button.textContent = text; }
        document.querySelectorAll('form[data-submitting]').forEach(form => { delete form.dataset.submitting; form.removeAttribute('aria-busy'); });
    });
    document.querySelectorAll('[data-map-toggle]').forEach(toggle => toggle.addEventListener('toggle', () => window.dispatchEvent(new Event('resize'))));
    const notifications = document.querySelector('[data-notification-url]');
    if (notifications) {
        const count = notifications.querySelector('[data-count]');
        async function updateCount() {
            if (document.hidden) return;
            try {
                const response = await fetch(notifications.dataset.notificationUrl, {credentials:'same-origin', headers:{Accept:'application/json'}});
                if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) return;
                const data = await response.json();
                if (!Number.isSafeInteger(data.count) || data.count < 0) return;
                if (data.count > Number(count.dataset.count)) document.getElementById('notification-announcement').textContent = 'Du har ' + data.count + ' uleste varsler.';
                count.dataset.count = data.count; count.textContent = data.count > 99 ? '99+' : data.count; count.hidden = data.count === 0;
                notifications.setAttribute('aria-label', 'Varsler, ' + data.count + ' uleste');
            } catch { /* Nettverksfeil skal ikke blokkere brukerens arbeid. */ }
        }
        setInterval(updateCount, 20000);
    }
})();

(() => {
    let opener, request;
    const dialog = () => document.querySelector('#admin-form-dialog');
    const parse = html => new DOMParser().parseFromString(html, 'text/html');
    const slugify = value => value.normalize('NFKD').replace(/[\u0300-\u036f]/g, '').toLowerCase()
        .replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 160).replace(/-+$/g, '');
    function prepare(root, manual) {
        const form = root.querySelector('form[data-category-mode]');
        if (!form) return;
        const name = form.elements.Name, slug = form.elements.Slug;
        const edit = form.querySelector('[data-edit-slug]');
        if (!edit || edit.dataset.ready) return;
        edit.dataset.ready = 'true'; edit.hidden = false;
        form.dataset.slugManual = manual ?? (slug.value ? 'true' : 'false');
        slug.readOnly = true;
        edit.addEventListener('click', () => {
            slug.readOnly = false; form.dataset.slugManual = 'true'; edit.hidden = true; slug.focus(); slug.select();
        });
        name.addEventListener('input', () => {
            if (form.dataset.categoryMode === 'create' && form.dataset.slugManual !== 'true') slug.value = slugify(name.value);
        });
    }
    prepare(document);
    document.addEventListener('error', event => {
        if (event.target.matches?.('img[data-product-image]')) {
            event.target.hidden = true;
            event.target.parentElement.querySelector('[data-image-fallback]').hidden = false;
        }
    }, true);
    function preparePagination() {
        document.querySelectorAll('[data-page-size-apply]').forEach(button => { button.hidden = true; });
    }
    preparePagination();
    document.addEventListener('change', event => {
        if (event.target.matches('[data-page-size]')) event.target.form.requestSubmit();
    });
    const menuTrigger = menu => document.getElementById(menu.id + '-trigger');
    function positionMenu(menu) {
        const rect = menuTrigger(menu).getBoundingClientRect();
        menu.style.left = Math.max(8, Math.min(rect.right - 192, innerWidth - 200)) + 'px';
        menu.style.top = (rect.bottom + 112 > innerHeight ? Math.max(8, rect.top - 112) : rect.bottom + 4) + 'px';
    }
    document.addEventListener('beforetoggle', event => {
        const menu = event.target;
        if (!menu.matches?.('[data-category-menu]') || event.newState !== 'open') return;
        positionMenu(menu);
    }, true);
    document.addEventListener('toggle', event => {
        const menu = event.target;
        if (!menu.matches?.('[data-category-menu]')) return;
        menuTrigger(menu)?.setAttribute('aria-expanded', String(event.newState === 'open'));
        if (event.newState === 'open') menu.querySelector('[role="menuitem"]')?.focus({ preventScroll: true });
    }, true);
    document.addEventListener('keydown', event => {
        const trigger = event.target.closest('[data-category-menu-trigger]');
        if (trigger && ['ArrowDown', 'ArrowUp'].includes(event.key)) {
            event.preventDefault();
            const menu = document.getElementById(trigger.getAttribute('popovertarget'));
            menu.showPopover();
            requestAnimationFrame(() => (event.key === 'ArrowUp' ? menu.lastElementChild : menu.firstElementChild).focus({ preventScroll: true }));
            return;
        }
        const menu = event.target.closest('[data-category-menu]');
        if (!menu) return;
        const items = [...menu.querySelectorAll('[role="menuitem"]')];
        const index = items.indexOf(document.activeElement);
        if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) {
            event.preventDefault();
            const next = event.key === 'Home' ? 0 : event.key === 'End' ? items.length - 1 : (index + (event.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length;
            items[next].focus({ preventScroll: true });
        } else if (event.key === 'Escape') {
            event.preventDefault(); menu.hidePopover(); menuTrigger(menu)?.focus();
        } else if (event.key === 'Tab') {
            // Return to the trigger before native Tab moves to the next/previous control.
            menu.hidePopover(); menuTrigger(menu)?.focus();
        }
    });
    const closeMenus = () => document.querySelectorAll('[data-category-menu]:popover-open').forEach(menu => menu.hidePopover());
    window.addEventListener('resize', closeMenus);
    document.addEventListener('scroll', () => document.querySelectorAll('[data-category-menu]:popover-open').forEach(positionMenu), true);
    function busy(form, state) {
        const button = form.querySelector('button[type="submit"]');
        if (!button) return;
        button.dataset.label ??= button.textContent;
        button.disabled = state; button.textContent = state ? button.dataset.busyLabel : button.dataset.label;
        form.setAttribute('aria-busy', String(state));
        const status = form.querySelector('[data-form-status]'); if (status) status.textContent = state ? button.dataset.busyLabel : '';
    }
    function error(message) {
        const target = dialog().querySelector('#admin-form-error'); target.textContent = message; target.hidden = false;
    }
    function showPage(page, manual) {
        const form = page.querySelector('#admin-main form[method="post"]');
        if (!form) throw new Error('The form could not be loaded. Your session may have expired.');
        const section = form.closest('section');
        // The MVC form and its existing confirmation copy are reused, never rebuilt in JavaScript.
        const fragment = document.importNode(section, true);
        fragment.className = '';
        fragment.querySelectorAll('a[asp-action], a').forEach(link => {
            if (link.textContent.trim() === 'Cancel') { link.dataset.closeAdminForm = ''; }
        });
        const content = dialog().querySelector('#admin-form-content'); content.replaceChildren(fragment);
        dialog().querySelector('#admin-form-title').textContent = page.querySelector('h1').textContent;
        dialog().querySelector('#admin-form-error').hidden = true;
        prepare(content, manual);
        const summary = content.querySelector('.validation-summary-errors');
        if (summary) { summary.tabIndex = -1; summary.focus(); }
        else content.querySelector('input:not([type="hidden"]),button')?.focus();
    }
    document.addEventListener('click', async event => {
        const close = event.target.closest('[data-close-admin-form]');
        if (close && dialog()?.open) { event.preventDefault(); dialog().close(); return; }
        const link = event.target.closest('a[data-category-dialog]');
        if (!link || !dialog()?.showModal || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0) return;
        event.preventDefault();
        const menu = link.closest('[data-category-menu]');
        opener = menu ? menuTrigger(menu) : link;
        if (menu?.matches(':popover-open')) menu.hidePopover();
        const modal = dialog(); modal.querySelector('#admin-form-error').hidden = true;
        modal.querySelector('#admin-form-title').textContent = 'Loading form';
        modal.querySelector('#admin-form-content').innerHTML = '<p role="status" class="text-sm text-text-muted">Loading form...</p>';
        modal.showModal(); request = new AbortController();
        try {
            const response = await fetch(link.href, { signal: request.signal });
            if (!response.ok) throw new Error('Unable to load this record. Reload the listing and try again.');
            const page = parse(await response.text());
            if (modal.open) showPage(page);
        } catch (failure) { if (failure.name !== 'AbortError') error(failure.message); }
    });
    document.addEventListener('close', event => {
        if (event.target.id !== 'admin-form-dialog') return;
        request?.abort(); opener?.focus();
    }, true);
    document.addEventListener('keydown', event => {
        const modal = dialog();
        if (event.key !== 'Tab' || !modal?.open) return;
        const targets = [...modal.querySelectorAll('a[href],button,input,select,textarea,[tabindex]')]
            .filter(element => !element.disabled && element.tabIndex >= 0 && element.getClientRects().length);
        const first = targets[0], last = targets.at(-1);
        if (!first) return;
        if (event.shiftKey && (document.activeElement === first || !modal.contains(document.activeElement))) {
            event.preventDefault(); last.focus();
        } else if (!event.shiftKey && (document.activeElement === last || !modal.contains(document.activeElement))) {
            event.preventDefault(); first.focus();
        }
    });
    document.addEventListener('submit', async event => {
        const form = event.target;
        if (!form.matches('form[data-submit-state]') || event.defaultPrevented || !form.checkValidity()) return;
        const modal = form.closest('#admin-form-dialog');
        if (!modal) { busy(form, true); return; }
        event.preventDefault();
        if (form.getAttribute('aria-busy') === 'true') return;
        const body = new FormData(form); busy(form, true);
        request = new AbortController();
        try {
            const response = await fetch(form.action, { method: 'POST', body, signal: request.signal });
            const page = parse(await response.text());
            if (!modal.open) return;
            if (response.ok && page.querySelector('[data-admin-list]')) {
                modal.close();
                document.querySelector('#admin-main').replaceChildren(...document.importNode(page.querySelector('#admin-main'), true).childNodes);
                preparePagination();
                document.querySelector('[data-category-success]')?.focus();
                history.replaceState(null, '', response.url);
            } else if ((response.ok || response.status === 409) && page.querySelector('#admin-main form[method="post"]')) {
                showPage(page, form.dataset.slugManual);
            } else {
                error('Unable to save. Your entries are preserved. Check your session or reload before retrying.'); busy(form, false);
            }
        } catch (failure) {
            if (failure.name !== 'AbortError' && modal.open) { error('The request could not be completed. Your entries are preserved. Reload to check whether it saved before retrying.'); busy(form, false); }
        }
    });
    window.addEventListener('pageshow', () => document.querySelectorAll('form[data-submit-state]').forEach(form => busy(form, false)));
})();

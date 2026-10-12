(() => {
    const menu = document.querySelector('.menu-toggle');
    const nav = document.getElementById('storefront-nav');
    const closeMenu = () => { if (!menu || !nav) return; menu.setAttribute('aria-expanded', 'false'); menu.setAttribute('aria-label', 'Open navigation'); nav.classList.remove('is-open'); };
    menu?.addEventListener('click', () => { const open = menu.getAttribute('aria-expanded') !== 'true'; menu.setAttribute('aria-expanded', String(open)); menu.setAttribute('aria-label', open ? 'Close navigation' : 'Open navigation'); nav.classList.toggle('is-open', open); });
    nav?.querySelectorAll('a').forEach(link => link.addEventListener('click', closeMenu));
    document.addEventListener('keydown', event => { if (event.key === 'Escape' && menu?.getAttribute('aria-expanded') === 'true') { closeMenu(); menu.focus(); } });
    window.matchMedia('(min-width: 768px)').addEventListener('change', closeMenu);

    const dialog = document.getElementById('prototype-dialog');
    document.querySelectorAll('[data-preview]').forEach(button => button.addEventListener('click', () => { document.getElementById('dialog-message').textContent = button.dataset.preview; dialog.showModal(); }));
    dialog?.querySelectorAll('[data-close-dialog]').forEach(button => button.addEventListener('click', () => dialog.close()));

    const products = [...document.querySelectorAll('[data-product]')];
    if (!products.length) return;
    const input = document.getElementById('search-input');
    const filters = [...document.querySelectorAll('[data-filter]')];
    let category = 'all';
    const filter = () => {
        const query = input.value.trim().toLocaleLowerCase();
        let count = 0;
        products.forEach(product => { const matches = (category === 'all' || product.dataset.category === category) && `${product.dataset.name} ${product.dataset.category}`.toLocaleLowerCase().includes(query); product.hidden = !matches; if (matches) count++; });
        filters.forEach(button => { const active = button.dataset.filter === category; button.classList.toggle('is-active', active); button.setAttribute('aria-pressed', String(active)); });
        document.getElementById('results-count').textContent = `${count} sample product${count === 1 ? '' : 's'}${category === 'all' ? '' : ` · ${category}`}`;
        document.getElementById('empty-results').hidden = count !== 0;
    };
    filters.forEach(button => button.addEventListener('click', () => { category = button.dataset.filter; filter(); document.getElementById('featured').scrollIntoView({block:'start'}); }));
    const reset = () => { category = 'all'; input.value = ''; filter(); };
    document.getElementById('reset-filters').addEventListener('click', reset);
    document.getElementById('clear-search').addEventListener('click', () => { reset(); input.focus(); });
    document.getElementById('sample-search').addEventListener('submit', event => { event.preventDefault(); category = 'all'; filter(); document.getElementById('featured').scrollIntoView({block:'start'}); });
    input.addEventListener('input', filter);
    input.value = new URLSearchParams(window.location.search).get('q')?.slice(0, 100) ?? '';
    filter();
})();

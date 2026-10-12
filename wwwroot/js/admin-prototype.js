(() => {
    const drawer = document.getElementById('admin-drawer');
    const menu = document.getElementById('admin-menu');
    const preview = document.getElementById('admin-preview-dialog');
    const message = document.getElementById('admin-preview-message');
    let previewTrigger;
    menu.addEventListener('click', () => { drawer.showModal(); menu.setAttribute('aria-expanded', 'true'); });
    drawer.addEventListener('close', () => { menu.setAttribute('aria-expanded', 'false'); menu.focus(); });
    document.querySelector('[data-close-drawer]').addEventListener('click', () => drawer.close());
    drawer.addEventListener('keydown', event => {
        if (event.key === 'Escape') { event.preventDefault(); drawer.close(); }
    });
    drawer.addEventListener('click', event => { if(event.target === drawer) { const r = drawer.getBoundingClientRect(); if(event.clientX < r.left || event.clientX > r.right || event.clientY < r.top || event.clientY > r.bottom) drawer.close(); } });
    window.matchMedia('(min-width: 1101px)').addEventListener('change', event => { if(event.matches && drawer.open) drawer.close(); });
    document.querySelectorAll('[data-admin-preview]').forEach(button => button.addEventListener('click', () => {
        const inDrawer = drawer.contains(button);
        if(drawer.open) drawer.close();
        previewTrigger = inDrawer ? menu : button;
        message.textContent = `${button.dataset.adminPreview} is a visual placeholder. No records are loaded or changed, and no export is generated. Identity protects access; management functionality remains a prototype.`;
        if (["Stock", "Suppliers", "Purchases"].includes(button.dataset.adminPreview)) message.textContent += " This requested module is outside the original proposal and pending commercial approval.";
        preview.showModal();
    }));
    document.querySelectorAll('[data-close-preview]').forEach(button => button.addEventListener('click', () => preview.close()));
    preview.addEventListener('close', () => previewTrigger?.focus());
})();

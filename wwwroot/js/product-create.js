(() => {
    const form = document.querySelector('#product-create-form');
    if (!form) return;
    const list = form.querySelector('[data-variant-list]');
    const name = form.elements.Name, slug = form.elements.Slug;
    let manual = Boolean(slug.value), urls = [];
    const edit = form.querySelector('[data-product-edit-slug]'); if (edit) edit.hidden = false; slug.readOnly = true;
    edit?.addEventListener('click', () => { manual = true; slug.readOnly = false; edit.hidden = true; slug.focus(); });
    name.addEventListener('input', () => {
        if (edit && !manual) slug.value = name.value.normalize('NFKD').replace(/[\u0300-\u036f]/g, '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0,180).replace(/-+$/g,'');
    });
    function renumber() {
        const attributes = ['id', 'for', 'aria-describedby', 'aria-labelledby', 'aria-controls'];
        function renameNodes(container, pattern, replacement) {
            [container, ...container.querySelectorAll('*')].forEach(node => attributes.forEach(attribute => {
                if (node.hasAttribute(attribute)) node.setAttribute(attribute, node.getAttribute(attribute).replace(pattern, replacement));
            }));
        }
        [...list.querySelectorAll('[data-variant]')].forEach((row, i) => {
            row.querySelector('[data-variant-number]').textContent = i + 1;
            renameNodes(row, /Variants_(?:__variant__|\d+)_/g, `Variants_${i}_`);
            row.querySelectorAll('[name]').forEach(field => {
                field.name = field.name.replace(/Variants\[[^\]]+\]/, `Variants[${i}]`);
            });
            [...row.querySelectorAll('[data-conversion]')].forEach((conversion,j) => {
                renameNodes(conversion, /Conversions_(?:__conversion__|\d+)_/g, `Conversions_${j}_`);
                conversion.querySelectorAll('[data-valmsg-for]').forEach(error => {
                    error.dataset.valmsgFor = error.dataset.valmsgFor.replace(/Conversions\[[^\]]+\]/, `Conversions[${j}]`);
                });
                conversion.querySelectorAll('[name]').forEach(field => {
                    field.name = field.name.replace(/Conversions\[[^\]]+\]/, `Conversions[${j}]`);
                });
            });
            row.querySelectorAll('[data-valmsg-for]').forEach(error => {
                error.dataset.valmsgFor = error.dataset.valmsgFor.replace(/Variants\[[^\]]+\]/,`Variants[${i}]`);
            });
            row.querySelector('[data-remove-variant]').hidden = !form.hasAttribute('data-edit-product') && list.children.length === 1;
            row.querySelector('[data-add-conversion]').hidden = false;
            row.querySelector('[data-add-conversion]').disabled = row.querySelectorAll('[data-conversion]').length >= 10;
            row.querySelectorAll('[data-remove-conversion]').forEach(button => { button.hidden = false; });
        });
        form.querySelector('[data-add-variant]').disabled = list.children.length + form.querySelectorAll('[data-existing-option]').length >= 25;
        refreshStock();
    }
    const rowKey = row => row.querySelector('[data-row-key]').value.toLowerCase();
    const isShared = row => row.querySelector('[data-stock-mode]:checked')?.value === 'Shared';
    const sourceKey = row => row.querySelector('[data-stock-owner]').value.toLowerCase();
    const rows = () => [...list.querySelectorAll('[data-variant]')];
    const title = row => row.querySelector('[data-option-name]').value.trim() || `Selling option ${rows().indexOf(row)+1}`;
    const dependents = row => rows().filter(other => other !== row && isShared(other) && sourceKey(other) === rowKey(row));
    function refreshStock() {
        const all = rows();
        all.forEach(row => {
            const shared = isShared(row), select = row.querySelector('[data-stock-owner]'), previous = select.value;
            const choices = [...all, ...form.querySelectorAll('[data-existing-root]')].filter(other => other !== row && !isShared(other) && other.querySelector('[data-sold-unit]').value);
            select.replaceChildren(new Option('Choose a separately counted option', ''));
            choices.forEach(owner => {
                const code = owner.querySelector('input[name$=".Sku"]').value.trim();
                select.add(new Option(title(owner) + (code ? ' — ' + code : ''), rowKey(owner)));
            });
            if (previous && !choices.some(owner => rowKey(owner) === previous.toLowerCase()))
                select.add(new Option('Previous source is invalid — choose another option', previous));
            select.value = previous;
            const quantity = row.querySelector('[data-stock-quantity]');
            row.querySelector('[data-shared-fields]').hidden = !shared;
            select.disabled = quantity.disabled = !shared;
            select.required = quantity.required = shared;
            const usedBy = dependents(row);
            row.querySelector('[data-remove-variant]').disabled = usedBy.length > 0;
            row.querySelector('[data-owner-warning]').textContent = usedBy.length ? `Used by ${usedBy.map(title).join(', ')}. Reassign or remove those options before removing this source or sharing its stock.` : '';
            const summary = row.querySelector('[data-stock-summary]');
            const sold = row.querySelector('[data-sold-unit]').selectedOptions[0];
            const soldName = sold?.dataset.unitName?.toLowerCase() || 'sold unit';
            if (!shared) {
                summary.textContent = `Counted separately: selling 1 ${soldName} uses 1 of this option's own stock units. Other option balances remain unchanged.`;
                return;
            }
            const owner = choices.find(other => rowKey(other) === sourceKey(row));
            const base = owner?.querySelector('[data-sold-unit]').selectedOptions[0];
            const measured = ['3', '4'].includes(base?.dataset.unitKind);
            const packed = ['1', '2'].includes(sold?.dataset.unitKind);
            const increments = ['1', '0.1', '0.01', '0.001', '0.0001', '0.00001', '0.000001'];
            quantity.step = quantity.min = base?.value ? increments[Number(base.dataset.unitDecimals)] || '0.000001' : '0.000001';
            row.querySelector('[data-consumption-label]').textContent = packed && !measured ? 'Units per pack' : 'Stock quantity per sale';
            row.querySelector('[data-consumption-help]').textContent = (quantity.dataset.review === 'true' ? 'The source or unit changed. Re-enter the stock quantity. ' : '') + (base?.value ? `Enter the ${base.dataset.unitName.toLowerCase()} quantity used by one sale. No quantity is assumed.` : 'Choose a source with a selling unit, then enter the quantity used by one sale.');
            if (!owner || !base?.value || !quantity.value || Number(quantity.value) <= 0) {
                summary.textContent = 'Choose a separately counted source and enter a positive stock quantity. No pack size is assumed.';
                return;
            }
            summary.textContent = `Selling 1 ${soldName} removes ${quantity.value} ${base.dataset.unitName.toLowerCase()} (${base.dataset.unitSymbol}) from “${title(owner)}” stock when inventory is enabled.`;
        });
    }
    const add = form.querySelector('[data-add-variant]'); add.hidden = false;
    add.addEventListener('click', () => {
        if (list.children.length + form.querySelectorAll('[data-existing-option]').length >= 25) return;
        const fragment = document.querySelector('#variant-template').content.cloneNode(true);
        fragment.querySelector('[data-row-key]').value = crypto.randomUUID();
        list.append(fragment); renumber();
        list.lastElementChild.querySelector('input:not([type="hidden"])').focus();
    });
    list.addEventListener('click', event => {
        const row = event.target.closest('[data-variant]'); if (!row) return;
        if (event.target.closest('[data-remove-variant]') && (form.hasAttribute('data-edit-product') || list.children.length > 1) && dependents(row).length === 0) { row.remove(); renumber(); add.focus(); }
        if (event.target.closest('[data-add-conversion]')) {
            const target = row.querySelector('[data-conversion-list]'); if (target.children.length >= 10) return;
            target.append(document.querySelector('#conversion-template').content.cloneNode(true)); renumber(); target.lastElementChild.querySelector('select').focus();
        }
        if (event.target.closest('[data-remove-conversion]')) { event.target.closest('[data-conversion]').remove(); renumber(); row.querySelector('[data-add-conversion]').focus(); }
    });
    list.addEventListener('input', event => {
        if (event.target.matches('[data-stock-quantity]')) delete event.target.dataset.review;
        if (!event.target.matches('[data-stock-mode]')) refreshStock();
    });
    list.addEventListener('change', event => {
        const row = event.target.closest('[data-variant]');
        const resetQuantity = option => { const quantity = option.querySelector('[data-stock-quantity]'); quantity.value = ''; quantity.dataset.review = 'true'; };
        if (row && event.target.matches('[data-stock-owner]')) resetQuantity(row);
        if (row && event.target.matches('[data-sold-unit]')) {
            dependents(row).forEach(resetQuantity);
            if (isShared(row)) resetQuantity(row);
        }
        if (row && event.target.matches('[data-stock-mode]') && isShared(row) && dependents(row).length) {
            const own = row.querySelector('[data-stock-mode][value="Own"]'); own.checked = true; own.focus();
        }
        refreshStock();
    });
    renumber();
    function focusField(field) {
        if (field?.type === 'hidden') field = field.closest('[data-variant], [data-existing-option]')?.querySelector('[data-option-name], input:not([type="hidden"])');
        if (!field) return;
        for (let parent = field.parentElement; parent && parent !== form; parent = parent.parentElement) if (parent.tagName === 'DETAILS') parent.open = true;
        field.focus();
    }
    form.addEventListener('invalid', event => focusField(event.target), true);
    const summary = form.querySelector('.validation-summary-errors');
    if (summary) {
        form.querySelectorAll('.field-validation-error[data-valmsg-for]').forEach(error => {
            const field = [...form.querySelectorAll('[name]')].find(input => input.name === error.dataset.valmsgFor);
            if (field) field.setAttribute('aria-invalid', 'true');
            [...summary.querySelectorAll('li')].filter(item => item.textContent.trim() === error.textContent.trim()).forEach(item => {
                const link = document.createElement('a'); link.href = '#' + (field?.id || 'selling-title'); link.className = 'underline'; link.textContent = item.textContent;
                link.addEventListener('click', event => { event.preventDefault(); focusField(field); }); item.replaceChildren(link);
            });
        });
        summary.focus();
    }
    const files = form.querySelector('#product-images'), previews = form.querySelector('[data-image-previews]'), clear = form.querySelector('[data-clear-images]');
    function resetPreviews() { urls.forEach(URL.revokeObjectURL); urls = []; previews?.replaceChildren(); }
    files?.addEventListener('change', () => {
        resetPreviews(); clear.hidden = files.files.length === 0;
        [...files.files].slice(0,5).forEach((file,i) => {
            const label = document.createElement('label'); label.className = 'rounded-xl border border-border p-3 text-sm';
            const radio = document.createElement('input'); radio.type = 'radio'; radio.name = 'PrimaryImageIndex'; radio.value = i; radio.checked = i === 0; radio.className = 'mr-2 accent-brand';
            label.append(radio, document.createTextNode('Primary image · preview only'));
            if (['image/jpeg','image/png','image/webp'].includes(file.type) && file.size <= 5*1024*1024) {
                const image = document.createElement('img'); image.src = URL.createObjectURL(file); urls.push(image.src); image.alt = 'Selected image preview'; image.className = 'mt-3 h-32 w-full object-contain'; label.append(image);
            }
            previews.append(label);
        });
    });
    clear?.addEventListener('click', () => { files.value = ''; resetPreviews(); clear.hidden = true; files.focus(); });
    window.addEventListener('pagehide', resetPreviews);
})();


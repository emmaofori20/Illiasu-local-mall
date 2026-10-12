// Requires CategorySmoke --preview and a local headless Edge CDP endpoint on 9225.
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
(async () => {
  const fixture = JSON.parse(fs.readFileSync(path.join(os.tmpdir(), 'illiasu-category-preview.json')));
  const tab = await (await fetch('http://localhost:9225/json/new?about:blank', { method: 'PUT' })).json();
  const ws = new WebSocket(tab.webSocketDebuggerUrl);
  await new Promise(resolve => ws.addEventListener('open', resolve, { once: true }));
  let seq = 0; const pending = new Map();
  ws.addEventListener('message', event => { const msg = JSON.parse(event.data); if (msg.id) { pending.get(msg.id)?.(msg); pending.delete(msg.id); } });
  const call = (method, params = {}) => new Promise((resolve, reject) => { const id = ++seq; pending.set(id, msg => msg.error ? reject(msg.error) : resolve(msg.result)); ws.send(JSON.stringify({ id, method, params })); });
  const evaluate = async expression => (await call('Runtime.evaluate', { expression, returnByValue: true })).result.value;
  await call('Network.enable');
  for (const cookie of fixture.cookies) await call('Network.setCookie', { ...cookie, url: 'https://localhost:7116' });
  const out = path.join(os.tmpdir(), 'illiasu-category-screenshots'); fs.mkdirSync(out, { recursive: true });
  const results = [];
  const wait = async expression => {
    for(let n=0;n<80;n++) { if(await evaluate(expression)) return; await new Promise(r=>setTimeout(r,100)); }
    throw new Error('Timed out: '+expression);
  };
  const check = async (expression,label) => { if(!await evaluate(expression)) throw new Error(label); results.push(label); };
  const open = async selector => {
    await evaluate(`{const link=document.querySelector(${JSON.stringify(selector)});const menu=link.closest('[data-category-menu]');if(menu && !menu.matches(':popover-open')) document.getElementById(menu.id+'-trigger').click();}`);
    await new Promise(resolve=>setTimeout(resolve,50));
    await evaluate(`document.querySelector(${JSON.stringify(selector)}).click()`);
    await wait(`!!document.querySelector('#admin-form-dialog[open] form')`);
  };
  const close = async () => { await evaluate(`document.querySelector('[data-close-admin-form]').click()`); await wait(`!document.querySelector('#admin-form-dialog').open`); };
  const fill = async (name,value) => evaluate(`{const field=document.querySelector('#admin-form-dialog [name="${name}"]'); field.value=${JSON.stringify(value)};field.dispatchEvent(new Event('input',{bubbles:true}));}`);
  const submit = async () => evaluate(`document.querySelector('#admin-form-dialog form').requestSubmit()`);
  await call('Page.navigate',{url:'https://localhost:7116/Admin/Categories?q='+fixture.query+'&status=active'});
  await wait(`!!document.querySelector('[data-categories-list]')`);
  await check(`document.querySelector('.admin-page-heading a[data-category-dialog] svg')!==null`,'Add category has a leading icon');
  await evaluate(`document.querySelector('nav[aria-label="Category pagination"] a[aria-label="Next page"]').click()`);
  await wait(`document.querySelector('nav[aria-label="Category pagination"] a[aria-current="page"]')?.textContent==='2'`);
  await check(`location.search.includes('q=') && location.search.includes('status=active') && document.querySelector('[data-categories-list]').textContent.includes('Showing 11')`,'Pagination preserves search/status and shows result range');
  await evaluate(`const size=document.querySelector('[data-page-size]');size.value='25';size.dispatchEvent(new Event('change',{bubbles:true}));`);
  await wait(`location.search.includes('pageSize=25')`);
  await wait(`document.querySelectorAll('tbody tr').length===22`);
  await check(`location.search.includes('q=') && location.search.includes('status=active') && document.querySelector('nav[aria-label="Category pagination"] a[aria-current="page"]').textContent==='1'`,'Page size resets page and preserves filters');
  for(const width of [375,768,1440]) {
    await call('Emulation.setDeviceMetricsOverride',{width,height:1000,deviceScaleFactor:1,mobile:false});
    await new Promise(resolve=>setTimeout(resolve,150));
    await evaluate(`document.querySelector('[data-category-menu-trigger]').scrollIntoView({block:'center',inline:'center'});document.querySelector('[data-category-menu-trigger]').click()`);
    await wait(`!!document.querySelector('[data-category-menu]:popover-open')`);
    await check(`document.querySelector('[data-category-menu-trigger]').getAttribute('aria-expanded')==='true' && document.activeElement.getAttribute('role')==='menuitem'`,'Menu opens with correct ARIA/focus '+width);
    await check(`document.querySelector('[data-category-menu]:popover-open').getBoundingClientRect().right<=innerWidth && document.querySelector('[data-category-menu]:popover-open').querySelectorAll('[role="menuitem"]').length===2`,'Menu fits and has only edit/status actions '+width);
    const menuShot=await call('Page.captureScreenshot',{format:'png'});fs.writeFileSync(path.join(out,'category-menu-'+width+'.png'),Buffer.from(menuShot.data,'base64'));
    await call('Input.dispatchKeyEvent',{type:'keyDown',key:'ArrowDown',code:'ArrowDown',windowsVirtualKeyCode:40});
    await check(`document.activeElement===document.querySelector('[data-category-menu]:popover-open').lastElementChild`,'Menu arrow navigation '+width);
    await call('Input.dispatchKeyEvent',{type:'keyDown',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});
    await call('Input.dispatchKeyEvent',{type:'keyUp',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});
    await check(`!document.querySelector('[data-category-menu]:popover-open') && document.activeElement.matches('[data-category-menu-trigger]')`,'Menu Escape restores focus '+width);
    await open('a[data-category-dialog][href$="Create"]');
    await check(`document.activeElement.name==='Name'`,'Create initial focus '+width);
    await fill('Name','Fresh Fruit & Vegetables');
    await check(`document.querySelector('#admin-form-dialog [name="Slug"]').value==='fresh-fruit-vegetables'`,'Automatic slug '+width);
    const screenshot=await call('Page.captureScreenshot',{format:'png'});fs.writeFileSync(path.join(out,'modal-create-'+width+'.png'),Buffer.from(screenshot.data,'base64'));
    await check(`document.querySelector('#admin-form-dialog').getBoundingClientRect().right<=innerWidth && document.documentElement.scrollWidth<=innerWidth`,'Modal fits '+width);
    await call('Input.dispatchKeyEvent',{type:'keyDown',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});
    await call('Input.dispatchKeyEvent',{type:'keyUp',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});
    await wait(`!document.querySelector('#admin-form-dialog').open`);
    await wait(`document.activeElement.matches('a[data-category-dialog][href$="Create"]')`);
    await check(`document.activeElement.matches('a[data-category-dialog][href$="Create"]')`,'Escape focus restoration '+width);
  }
  await open('a[data-category-dialog][href$="Create"]');
  await fill('Name','AA QA modal produce');
  await evaluate(`document.querySelector('#admin-form-dialog [data-edit-slug]').click()`);
  await fill('Slug',fixture.query);
  await fill('Name','AA QA renamed produce');
  await check(`document.querySelector('#admin-form-dialog [name="Slug"]').value===${JSON.stringify(fixture.query)}`,'Manual slug stops auto generation');
  await submit();await wait(`!!document.querySelector('#admin-form-dialog .validation-summary-errors')`);
  await check(`document.querySelector('#admin-form-dialog [name="Name"]').value==='AA QA renamed produce' && document.querySelector('#admin-form-dialog [name="Slug"]').value===${JSON.stringify(fixture.query)}`,'Duplicate validation retains both values');
  await check(`document.activeElement.matches('.validation-summary-errors')`,'Validation summary receives focus');
  await evaluate(`document.querySelector('#admin-form-dialog [data-edit-slug]').click()`);
  await fill('Slug',fixture.query+'-ui');await submit();
  await wait(`!document.querySelector('#admin-form-dialog').open && !!document.querySelector('[data-category-success]')`);
  await check(`document.querySelector('[data-categories-list]').textContent.includes('AA QA renamed produce')`,'Create updates listing with feedback');
  await open('a[data-category-dialog][href*="Edit"]');
  await check(`document.querySelector('#admin-form-dialog [name="Name"]').value==='AA QA renamed produce'`,'Edit prefilled');
  await fill('Name','AA QA edited produce');
  await check(`document.querySelector('#admin-form-dialog [name="Slug"]').value===${JSON.stringify(fixture.query+'-ui')}`,'Editing name preserves existing slug');
  await submit();await wait(`!document.querySelector('#admin-form-dialog').open`);
  await open('a[data-category-dialog][href*="Status"]');
  await check(`document.querySelector('#admin-form-dialog').textContent.includes('AA QA edited produce') && document.querySelector('#admin-form-dialog').textContent.includes('Historical references are preserved')`,'Confirmation identifies category and visibility impact');
  for(const width of [375,768,1440]) {
    await call('Emulation.setDeviceMetricsOverride',{width,height:1000,deviceScaleFactor:1,mobile:false});
    await check(`document.querySelector('#admin-form-dialog').getBoundingClientRect().right<=innerWidth && document.documentElement.scrollWidth<=innerWidth`,'Confirmation fits '+width);
    const shot=await call('Page.captureScreenshot',{format:'png'});fs.writeFileSync(path.join(out,'modal-status-'+width+'.png'),Buffer.from(shot.data,'base64'));
  }
  await evaluate(`document.querySelector('#admin-form-dialog [name="Confirm"]').checked=true`);
  await submit();await wait(`!document.querySelector('#admin-form-dialog').open`);
  await check(`document.querySelector('tbody tr').textContent.includes('Inactive')`,'Deactivate updates status');
  await open('a[data-category-dialog][href*="Status"]');
  await evaluate(`document.querySelector('#admin-form-dialog [name="Confirm"]').checked=true`);
  await submit();await wait(`!document.querySelector('#admin-form-dialog').open`);
  await check(`document.querySelector('tbody tr').textContent.includes('Active')`,'Activate updates status');
  await open('a[data-category-dialog][href$="Create"]');
  for(let n=0;n<15;n++) {
    await call('Input.dispatchKeyEvent',{type:'keyDown',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});
    await call('Input.dispatchKeyEvent',{type:'keyUp',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});
    await check(`document.querySelector('#admin-form-dialog').contains(document.activeElement)`,'Native Tab focus containment '+n);
  }
  await close();
  console.log(JSON.stringify({results,screenshots:out},null,2));
  await call('Page.close'); ws.close();
  fs.writeFileSync(path.join(os.tmpdir(),'illiasu-category-preview.json.done'),'done');
})().catch(error => { console.error(error); process.exit(1); });

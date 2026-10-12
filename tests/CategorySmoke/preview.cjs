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
  for (const width of [375, 768, 1440]) {
    await call('Emulation.setDeviceMetricsOverride', { width, height: 1000, deviceScaleFactor: 1, mobile: false });
    for (const [name, route] of [['list', '/Admin/Categories?q='+fixture.query], ['create','/Admin/Categories/Create'], ['edit','/Admin/Categories/Edit/'+fixture.id], ['status','/Admin/Categories/Status/'+fixture.id], ['empty','/Admin/Categories?q='+fixture.query+'-missing']]) {
      await call('Page.navigate', { url: 'https://localhost:7116'+route });
      await new Promise(resolve => setTimeout(resolve, 700));
      const info = await evaluate(`({title:document.querySelector('h1')?.textContent, width:innerWidth, scroll:document.documentElement.scrollWidth, font:getComputedStyle(document.body).fontFamily, live:document.querySelector('.admin-sample-badge')?.textContent, fields:[...document.querySelectorAll('.category-input')].map(x=>({width:x.getBoundingClientRect().width,height:x.getBoundingClientRect().height})),table:document.querySelector('.admin-table-scroll')?.scrollWidth})`);
      if (!info.title || info.scroll > width) throw new Error(name+' '+width+' failed: '+JSON.stringify(info));
      const shot = await call('Page.captureScreenshot', { format:'png', captureBeyondViewport:true }); fs.writeFileSync(path.join(out,name+'-'+width+'.png'),Buffer.from(shot.data,'base64'));
      results.push({ name,width,...info });
    }
    if (width === 375) {
      await evaluate(`document.querySelector('#admin-menu').click()`);
      if (!await evaluate(`document.querySelector('#admin-drawer').open`)) throw new Error('Drawer did not open');
      await call('Input.dispatchKeyEvent',{type:'keyDown',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});
      await call('Input.dispatchKeyEvent',{type:'keyUp',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});
      if (await evaluate(`document.querySelector('#admin-drawer').open`)) throw new Error('Escape did not close drawer');
      results.push({ mobileDrawer:'Open and Escape close passed',focusRestored:await evaluate(`document.activeElement.id==='admin-menu'`) });
    }
  }
  console.log(JSON.stringify({results,screenshots:out},null,2));
  await call('Page.close'); ws.close();
})().catch(error => { console.error(error); process.exit(1); });

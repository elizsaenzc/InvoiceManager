// Capturas de las pantallas principales (escritorio y móvil) para revisar el diseño
const { chromium } = require('playwright');
const fs = require('fs');
const BASE = process.env.BASE_URL || 'http://localhost:8080';
const OUT = process.argv[2] || 'shots';
fs.mkdirSync(OUT, { recursive: true });
const log = [];

(async () => {
  const browser = await chromium.launch();
  const desk = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: 'es-ES', timezoneId: 'Europe/Madrid' });
  const page = await desk.newPage();
  page.setDefaultTimeout(15000);
  page.setDefaultNavigationTimeout(20000);
  page.on('pageerror', e => log.push(`pageerror ${page.url()}: ${e.message}`));
  page.on('console', m => { if (m.type() === 'error') log.push(`console ${page.url()}: ${m.text()}`); });
  const shot = async (name, full = false) => { await page.waitForTimeout(600); await page.screenshot({ path: `${OUT}/${name}.png`, fullPage: full }); };
  const go = async (path) => { log.push(`→ ${path}`); await page.goto(BASE + path, { waitUntil: 'load' }); await page.waitForLoadState('networkidle', { timeout: 5000 }).catch(() => {}); };
  const firstId = async (selector) => {
    const href = await page.locator(selector).first().getAttribute('data-href');
    return href;
  };

  await go('/'); await shot('d-panel', true);
  await go('/Invoices'); await shot('d-facturas');
  await go('/Invoices?status=Overdue'); await shot('d-vencidas');
  const overdue = await firstId('tr[data-href]');
  if (overdue) { await go(overdue); await shot('d-factura-vencida', true); }
  await go('/Invoices?status=Paid');
  const paid = await firstId('tr[data-href]');
  if (paid) {
    await go(paid); await shot('d-factura-cobrada', true);
    await go(paid.replace('Details', 'Print')); await shot('d-imprimir', true);
  }
  await go('/Invoices?status=Open');
  const open = await firstId('tr[data-href]');
  if (open) {
    await go(open);
    await page.getByRole('button', { name: 'Registrar cobro' }).first().click();
    await shot('d-modal-cobro');
  }
  await go('/Invoices/Create');
  await page.selectOption('#client', { index: 2 });
  await page.locator('[data-line-desc]').first().fill('Posicionamiento SEO local (mensual)');
  await page.locator('#add-line').click();
  await page.locator('[data-line-desc]').nth(1).fill('Soporte técnico fuera de plan (horas)');
  await page.locator('[data-line-qty]').nth(1).fill('3');
  await page.locator('#terms a[data-days="15"]').click();
  await shot('d-nueva-factura', true);
  await go('/Clients'); await shot('d-clientes', true);
  const client = await page.locator('a.client-card').first().getAttribute('href');
  await go(client); await shot('d-cliente', true);
  await go('/Reports'); await shot('d-iva', true);
  await go('/Invoices/Details/999999'); await shot('d-404');

  const mob = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true, locale: 'es-ES' });
  const m = await mob.newPage();
  m.setDefaultTimeout(15000);
  m.on('pageerror', e => log.push(`pageerror móvil ${m.url()}: ${e.message}`));
  for (const [name, path] of [['m-panel', '/'], ['m-facturas', '/Invoices'], ['m-factura', overdue || '/Invoices'], ['m-clientes', '/Clients']]) {
    await m.goto(BASE + path, { waitUntil: 'load' }); await m.waitForLoadState('networkidle', { timeout: 5000 }).catch(() => {});
    await m.waitForTimeout(600);
    await m.screenshot({ path: `${OUT}/${name}.png` });
  }

  fs.writeFileSync(`${OUT}/log.txt`, log.join('\n'));
  console.log(log.join('\n') || 'Sin errores de JavaScript');
  await browser.close();
})().catch(e => { console.error(e); fs.writeFileSync(`${OUT}/log.txt`, log.join('\n') + '\n\nERROR: ' + String(e.stack || e)); process.exit(1); });

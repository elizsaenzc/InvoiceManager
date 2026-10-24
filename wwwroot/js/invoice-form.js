/* Formulario de factura: conceptos dinámicos, totales en vivo y plazo de pago */
(function () {
  'use strict';

  const form = document.getElementById('invoice-form');
  if (!form) return;
  const lines = document.getElementById('lines');
  const template = document.getElementById('line-template');
  const taxRate = document.getElementById('tax-rate');
  const client = document.getElementById('client');
  const issue = document.getElementById('issue-date');
  const due = document.getElementById('due-date');
  const terms = document.getElementById('terms');
  const concepts = window.INVOICE_CONCEPTS || {};

  const eur = n => n.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' });
  const round2 = n => Math.round(n * 100) / 100;
  const num = el => parseFloat(el?.value) || 0;

  function recalc() {
    let base = 0;
    lines.querySelectorAll('[data-line]').forEach(row => {
      const total = round2(num(row.querySelector('[data-line-qty]')) * num(row.querySelector('[data-line-price]')));
      row.querySelector('[data-line-total]').textContent = eur(total);
      base += total;
    });
    const rate = num(taxRate);
    const tax = round2(base * rate / 100);
    document.getElementById('s-base').textContent = eur(base);
    document.getElementById('s-rate').textContent = rate.toLocaleString('es-ES');
    document.getElementById('s-tax').textContent = eur(tax);
    document.getElementById('s-total').textContent = eur(base + tax);
  }

  function reindex() {
    lines.querySelectorAll('[data-line]').forEach((row, i) => {
      row.querySelectorAll('[name]').forEach(el => { el.name = el.name.replace(/Lines\[[^\]]+\]/, `Lines[${i}]`); });
    });
  }

  function addLine(focus = true) {
    const html = template.innerHTML.replaceAll('__i__', lines.children.length);
    lines.insertAdjacentHTML('beforeend', html);
    reindex();
    if (focus) lines.lastElementChild.querySelector('[data-line-desc]').focus();
  }

  // Plazo de pago: días entre emisión y vencimiento
  const toDate = s => (s ? new Date(s + 'T00:00:00') : null);
  const iso = d => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  function currentDays() {
    const a = toDate(issue.value), b = toDate(due.value);
    return a && b ? Math.round((b - a) / 86400000) : null;
  }
  function setDays(days) {
    const a = toDate(issue.value);
    if (!a) return;
    a.setDate(a.getDate() + days);
    due.value = iso(a);
    markTerms();
  }
  function markTerms() {
    const d = currentDays();
    terms.querySelectorAll('a').forEach(a => a.classList.toggle('active', Number(a.dataset.days) === d));
  }

  terms.addEventListener('click', e => {
    const a = e.target.closest('a[data-days]');
    if (!a) return;
    e.preventDefault();
    setDays(Number(a.dataset.days));
  });

  let lastDays = currentDays();
  issue.addEventListener('change', () => { if (lastDays !== null) setDays(lastDays); });
  due.addEventListener('change', () => { lastDays = currentDays(); markTerms(); });
  client.addEventListener('change', () => {
    const t = client.selectedOptions[0]?.dataset.terms;
    if (t !== undefined) { lastDays = Number(t); setDays(lastDays); }
  });

  lines.addEventListener('input', e => {
    // Al elegir un concepto ya facturado se propone su último precio
    if (e.target.matches('[data-line-desc]') && concepts[e.target.value] !== undefined) {
      const price = e.target.closest('[data-line]').querySelector('[data-line-price]');
      if (!price.value || Number(price.value) === 0) price.value = Number(concepts[e.target.value]).toFixed(2);
    }
    recalc();
  });
  taxRate.addEventListener('change', recalc);

  lines.addEventListener('click', e => {
    if (!e.target.closest('[data-remove-line]')) return;
    const row = e.target.closest('[data-line]');
    if (lines.querySelectorAll('[data-line]').length > 1) row.remove();
    else row.querySelectorAll('input').forEach(i => { i.value = i.matches('[data-line-qty]') ? '1' : ''; });
    reindex();
    recalc();
  });

  document.getElementById('add-line').addEventListener('click', () => addLine());

  // Intro en el último precio añade un concepto nuevo en lugar de enviar el formulario
  lines.addEventListener('keydown', e => {
    if (e.key === 'Enter' && e.target.matches('input')) {
      e.preventDefault();
      if (e.target.closest('[data-line]') === lines.lastElementChild) addLine();
    }
  });

  markTerms();
  recalc();
})();

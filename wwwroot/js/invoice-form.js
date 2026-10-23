/* Invoice form: dynamic lines and live total calculation */
(function () {
  'use strict';

  const container = document.getElementById('lines-container');
  const addBtn    = document.getElementById('add-line');
  const taxInput  = document.getElementById('tax-rate');

  let lineIndex = container ? container.querySelectorAll('[data-line]').length : 0;

  function fmtEur(n) {
    return n.toLocaleString('es-ES', { style: 'currency', currency: 'EUR' });
  }

  function calcLine(row) {
    const qty   = parseFloat(row.querySelector('[data-line-qty]')?.value)   || 0;
    const price = parseFloat(row.querySelector('[data-line-price]')?.value) || 0;
    const total = qty * price;
    const span  = row.querySelector('[data-line-total]');
    if (span) span.textContent = fmtEur(total);
    return total;
  }

  function calcAll() {
    let subtotal = 0;
    container.querySelectorAll('[data-line]').forEach(row => {
      subtotal += calcLine(row);
    });
    const taxPct  = parseFloat(taxInput?.value) || 0;
    const tax     = subtotal * taxPct / 100;
    const total   = subtotal + tax;

    const el = id => document.getElementById(id);
    if (el('s-subtotal')) el('s-subtotal').textContent = fmtEur(subtotal);
    if (el('s-tax'))      el('s-tax').textContent      = fmtEur(tax);
    if (el('s-total'))    el('s-total').textContent    = fmtEur(total);
    if (el('s-tax-pct'))  el('s-tax-pct').textContent  = taxPct;
  }

  function reindex() {
    container.querySelectorAll('[data-line]').forEach((row, i) => {
      row.querySelectorAll('[name]').forEach(el => {
        el.name = el.name.replace(/Lines\[\d+\]/, `Lines[${i}]`);
      });
    });
    lineIndex = container.querySelectorAll('[data-line]').length;
  }

  function addLine() {
    const html = `
      <div class="line-row lines-grid" data-line>
        <input type="hidden" name="Lines[${lineIndex}].Id" value="0" />
        <div>
          <input type="text" name="Lines[${lineIndex}].Description"
                 class="form-control" placeholder="Descripción del servicio o producto" data-line-desc />
        </div>
        <div>
          <input type="number" name="Lines[${lineIndex}].Quantity"
                 class="form-control text-center" min="0.01" step="0.01" value="1" data-line-qty />
        </div>
        <div>
          <input type="number" name="Lines[${lineIndex}].UnitPrice"
                 class="form-control text-end" min="0" step="0.01" placeholder="0,00" data-line-price />
        </div>
        <div class="d-flex align-items-center justify-content-end">
          <span class="line-total" data-line-total>0,00 €</span>
        </div>
        <div class="d-flex align-items-center justify-content-center">
          <button type="button" class="btn btn-icon text-danger" data-remove-line title="Eliminar línea">
            <i class="bi bi-trash"></i>
          </button>
        </div>
      </div>`;
    container.insertAdjacentHTML('beforeend', html);
    lineIndex++;
    // Focus new description
    const rows = container.querySelectorAll('[data-line]');
    rows[rows.length - 1].querySelector('[data-line-desc]')?.focus();
  }

  // Event delegation
  document.addEventListener('input', e => {
    if (e.target.closest('[data-line]')) calcAll();
    if (e.target === taxInput) calcAll();
  });

  document.addEventListener('click', e => {
    if (e.target.closest('[data-remove-line]')) {
      const row = e.target.closest('[data-line]');
      if (container.querySelectorAll('[data-line]').length > 1) {
        row.remove();
        reindex();
        calcAll();
      } else {
        // Clear instead of remove if it's the last line
        row.querySelectorAll('input[type="text"], input[type="number"]').forEach(i => i.value = '');
        calcAll();
      }
    }
    if (e.target === addBtn || e.target.closest('#add-line') === addBtn) {
      addLine();
    }
  });

  // Init: calculate existing lines on page load (edit mode)
  calcAll();
})();

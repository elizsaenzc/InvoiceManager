/* Comportamiento común: avisos, confirmaciones y filas clicables */
(function () {
  'use strict';

  // Los avisos se ocultan solos
  document.querySelectorAll('.toast-ios').forEach(el => {
    setTimeout(() => el.classList.add('hide'), 3800);
    setTimeout(() => el.remove(), 4200);
  });

  // Filas de tabla que llevan a otra página (sin interferir con enlaces y botones internos)
  document.addEventListener('click', e => {
    const row = e.target.closest('[data-href]');
    if (!row || e.target.closest('a, button, input, select, form, label')) return;
    if (e.metaKey || e.ctrlKey) window.open(row.dataset.href, '_blank');
    else location.href = row.dataset.href;
  });

  // Confirmación con diálogo propio: <form data-confirm="Texto" data-confirm-title="…" data-confirm-ok="Eliminar" data-confirm-danger>
  const modalEl = document.getElementById('confirm-modal');
  if (!modalEl || !window.bootstrap) return;
  const modal = new bootstrap.Modal(modalEl);
  const okBtn = modalEl.querySelector('[data-confirm-ok]');
  let pending = null;

  document.addEventListener('submit', e => {
    const form = e.target;
    if (!form.dataset.confirm || form.dataset.confirmed) return;
    e.preventDefault();
    pending = form;
    modalEl.querySelector('[data-confirm-title]').textContent = form.dataset.confirmTitle || '¿Confirmar?';
    modalEl.querySelector('[data-confirm-text]').textContent = form.dataset.confirm;
    okBtn.textContent = form.dataset.confirmOk || 'Aceptar';
    okBtn.className = 'btn ' + ('confirmDanger' in form.dataset ? 'btn-danger-tinted' : 'btn-primary');
    modal.show();
  });

  okBtn.addEventListener('click', () => {
    if (!pending) return;
    pending.dataset.confirmed = '1';
    modal.hide();
    pending.requestSubmit ? pending.requestSubmit() : pending.submit();
  });
})();

// Klinova client-side behaviour. No inline scripts are used so that the
// Content-Security-Policy can forbid them.
(function () {
    'use strict';

    // Mobile sidebar toggle.
    document.querySelectorAll('[data-sidebar-toggle]').forEach(function (el) {
        el.addEventListener('click', function () {
            document.body.classList.toggle('sidebar-open');
        });
    });

    // Clickable table rows: <tr data-href="/Patients/Details/1">.
    document.querySelectorAll('tr[data-href]').forEach(function (row) {
        row.addEventListener('click', function (e) {
            if (e.target.closest('a, button, input, select, form, label')) {
                return;
            }
            window.location.href = row.getAttribute('data-href');
        });
    });

    // Confirmation for destructive actions: <button data-confirm="Emin misiniz?">.
    document.addEventListener('submit', function (e) {
        var submitter = e.submitter;
        var message = (submitter && submitter.getAttribute('data-confirm')) || e.target.getAttribute('data-confirm');
        if (message && !window.confirm(message)) {
            e.preventDefault();
        }
    });

    // Print buttons.
    document.querySelectorAll('[data-print]').forEach(function (btn) {
        btn.addEventListener('click', function () { window.print(); });
    });
    if (document.body.hasAttribute('data-autoprint')) {
        window.addEventListener('load', function () { window.print(); });
    }

    // Horizontal bars: <div class="bar-fill" data-percent="42">.
    document.querySelectorAll('[data-percent]').forEach(function (bar) {
        var value = Math.max(0, Math.min(100, parseFloat(bar.getAttribute('data-percent')) || 0));
        bar.style.width = value + '%';
    });

    // Auto-submit filters: <select data-autosubmit>.
    document.querySelectorAll('[data-autosubmit]').forEach(function (el) {
        el.addEventListener('change', function () { el.form && el.form.submit(); });
    });

    // Live price preview on the service picker.
    document.querySelectorAll('[data-price-source]').forEach(function (select) {
        var target = document.querySelector(select.getAttribute('data-price-source'));
        var update = function () {
            var option = select.options[select.selectedIndex];
            if (target) {
                target.textContent = option ? (option.getAttribute('data-price') || '') : '';
            }
        };
        select.addEventListener('change', update);
        update();
    });

    // Keep focus on the first invalid field after server-side validation.
    var invalid = document.querySelector('.input-validation-error');
    if (invalid) {
        invalid.focus();
    }
})();

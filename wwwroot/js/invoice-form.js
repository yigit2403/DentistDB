(() => {
    "use strict";

    const form = document.querySelector("[data-invoice-form]");
    if (!form) return;

    const tbody = form.querySelector("[data-items-body]");
    const template = form.querySelector("#invoice-item-template");
    const addButton = form.querySelector("[data-add-item]");
    const totalEl = form.querySelector("[data-items-total]");
    const procedures = JSON.parse(form.dataset.procedures || "[]");
    const parseNumber = (window.DentistDB && window.DentistDB.parseNumber) || (v => parseFloat(String(v).replace(",", ".")));
    const formatMoney = (window.DentistDB && window.DentistDB.formatMoney) || (v => v.toFixed(2));

    const formatInput = value => {
        if (value === null || value === undefined || Number.isNaN(value)) return "";
        return value.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2, useGrouping: false });
    };

    const reindex = () => {
        [...tbody.querySelectorAll("tr[data-item-row]")].forEach((row, index) => {
            row.querySelectorAll("[name]").forEach(el => {
                el.name = el.name.replace(/Items\[\d+\]/, `Items[${index}]`);
                if (el.id) el.id = el.id.replace(/Items_\d+__/, `Items_${index}__`);
            });
            row.querySelectorAll("[data-valmsg-for]").forEach(el => {
                el.setAttribute("data-valmsg-for", el.getAttribute("data-valmsg-for").replace(/Items\[\d+\]/, `Items[${index}]`));
            });
            row.querySelector("[data-row-number]")?.replaceChildren(document.createTextNode(String(index + 1)));
        });
    };

    const rowTotal = row => {
        const qty = parseInt(row.querySelector("[data-qty]")?.value || "0", 10) || 0;
        const price = parseNumber(row.querySelector("[data-price]")?.value || "0");
        return qty * (Number.isNaN(price) ? 0 : price);
    };

    const recalc = () => {
        let total = 0;
        tbody.querySelectorAll("tr[data-item-row]").forEach(row => {
            const line = rowTotal(row);
            total += line;
            const cell = row.querySelector("[data-line-total]");
            if (cell) cell.textContent = formatMoney(line);
        });
        if (totalEl) totalEl.textContent = formatMoney(total);
    };

    const addRow = (focus = true) => {
        const fragment = template.content.cloneNode(true);
        tbody.appendChild(fragment);
        reindex();
        recalc();
        if (window.jQuery && jQuery.validator && jQuery.validator.unobtrusive) {
            const $form = jQuery(form);
            $form.removeData("validator");
            $form.removeData("unobtrusiveValidation");
            jQuery.validator.unobtrusive.parse(form);
        }
        if (focus) {
            const rows = tbody.querySelectorAll("tr[data-item-row]");
            rows[rows.length - 1]?.querySelector("[data-procedure]")?.focus();
        }
    };

    tbody.addEventListener("change", event => {
        const select = event.target.closest("[data-procedure]");
        if (select) {
            const row = select.closest("tr");
            const procedure = procedures.find(p => String(p.id) === select.value);
            const description = row.querySelector("[data-description]");
            const price = row.querySelector("[data-price]");
            if (procedure) {
                if (description && (!description.value.trim() || description.dataset.auto === "true")) {
                    description.value = procedure.name;
                    description.dataset.auto = "true";
                }
                if (price && (!parseNumber(price.value) || price.dataset.auto === "true")) {
                    price.value = formatInput(procedure.price);
                    price.dataset.auto = "true";
                }
            }
        }
        recalc();
    });

    tbody.addEventListener("input", event => {
        const target = event.target;
        if (target.matches("[data-description]") || target.matches("[data-price]")) {
            target.dataset.auto = "false";
        }
        recalc();
    });

    tbody.addEventListener("click", event => {
        const remove = event.target.closest("[data-remove-item]");
        if (!remove) return;
        const rows = tbody.querySelectorAll("tr[data-item-row]");
        if (rows.length <= 1) {
            const row = rows[0];
            row.querySelectorAll("input").forEach(el => { if (el.type !== "hidden") el.value = el.matches("[data-qty]") ? "1" : ""; });
            row.querySelectorAll("select").forEach(el => { el.value = ""; });
        } else {
            remove.closest("tr").remove();
        }
        reindex();
        recalc();
    });

    addButton?.addEventListener("click", () => addRow(true));

    // Payment plan toggle
    const toggle = form.querySelector(".payment-plan-toggle");
    const planFields = [...form.querySelectorAll(".payment-plan-field")];
    const syncPlan = () => {
        const enabled = !!(toggle && toggle.checked);
        planFields.forEach(field => {
            field.classList.toggle("d-none", !enabled);
            field.querySelectorAll("input, select").forEach(el => { el.disabled = !enabled || el.dataset.locked === "true"; });
        });
    };
    toggle?.addEventListener("change", syncPlan);
    syncPlan();

    // Keyboard: Enter in the last price field adds a new row.
    tbody.addEventListener("keydown", event => {
        if (event.key === "Enter" && event.target.matches("[data-price]")) {
            const rows = tbody.querySelectorAll("tr[data-item-row]");
            if (event.target.closest("tr") === rows[rows.length - 1]) {
                event.preventDefault();
                addRow(true);
            }
        }
    });

    reindex();
    recalc();
})();

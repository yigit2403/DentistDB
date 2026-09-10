(() => {
    "use strict";

    const form = document.querySelector("[data-appointment-form]");
    if (!form) return;

    const dateInput = form.querySelector("[name='Date']");
    const timeInput = form.querySelector("[name='Time']");
    const durationSelect = form.querySelector("[name='DurationMinutes']");
    const statusSelect = form.querySelector("[name='Status']");
    const box = form.querySelector("[data-conflict-live]");
    const conflictsUrl = form.dataset.conflictsUrl;
    const excludeId = form.dataset.excludeId || "";
    const endLabel = form.querySelector("[data-end-time]");

    if (!dateInput || !timeInput || !durationSelect) return;

    let timer = null;

    const pad = n => String(n).padStart(2, "0");

    const updateEndTime = () => {
        if (!endLabel) return;
        const [h, m] = (timeInput.value || "").split(":").map(Number);
        const duration = Number(durationSelect.value || 0);
        if (Number.isNaN(h) || Number.isNaN(m) || !duration) {
            endLabel.textContent = "";
            return;
        }
        const total = h * 60 + m + duration;
        endLabel.textContent = `Bitiş: ${pad(Math.floor(total / 60) % 24)}:${pad(total % 60)}`;
    };

    const blocksCalendar = () => !statusSelect || statusSelect.value === "Scheduled" || statusSelect.value === "Completed" || statusSelect.value === "0" || statusSelect.value === "1";

    const check = () => {
        updateEndTime();
        if (!box || !conflictsUrl || !dateInput.value || !timeInput.value || !blocksCalendar()) {
            if (box) box.hidden = true;
            return;
        }

        const start = `${dateInput.value}T${timeInput.value}`;
        const params = new URLSearchParams({ start, duration: durationSelect.value || "30" });
        if (excludeId) params.set("excludeId", excludeId);

        fetch(`${conflictsUrl}?${params.toString()}`, { headers: { Accept: "application/json" } })
            .then(r => (r.ok ? r.json() : []))
            .then(items => {
                if (!items.length) {
                    box.hidden = true;
                    return;
                }
                const list = items.map(i =>
                    `<li><strong>${i.start}–${i.end}</strong> ${escapeHtml(i.patient || "")}${i.purpose ? " · " + escapeHtml(i.purpose) : ""}</li>`
                ).join("");
                box.innerHTML = `<div><strong>Bu saat dolu görünüyor.</strong> Çakışan randevular:</div><ul>${list}</ul>`;
                box.hidden = false;
            })
            .catch(() => { box.hidden = true; });
    };

    const escapeHtml = text => String(text).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

    const schedule = () => {
        window.clearTimeout(timer);
        timer = window.setTimeout(check, 200);
    };

    [dateInput, timeInput, durationSelect, statusSelect].forEach(el => el && el.addEventListener("change", schedule));
    updateEndTime();
    check();
})();

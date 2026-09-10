(() => {
    "use strict";

    const root = document.querySelector("[data-odontogram]");
    if (!root) return;

    const stage = root.querySelector("[data-odo-stage]");
    const statuses = JSON.parse(root.dataset.statuses || "{}");
    const history = JSON.parse(root.dataset.history || "{}");
    const labels = JSON.parse(root.dataset.labels || "{}");
    const panel = root.querySelector("[data-odo-panel]");
    const form = root.querySelector("[data-odo-form]");
    const toothInput = form.querySelector("[name='ToothNumber']");
    const conditionSelect = form.querySelector("[name='Condition']");
    const noteInput = form.querySelector("[name='Note']");
    const title = root.querySelector("[data-odo-title]");
    const historyList = root.querySelector("[data-odo-history]");
    const emptyHint = root.querySelector("[data-odo-empty]");
    const dentitionButtons = (root.closest(".card") || root).querySelectorAll("[data-dentition]");

    Object.values(statuses).forEach(s => { s.label = labels[s.condition] || s.condition; });

    const escapeHtml = text => String(text ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

    function select(toothNo) {
        const status = statuses[toothNo];
        chart.setActive(toothNo);

        toothInput.value = toothNo;
        conditionSelect.value = status ? status.condition : "Healthy";
        noteInput.value = status ? (status.note || "") : "";
        title.textContent = window.ToothChart.toothName(toothNo) + (status ? ` · ${status.label}` : "");

        const items = history[toothNo] || [];
        historyList.innerHTML = items.length
            ? items.map(h => `<li><span class="text-muted">${escapeHtml(h.date)}</span> ${escapeHtml(h.title)} <span class="badge-soft neutral">${escapeHtml(h.source)}</span></li>`).join("")
            : '<li class="text-muted">Bu diş için kayıtlı işlem yok.</li>';

        panel.hidden = false;
        if (emptyHint) emptyHint.hidden = true;
        noteInput.focus({ preventScroll: true });
    }

    const hasDeciduous = Object.keys(statuses).some(window.ToothChart.isDeciduous);
    const hasPermanent = Object.keys(statuses).some(n => !window.ToothChart.isDeciduous(n));
    let dentition = root.dataset.dentition || (hasDeciduous && !hasPermanent ? "deciduous" : "permanent");

    const chart = new window.ToothChart(stage, {
        svgUrl: root.dataset.svgUrl,
        mode: "view",
        dentition,
        statuses,
        onPick: select
    });

    dentitionButtons.forEach(button => {
        button.classList.toggle("active", button.dataset.dentition === dentition);
        button.addEventListener("click", async () => {
            dentition = button.dataset.dentition;
            dentitionButtons.forEach(b => b.classList.toggle("active", b === button));
            chart.options.dentition = dentition;
            await chart.render();
        });
    });

    chart.render().then(() => {
        const preselect = new URLSearchParams(location.search).get("tooth");
        if (preselect && chart.teeth.has(preselect)) select(preselect);
    }).catch(err => {
        console.error(err);
        stage.innerHTML = '<div class="tooth-selector-error">Diş şeması yüklenemedi.</div>';
    });
})();

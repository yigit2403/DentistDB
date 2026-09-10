/*
 * Tooth selector form widget: chart + typed numbers + quadrant shortcuts + chips.
 * Posts a comma-separated list of FDI numbers in the hidden input.
 */
(() => {
    "use strict";

    const QUADRANT_BUTTONS = [
        { label: "Üst çene", teeth: q => q === 1 || q === 2 || q === 5 || q === 6 },
        { label: "Alt çene", teeth: q => q === 3 || q === 4 || q === 7 || q === 8 },
        { label: "Sağ üst", teeth: q => q === 1 || q === 5 },
        { label: "Sol üst", teeth: q => q === 2 || q === 6 },
        { label: "Sol alt", teeth: q => q === 3 || q === 7 },
        { label: "Sağ alt", teeth: q => q === 4 || q === 8 }
    ];

    function parseCsv(value) {
        return new Set(String(value || "").split(/[,\s;]+/).map(x => x.trim()).filter(x => /^[1-8][1-8]$/.test(x)));
    }

    function toCsv(set) {
        return window.ToothChart.sortTeeth(set).join(",");
    }

    async function init(root) {
        if (root.dataset.initialized === "true") return;
        root.dataset.initialized = "true";

        const stage = root.querySelector("[data-tooth-stage]");
        const input = document.getElementById(root.dataset.inputId);
        const summary = document.getElementById(root.dataset.summaryId);
        const chips = root.querySelector("[data-tooth-chips]");
        const typed = root.querySelector("[data-tooth-typed]");
        const quick = root.querySelector("[data-tooth-quick]");
        const card = root.closest(".tooth-selector-card") || root;
        const dentitionButtons = card.querySelectorAll("[data-dentition]");
        const clearButton = card.querySelector("[data-tooth-clear]");
        const mode = root.dataset.mode || "multiple";
        if (!stage || !input) return;

        const selected = parseCsv(input.value);
        let dentition = [...selected].some(window.ToothChart.isDeciduous) && ![...selected].some(t => !window.ToothChart.isDeciduous(t)) ? "deciduous" : "permanent";

        const chart = new window.ToothChart(stage, {
            svgUrl: root.dataset.svgUrl,
            mode,
            dentition,
            selected,
            onChange: () => sync()
        });

        const sync = () => {
            input.value = toCsv(selected);
            input.dispatchEvent(new Event("change", { bubbles: true }));
            const ordered = window.ToothChart.sortTeeth(selected);

            if (summary) {
                summary.textContent = ordered.length ? `${ordered.length} diş seçili` : "Diş seçilmedi";
            }

            if (chips) {
                chips.innerHTML = "";
                ordered.forEach(number => {
                    const chip = document.createElement("button");
                    chip.type = "button";
                    chip.className = "tooth-chip tooth-chip--removable";
                    chip.title = `${window.ToothChart.toothName(number)} — kaldırmak için tıklayın`;
                    chip.textContent = number;
                    chip.addEventListener("click", () => { selected.delete(number); chart.refresh(); sync(); });
                    chips.appendChild(chip);
                });
            }

            if (typed && document.activeElement !== typed) {
                typed.value = ordered.join(", ");
            }
        };

        // Typed numbers → chart.
        if (typed) {
            const apply = () => {
                const parsed = parseCsv(typed.value);
                selected.clear();
                parsed.forEach(n => selected.add(n));
                chart.refresh();
                sync();
            };
            typed.addEventListener("input", apply);
            typed.addEventListener("blur", () => { apply(); typed.value = window.ToothChart.sortTeeth(selected).join(", "); });
            typed.addEventListener("keydown", e => { if (e.key === "Enter") { e.preventDefault(); typed.blur(); } });
        }

        // Quadrant shortcuts (toggle: if every tooth of the group is selected, deselect them).
        if (quick && mode === "multiple") {
            QUADRANT_BUTTONS.forEach(def => {
                const button = document.createElement("button");
                button.type = "button";
                button.className = "chip";
                button.textContent = def.label;
                button.addEventListener("click", () => {
                    const group = chart.numbers().filter(n => def.teeth(Number(n[0])));
                    const allOn = group.every(n => selected.has(n));
                    group.forEach(n => allOn ? selected.delete(n) : selected.add(n));
                    chart.refresh();
                    sync();
                });
                quick.appendChild(button);
            });
        }

        // Permanent / deciduous switch. Selection is kept; only the visible chart changes.
        dentitionButtons.forEach(button => {
            button.classList.toggle("active", button.dataset.dentition === dentition);
            button.addEventListener("click", async () => {
                dentition = button.dataset.dentition;
                dentitionButtons.forEach(b => b.classList.toggle("active", b === button));
                chart.options.dentition = dentition;
                await chart.render();
                sync();
            });
        });

        clearButton?.addEventListener("click", () => { selected.clear(); chart.refresh(); sync(); });

        try {
            await chart.render();
            sync();
        } catch (err) {
            console.error(err);
            stage.innerHTML = '<div class="tooth-selector-error">Diş şeması yüklenemedi.</div>';
        }
    }

    function initAll() {
        document.querySelectorAll("[data-tooth-selector]").forEach(init);
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", initAll);
    else initAll();
})();

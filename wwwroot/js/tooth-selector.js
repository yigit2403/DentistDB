(() => {
    const FDI_MAP = {
        0: "18", 1: "17", 2: "16", 3: "15", 4: "14", 5: "13", 6: "12", 7: "11",
        8: "21", 9: "22", 10: "23", 11: "24", 12: "25", 13: "26", 14: "27", 15: "28",
        16: "38", 17: "37", 18: "36", 19: "35", 20: "34", 21: "33", 22: "32", 23: "31",
        24: "41", 25: "42", 26: "43", 27: "44", 28: "45", 29: "46", 30: "47", 31: "48"
    };

    function parseCsv(value) {
        return new Set(
            (value || "")
                .split(",")
                .map(x => x.trim())
                .filter(Boolean)
        );
    }

    function setCsv(input, values) {
        input.value = [...values].sort((a, b) => Number(a) - Number(b)).join(",");
    }

    function updateSummary(summaryEl, selectedSet) {
        if (!summaryEl) return;

        if (!selectedSet.size) {
            summaryEl.textContent = "Dis secilmedi";
            return;
        }

        const ordered = [...selectedSet].sort((a, b) => Number(a) - Number(b));
        summaryEl.textContent = `Secilen disler: ${ordered.join(", ")}`;
    }

    function syncVisualState(stage, selectedSet) {
        const buttons = stage.querySelectorAll(".tooth-hotspot");
        const paths = stage.querySelectorAll(".tooth-shape");

        buttons.forEach(btn => {
            const toothNo = btn.dataset.toothNo;
            btn.classList.toggle("is-selected", selectedSet.has(toothNo));
        });

        paths.forEach(path => {
            const toothNo = path.dataset.toothNo;
            path.classList.toggle("is-selected", selectedSet.has(toothNo));
        });
    }

    function toggleTooth(toothNo, mode, selectedSet) {
        if (mode === "single") {
            if (selectedSet.has(toothNo) && selectedSet.size === 1) {
                selectedSet.clear();
            } else {
                selectedSet.clear();
                selectedSet.add(toothNo);
            }
            return;
        }

        if (selectedSet.has(toothNo)) {
            selectedSet.delete(toothNo);
        } else {
            selectedSet.add(toothNo);
        }
    }

    async function loadSvgInto(stage, url) {
        const response = await fetch(url, { cache: "no-cache" });
        if (!response.ok) {
            throw new Error(`SVG load failed: ${response.status}`);
        }

        const markup = await response.text();
        stage.innerHTML = markup;

        const svg = stage.querySelector("svg");
        if (!svg) {
            throw new Error("SVG element not found in loaded file.");
        }

        svg.setAttribute("preserveAspectRatio", "xMidYMid meet");
        return svg;
    }

    function buildHotspots(stage, svg, selectedSet, mode, input, summaryEl) {
        const svgPaths = [...svg.querySelectorAll("path[id]")];

        svgPaths.forEach(path => {
            const rawId = path.getAttribute("id");
            const index = Number(rawId);

            if (!Number.isInteger(index) || !(index in FDI_MAP)) {
                return;
            }

            const toothNo = FDI_MAP[index];
            const bbox = path.getBBox();

            path.classList.add("tooth-shape");
            path.dataset.toothNo = toothNo;
            path.dataset.toothIndex = rawId;

            const btn = document.createElement("button");
            btn.type = "button";
            btn.className = "tooth-hotspot";
            btn.dataset.toothNo = toothNo;
            btn.dataset.toothIndex = rawId;
            btn.textContent = toothNo;

            const left = ((bbox.x + bbox.width / 2) / 450) * 100;
            const top = ((bbox.y + bbox.height / 2) / 750) * 100;

            btn.style.left = `${left}%`;
            btn.style.top = `${top}%`;

            const handleClick = () => {
                toggleTooth(toothNo, mode, selectedSet);
                setCsv(input, selectedSet);
                updateSummary(summaryEl, selectedSet);
                syncVisualState(stage, selectedSet);
            };

            btn.addEventListener("click", handleClick);
            path.addEventListener("click", handleClick);

            stage.appendChild(btn);
        });

        syncVisualState(stage, selectedSet);
        updateSummary(summaryEl, selectedSet);
    }

    async function initSelector(root) {
        const stage = root.querySelector("[data-tooth-stage]");
        const svgUrl = root.dataset.svgUrl;
        const input = document.getElementById(root.dataset.inputId);
        const summaryEl = document.getElementById(root.dataset.summaryId);
        const mode = root.dataset.mode || "multiple";
        const clearBtn = root.closest(".tooth-selector-card")?.querySelector("[data-tooth-clear]");

        if (!stage || !svgUrl || !input) return;

        const selectedSet = parseCsv(input.value);

        try {
            const svg = await loadSvgInto(stage, svgUrl);
            buildHotspots(stage, svg, selectedSet, mode, input, summaryEl);

            clearBtn?.addEventListener("click", () => {
                selectedSet.clear();
                setCsv(input, selectedSet);
                updateSummary(summaryEl, selectedSet);
                syncVisualState(stage, selectedSet);
            });
        } catch (err) {
            console.error(err);
            stage.innerHTML = `<div class="tooth-selector-error">Dis semasi yuklenemedi.</div>`;
        }
    }

    function initAll() {
        document.querySelectorAll("[data-tooth-selector]").forEach(initSelector);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initAll);
    } else {
        initAll();
    }
})();

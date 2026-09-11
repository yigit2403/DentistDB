/*
 * ToothChart – shared FDI tooth chart used by the appointment/treatment tooth selector
 * and by the patient odontogram.
 *
 * Permanent teeth come from /images/teethSelection.svg (anatomical shapes, path ids 0–31).
 * Deciduous teeth (51–85) are generated as rounded shapes on two arches.
 * Numbers are rendered as SVG <text> inside the same coordinate space, so they never drift.
 */
(() => {
    "use strict";

    const SVG_NS = "http://www.w3.org/2000/svg";
    const VIEW_W = 450;
    const VIEW_H = 750;

    // Path id (0..31) in teethSelection.svg → FDI number.
    const FDI_MAP = {
        0: "18", 1: "17", 2: "16", 3: "15", 4: "14", 5: "13", 6: "12", 7: "11",
        8: "21", 9: "22", 10: "23", 11: "24", 12: "25", 13: "26", 14: "27", 15: "28",
        16: "38", 17: "37", 18: "36", 19: "35", 20: "34", 21: "33", 22: "32", 23: "31",
        24: "41", 25: "42", 26: "43", 27: "44", 28: "45", 29: "46", 30: "47", 31: "48"
    };

    const QUADRANTS = {
        1: "Üst sağ", 2: "Üst sol", 3: "Alt sol", 4: "Alt sağ",
        5: "Üst sağ (süt)", 6: "Üst sol (süt)", 7: "Alt sol (süt)", 8: "Alt sağ (süt)"
    };

    const PERMANENT_NAMES = {
        1: "orta kesici", 2: "yan kesici", 3: "kanin", 4: "1. küçük azı",
        5: "2. küçük azı", 6: "1. büyük azı", 7: "2. büyük azı", 8: "3. büyük azı (20 yaş)"
    };

    const DECIDUOUS_NAMES = {
        1: "süt orta kesici", 2: "süt yan kesici", 3: "süt kanin", 4: "1. süt azı", 5: "2. süt azı"
    };

    function toothName(number) {
        const n = String(number);
        if (n.length !== 2) return n;
        const q = Number(n[0]);
        const t = Number(n[1]);
        const names = q >= 5 ? DECIDUOUS_NAMES : PERMANENT_NAMES;
        return `${n} · ${QUADRANTS[q] || ""} ${names[t] || ""}`.trim();
    }

    function isDeciduous(number) {
        const q = Number(String(number)[0]);
        return q >= 5 && q <= 8;
    }

    let svgPromise = null;
    function loadPermanentSvg(url) {
        if (!svgPromise) {
            svgPromise = fetch(url, { cache: "force-cache" })
                .then(r => { if (!r.ok) throw new Error(`SVG load failed: ${r.status}`); return r.text(); })
                .then(markup => new DOMParser().parseFromString(markup, "image/svg+xml").documentElement);
        }
        return svgPromise;
    }

    function el(name, attrs = {}, parent = null) {
        const node = document.createElementNS(SVG_NS, name);
        for (const [key, value] of Object.entries(attrs)) node.setAttribute(key, value);
        if (parent) parent.appendChild(node);
        return node;
    }

    // Deciduous arches: 10 teeth each, laid on a half ellipse.
    function deciduousLayout() {
        const upper = ["55", "54", "53", "52", "51", "61", "62", "63", "64", "65"];
        const lower = ["85", "84", "83", "82", "81", "71", "72", "73", "74", "75"];
        const RX = 178, RY = 245, CX = 225;

        // Sample the half ellipse (angle π → 2π = viewer's left to right; sin ≤ 0 there) so the
        // teeth can be spaced by arc length instead of by angle, which bunches them near the canines.
        const samples = [];
        let length = 0;
        for (let i = 0; i <= 400; i++) {
            const angle = Math.PI * (1 + i / 400);
            const point = { angle, x: RX * Math.cos(angle), y: RY * Math.sin(angle) };
            if (i > 0) length += Math.hypot(point.x - samples[i - 1].x, point.y - samples[i - 1].y);
            point.s = length;
            samples.push(point);
        }
        const pointAt = fraction => samples.find(p => p.s >= fraction * length) || samples[samples.length - 1];

        const teeth = [];
        const place = (list, cy, isUpper) => {
            list.forEach((number, i) => {
                const p = pointAt((i + 0.5) / list.length);
                const molar = i <= 1 || i >= 8;
                // The tooth's long axis follows the ellipse normal so neighbours stay parallel and never overlap.
                const normalDeg = Math.atan2(Math.sin(p.angle) / RY, Math.cos(p.angle) / RX) * 180 / Math.PI;
                const rotate = isUpper ? normalDeg + 90 : -(normalDeg + 90);
                teeth.push({
                    number,
                    x: CX + p.x,
                    y: isUpper ? cy + p.y : cy - p.y,
                    w: molar ? 54 : 46,
                    h: molar ? 62 : 56,
                    rotate
                });
            });
        };
        place(upper, 330, true);
        place(lower, 420, false);
        return teeth;
    }

    class ToothChart {
        /**
         * @param {HTMLElement} stage container that receives the <svg>
         * @param {object} options { svgUrl, mode: 'multiple'|'single'|'view', dentition: 'permanent'|'deciduous',
         *                           selected: Set<string>, statuses: {no: {condition}}, onChange(selectedSet, toothNo), onPick(toothNo) }
         */
        constructor(stage, options) {
            this.stage = stage;
            this.options = Object.assign({ mode: "multiple", dentition: "permanent", selected: new Set(), statuses: {} }, options);
            this.selected = this.options.selected;
            this.svg = null;
            this.teeth = new Map(); // number → { group, shape, label }
            this._drag = null;
        }

        async render() {
            this.stage.innerHTML = "";
            this.teeth.clear();

            const svg = el("svg", { viewBox: `0 0 ${VIEW_W} ${VIEW_H}`, preserveAspectRatio: "xMidYMid meet", role: "img", "aria-label": "Diş şeması", class: "tooth-chart" });
            svg.classList.add(this.options.dentition === "deciduous" ? "is-deciduous" : "is-permanent");

            // Midline + arch labels help orientation.
            el("line", { x1: 225, y1: 40, x2: 225, y2: 710, class: "tc-midline" }, svg);
            const labels = [["Sağ", 60, 380], ["Sol", 390, 380]];
            labels.forEach(([text, x, y]) => { const t = el("text", { x, y, class: "tc-side" }, svg); t.textContent = text; });

            if (this.options.dentition === "deciduous") {
                this._renderDeciduous(svg);
            } else {
                await this._renderPermanent(svg);
            }

            this.stage.appendChild(svg);
            this.svg = svg;
            this._bindDrag();
            this.refresh();
            return this;
        }

        async _renderPermanent(svg) {
            const source = await loadPermanentSvg(this.options.svgUrl);
            const paths = [...source.querySelectorAll("path[id]")];

            // Measure with a detached-but-rendered svg so getBBox works.
            const measure = el("svg", { viewBox: `0 0 ${VIEW_W} ${VIEW_H}`, style: "position:absolute;width:450px;height:750px;opacity:0;pointer-events:none" });
            document.body.appendChild(measure);

            for (const path of paths) {
                const index = Number(path.getAttribute("id"));
                if (!Number.isInteger(index) || !(index in FDI_MAP)) continue;
                const number = FDI_MAP[index];

                const shape = document.importNode(path, true);
                shape.removeAttribute("id");
                shape.removeAttribute("fill");
                shape.setAttribute("class", "tc-shape");
                measure.appendChild(shape);
                const box = shape.getBBox();
                measure.removeChild(shape);

                this._addTooth(svg, number, shape, box.x + box.width / 2, box.y + box.height / 2);
            }

            document.body.removeChild(measure);
        }

        _renderDeciduous(svg) {
            for (const t of deciduousLayout()) {
                const shape = el("rect", {
                    x: (t.x - t.w / 2).toFixed(1), y: (t.y - t.h / 2).toFixed(1), width: t.w, height: t.h, rx: t.w / 2.6,
                    transform: `rotate(${t.rotate.toFixed(1)} ${t.x.toFixed(1)} ${t.y.toFixed(1)})`,
                    class: "tc-shape"
                });
                this._addTooth(svg, t.number, shape, t.x, t.y);
            }
        }

        _addTooth(svg, number, shape, cx, cy) {
            const group = el("g", { class: "tc-tooth", "data-tooth": number, tabindex: "0", role: this.options.mode === "view" ? "img" : "checkbox", "aria-label": toothName(number) }, svg);
            const title = el("title", {}, group);
            title.textContent = toothName(number);
            group.appendChild(shape);
            const label = el("text", { x: cx, y: cy, class: "tc-label", "text-anchor": "middle", "dominant-baseline": "central" }, group);
            label.textContent = number;

            group.addEventListener("keydown", e => {
                if (e.key === " " || e.key === "Enter") { e.preventDefault(); this._pick(number); }
            });

            this.teeth.set(number, { group, shape, label });
        }

        _bindDrag() {
            const svg = this.svg;
            const toothOf = target => target.closest ? target.closest(".tc-tooth") : null;

            svg.addEventListener("pointerdown", e => {
                const g = toothOf(e.target);
                if (!g) return;
                e.preventDefault();
                const number = g.dataset.tooth;
                if (this.options.mode !== "multiple") {
                    this._pick(number);
                    return;
                }
                // Drag paints: first tooth decides whether we are selecting or deselecting.
                const adding = !this.selected.has(number);
                this._drag = { adding, touched: new Set([number]), last: { x: e.clientX, y: e.clientY } };
                this._set(number, adding);
                svg.setPointerCapture?.(e.pointerId);
            });

            svg.addEventListener("pointermove", e => {
                if (!this._drag) return;
                // Sample along the path since the last event so a fast swipe cannot skip a small tooth.
                const from = this._drag.last;
                const to = { x: e.clientX, y: e.clientY };
                this._drag.last = to;
                const steps = Math.max(1, Math.ceil(Math.hypot(to.x - from.x, to.y - from.y) / 6));
                for (let i = 1; i <= steps; i++) {
                    const hit = document.elementFromPoint(from.x + (to.x - from.x) * i / steps, from.y + (to.y - from.y) * i / steps);
                    const g = hit ? toothOf(hit) : null;
                    if (!g) continue;
                    const number = g.dataset.tooth;
                    if (this._drag.touched.has(number)) continue;
                    this._drag.touched.add(number);
                    this._set(number, this._drag.adding);
                }
            });

            const end = () => { if (this._drag) { this._drag = null; this._emit(null); } };
            svg.addEventListener("pointerup", end);
            svg.addEventListener("pointercancel", end);
            svg.addEventListener("lostpointercapture", end);
        }

        _pick(number) {
            if (this.options.mode === "view") {
                this.options.onPick?.(number);
                this.setActive(number);
                return;
            }
            if (this.options.mode === "single") {
                const wasOnly = this.selected.has(number) && this.selected.size === 1;
                this.selected.clear();
                if (!wasOnly) this.selected.add(number);
            } else {
                this._set(number, !this.selected.has(number), true);
                return;
            }
            this.refresh();
            this._emit(number);
        }

        _set(number, on, emit = false) {
            if (on) this.selected.add(number); else this.selected.delete(number);
            this._paint(number);
            if (emit) this._emit(number);
        }

        _emit(number) {
            this.options.onChange?.(this.selected, number);
        }

        _paint(number) {
            const tooth = this.teeth.get(number);
            if (!tooth) return;
            const status = this.options.statuses[number];
            tooth.group.classList.toggle("is-selected", this.selected.has(number));
            tooth.group.setAttribute("aria-checked", this.selected.has(number) ? "true" : "false");
            [...tooth.group.classList].filter(c => c.startsWith("cond-")).forEach(c => tooth.group.classList.remove(c));
            if (status) tooth.group.classList.add(`cond-${status.condition}`);
            const title = tooth.group.querySelector("title");
            if (title) title.textContent = toothName(number) + (status ? ` — ${status.label || status.condition}${status.note ? " · " + status.note : ""}` : "");
        }

        refresh() {
            for (const number of this.teeth.keys()) this._paint(number);
        }

        setActive(number) {
            for (const [n, t] of this.teeth) t.group.classList.toggle("is-active", n === number);
        }

        setSelected(values) {
            this.selected.clear();
            values.forEach(v => this.selected.add(String(v)));
            this.refresh();
        }

        numbers() { return [...this.teeth.keys()]; }
    }

    window.ToothChart = ToothChart;
    window.ToothChart.toothName = toothName;
    window.ToothChart.isDeciduous = isDeciduous;
    window.ToothChart.sortTeeth = list => [...list].sort((a, b) => Number(a) - Number(b));
})();

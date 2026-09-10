/* ==========================================================================
   Scan viewer – zoom / pan / rotate / brightness-contrast / negative and
   side-by-side compare for X-ray images. Dependency-free.

   Markup contract (see Views/Scans/View.cshtml):
     [data-scan-viewer]                 root
       [data-sv-action="..."]           toolbar buttons
       [data-sv-zoom]                   zoom % of the active pane
       [data-sv-filter="brightness|contrast"] range inputs
       [data-sv-filter-val="..."]       value labels for the sliders
       [data-sv-compare]                <select> of sibling images (option[data-src])
       [data-sv-compare-bar]            footer bar shown in compare mode
       [data-sv-sync]                   checkbox: mirror zoom/pan between panes
       [data-sv-pane] x2                primary + secondary pane
         [data-sv-pane-title] [data-sv-pane-zoom]
         [data-sv-stage] > [data-sv-img] + [data-sv-status]

   Transform model: the <img> is rendered at its natural size, anchored with
   left/top 50% and transform-origin at its centre. State is
   { scale, tx, ty, rotation, flip } where (tx, ty) is the offset of the image
   centre from the stage centre, in stage pixels. All operations are expressed
   as deltas (zoomBy, panBy, rotateBy...) so "Senkron" can replay the same
   operation on the other pane.
   ========================================================================== */
(function () {
  "use strict";

  var root = document.querySelector("[data-scan-viewer]");
  if (!root) return;

  var ZOOM_STEP = 1.25;      // toolbar / keyboard zoom factor
  var PAN_STEP = 60;         // keyboard pan, px
  var FIT_PADDING = 24;      // breathing room around a fitted image, px
  var EDGE_KEEP = 48;        // px of image that must stay inside the stage when panning
  var WHEEL_SENSITIVITY = 0.0025;

  function clamp(value, min, max) { return value < min ? min : value > max ? max : value; }
  function normalizeDegrees(deg) { deg %= 360; return deg < 0 ? deg + 360 : deg; }
  function defaultState() {
    return { scale: 1, tx: 0, ty: 0, rotation: 0, flip: false, brightness: 100, contrast: 100, invert: false };
  }

  /* ------------------------------------------------------------------------
     Pane: one image on one stage
     ------------------------------------------------------------------------ */
  function Pane(el, viewer) {
    this.el = el;
    this.viewer = viewer;
    this.stage = el.querySelector("[data-sv-stage]");
    this.img = el.querySelector("[data-sv-img]");
    this.status = el.querySelector("[data-sv-status]");
    this.titleEl = el.querySelector("[data-sv-pane-title]");
    this.zoomLabel = el.querySelector("[data-sv-pane-zoom]");

    this.ready = false;
    this.fitted = true;          // true until the user zooms/pans; a fitted pane re-fits on resize
    this.state = defaultState();
    this.frame = 0;
    this.pointers = new Map();
    this.pinch = null;

    this.bind();
  }

  Pane.prototype.bind = function () {
    var self = this;
    var stage = this.stage;

    this.img.addEventListener("load", function () { self.onLoad(); });
    this.img.addEventListener("error", function () { self.onError(); });
    // The primary image starts loading before this script runs; it may already be done.
    if (this.img.getAttribute("src") && this.img.complete) {
      if (this.img.naturalWidth > 0) this.onLoad(); else this.onError();
    }

    stage.addEventListener("pointerdown", function (e) { self.onPointerDown(e); });
    stage.addEventListener("pointermove", function (e) { self.onPointerMove(e); });
    stage.addEventListener("pointerup", function (e) { self.onPointerUp(e); });
    stage.addEventListener("pointercancel", function (e) { self.onPointerUp(e); });
    stage.addEventListener("wheel", function (e) { self.onWheel(e); }, { passive: false });
    stage.addEventListener("dblclick", function (e) { self.onDoubleClick(e); });
    stage.addEventListener("dragstart", function (e) { e.preventDefault(); });
  };

  /* Loading ---------------------------------------------------------------- */
  Pane.prototype.setStatus = function (text) {
    if (this.status) this.status.textContent = text;
  };

  Pane.prototype.load = function (src, title) {
    this.ready = false;
    this.fitted = true;
    this.el.classList.remove("is-ready", "is-error");
    this.setStatus("Yükleniyor…");
    if (this.titleEl) this.titleEl.textContent = title || "";
    this.img.alt = title || "";
    this.img.style.width = "";
    this.img.style.height = "";
    this.img.style.transform = "";
    this.img.src = src; // "load" fires even for cached images once src is (re)assigned
  };

  Pane.prototype.unload = function () {
    this.ready = false;
    this.fitted = true;
    this.pointers.clear();
    this.pinch = null;
    this.el.classList.remove("is-ready", "is-error", "is-dragging");
    this.img.removeAttribute("src");
    this.img.style.transform = "";
    this.state = defaultState();
    this.setStatus("Yükleniyor…");
  };

  Pane.prototype.onLoad = function () {
    if (!this.img.naturalWidth) { this.onError(); return; }
    this.img.style.width = this.img.naturalWidth + "px";
    this.img.style.height = this.img.naturalHeight + "px";
    this.ready = true;
    this.el.classList.remove("is-error");
    this.el.classList.add("is-ready");
    if (this.fitted) this.fit(); else { this.clampPan(); this.render(); }
    this.viewer.onPaneReady(this);
  };

  Pane.prototype.onError = function () {
    if (!this.img.getAttribute("src")) return;
    this.ready = false;
    this.el.classList.remove("is-ready");
    this.el.classList.add("is-error");
    this.setStatus("Görüntü yüklenemedi.");
  };

  /* Geometry --------------------------------------------------------------- */
  Pane.prototype.stageSize = function () {
    return { w: this.stage.clientWidth, h: this.stage.clientHeight };
  };

  // Natural image size, swapped when rotated by 90/270 degrees.
  Pane.prototype.imageSize = function () {
    var w = this.img.naturalWidth || 1;
    var h = this.img.naturalHeight || 1;
    return this.state.rotation % 180 === 0 ? { w: w, h: h } : { w: h, h: w };
  };

  Pane.prototype.fitScale = function () {
    var s = this.stageSize();
    var i = this.imageSize();
    if (s.w <= 0 || s.h <= 0) return 1;
    return Math.max(0.01, Math.min((s.w - FIT_PADDING) / i.w, (s.h - FIT_PADDING) / i.h));
  };

  Pane.prototype.scaleBounds = function () {
    var fit = this.fitScale();
    return { min: Math.min(fit * 0.25, 1), max: Math.max(fit * 40, 8) };
  };

  Pane.prototype.isAtFit = function () {
    return this.fitted || Math.abs(this.state.scale - this.fitScale()) < 0.001;
  };

  // Client coordinates -> offset from the stage centre.
  Pane.prototype.toStagePoint = function (clientX, clientY) {
    var rect = this.stage.getBoundingClientRect();
    return { x: clientX - rect.left - rect.width / 2, y: clientY - rect.top - rect.height / 2 };
  };

  // Keep at least EDGE_KEEP px of the image inside the stage on every side.
  Pane.prototype.clampPan = function () {
    var s = this.stageSize();
    var i = this.imageSize();
    var halfW = i.w * this.state.scale / 2;
    var halfH = i.h * this.state.scale / 2;
    var limX = Math.max(0, s.w / 2 + halfW - Math.min(EDGE_KEEP, halfW));
    var limY = Math.max(0, s.h / 2 + halfH - Math.min(EDGE_KEEP, halfH));
    this.state.tx = clamp(this.state.tx, -limX, limX);
    this.state.ty = clamp(this.state.ty, -limY, limY);
  };

  /* Operations (all usable as mirrored deltas) ----------------------------- */
  Pane.prototype.fit = function () {
    if (!this.ready) return;
    this.state.scale = this.fitScale();
    this.state.tx = 0;
    this.state.ty = 0;
    this.fitted = true;
    this.render();
  };

  // Zoom to an absolute scale keeping the stage point (cx, cy) fixed.
  Pane.prototype.zoomTo = function (scale, cx, cy) {
    if (!this.ready) return;
    var bounds = this.scaleBounds();
    var next = clamp(scale, bounds.min, bounds.max);
    var factor = next / this.state.scale;
    cx = cx || 0;
    cy = cy || 0;
    this.state.tx = factor * this.state.tx + (1 - factor) * cx;
    this.state.ty = factor * this.state.ty + (1 - factor) * cy;
    this.state.scale = next;
    this.fitted = false;
    this.clampPan();
    this.render();
  };

  Pane.prototype.zoomBy = function (factor, cx, cy) {
    this.zoomTo(this.state.scale * factor, cx, cy);
  };

  Pane.prototype.panBy = function (dx, dy) {
    if (!this.ready) return;
    this.state.tx += dx;
    this.state.ty += dy;
    this.fitted = false;
    this.clampPan();
    this.render();
  };

  Pane.prototype.rotateBy = function (deg) {
    this.state.rotation = normalizeDegrees(this.state.rotation + deg);
    if (!this.ready) return;
    if (this.fitted) this.fit(); else { this.clampPan(); this.render(); }
  };

  Pane.prototype.toggleFlip = function () {
    this.state.flip = !this.state.flip;
    this.render();
  };

  Pane.prototype.setFilter = function (key, value) {
    this.state[key] = value;
    this.render();
  };

  Pane.prototype.reset = function () {
    this.state = defaultState();
    this.fitted = true;
    if (this.ready) this.fit(); else this.render();
  };

  // Used when "Senkron" is switched on: align this pane with the other one.
  Pane.prototype.copyTransformFrom = function (other) {
    this.state.rotation = other.state.rotation;
    this.state.flip = other.state.flip;
    this.state.scale = other.state.scale;
    this.state.tx = other.state.tx;
    this.state.ty = other.state.ty;
    this.fitted = other.fitted;
    if (!this.ready) return;
    if (this.fitted) this.fit(); else { this.clampPan(); this.render(); }
  };

  Pane.prototype.onResize = function () {
    if (!this.ready) return;
    if (this.fitted) this.fit(); else { this.clampPan(); this.render(); }
  };

  /* Rendering -------------------------------------------------------------- */
  Pane.prototype.render = function () {
    if (this.frame) return;
    var self = this;
    this.frame = window.requestAnimationFrame(function () {
      self.frame = 0;
      self.paint();
    });
  };

  Pane.prototype.paint = function () {
    var st = this.state;
    var w = this.img.naturalWidth || 0;
    var h = this.img.naturalHeight || 0;
    this.img.style.transform =
      "translate(" + (st.tx - w / 2) + "px, " + (st.ty - h / 2) + "px) " +
      "scale(" + st.scale + ") rotate(" + st.rotation + "deg) scaleX(" + (st.flip ? -1 : 1) + ")";
    this.img.style.filter =
      "brightness(" + st.brightness + "%) contrast(" + st.contrast + "%)" + (st.invert ? " invert(1)" : "");
    var percent = Math.round(st.scale * 100) + "%";
    if (this.zoomLabel) this.zoomLabel.textContent = percent;
    this.viewer.onPainted(this, percent);
  };

  /* Pointer input: drag to pan, two fingers to pinch-zoom ------------------- */
  Pane.prototype.measurePinch = function () {
    var pts = Array.from(this.pointers.values());
    var dx = pts[1].x - pts[0].x;
    var dy = pts[1].y - pts[0].y;
    return { dist: Math.sqrt(dx * dx + dy * dy), mid: { x: (pts[0].x + pts[1].x) / 2, y: (pts[0].y + pts[1].y) / 2 } };
  };

  Pane.prototype.onPointerDown = function (e) {
    if (!this.ready) return;
    if (e.pointerType === "mouse" && e.button !== 0) return;
    e.preventDefault();
    this.viewer.setActive(this);
    try { this.stage.focus({ preventScroll: true }); } catch (err) { this.stage.focus(); }
    try { this.stage.setPointerCapture(e.pointerId); } catch (err) { /* not supported: drag still works while inside */ }
    this.pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
    this.el.classList.add("is-dragging");
    this.pinch = this.pointers.size === 2 ? this.measurePinch() : null;
  };

  Pane.prototype.onPointerMove = function (e) {
    var prev = this.pointers.get(e.pointerId);
    if (!prev) return;
    var cur = { x: e.clientX, y: e.clientY };
    this.pointers.set(e.pointerId, cur);
    e.preventDefault();

    if (this.pointers.size === 1) {
      var dx = cur.x - prev.x;
      var dy = cur.y - prev.y;
      this.viewer.run(this, function (p) { p.panBy(dx, dy); });
    } else if (this.pointers.size === 2 && this.pinch) {
      var next = this.measurePinch();
      var factor = this.pinch.dist > 0 ? next.dist / this.pinch.dist : 1;
      var focus = this.toStagePoint(next.mid.x, next.mid.y);
      var mx = next.mid.x - this.pinch.mid.x;
      var my = next.mid.y - this.pinch.mid.y;
      this.viewer.run(this, function (p) { p.zoomBy(factor, focus.x, focus.y); p.panBy(mx, my); });
      this.pinch = next;
    }
  };

  Pane.prototype.onPointerUp = function (e) {
    if (!this.pointers.has(e.pointerId)) return;
    this.pointers.delete(e.pointerId);
    try { this.stage.releasePointerCapture(e.pointerId); } catch (err) { /* ignore */ }
    if (this.pointers.size < 2) this.pinch = null;
    if (this.pointers.size === 0) this.el.classList.remove("is-dragging");
  };

  Pane.prototype.onWheel = function (e) {
    if (!this.ready) return;
    e.preventDefault();
    var dy = e.deltaY;
    if (e.deltaMode === 1) dy *= 16; else if (e.deltaMode === 2) dy *= 120;
    dy = clamp(dy, -120, 120);
    var factor = Math.exp(-dy * WHEEL_SENSITIVITY);
    var focus = this.toStagePoint(e.clientX, e.clientY);
    this.viewer.setActive(this);
    this.viewer.run(this, function (p) { p.zoomBy(factor, focus.x, focus.y); });
  };

  Pane.prototype.onDoubleClick = function (e) {
    if (!this.ready) return;
    e.preventDefault();
    var focus = this.toStagePoint(e.clientX, e.clientY);
    var atFit = this.isAtFit();
    this.viewer.setActive(this);
    this.viewer.run(this, function (p) {
      if (atFit) p.zoomTo(1, focus.x, focus.y); else p.fit();
    });
  };

  /* ------------------------------------------------------------------------
     Viewer: toolbar, compare mode, keyboard
     ------------------------------------------------------------------------ */
  function Viewer(rootEl) {
    this.root = rootEl;
    this.hover = false;
    this.compareOpen = false;

    this.zoomLabel = rootEl.querySelector("[data-sv-zoom]");
    this.compareSelect = rootEl.querySelector("[data-sv-compare]");
    this.compareBar = rootEl.querySelector("[data-sv-compare-bar]");
    this.syncBox = rootEl.querySelector("[data-sv-sync]");
    this.invertButton = rootEl.querySelector('[data-sv-action="invert"]');
    this.filterInputs = Array.prototype.slice.call(rootEl.querySelectorAll("[data-sv-filter]"));

    var paneEls = rootEl.querySelectorAll("[data-sv-pane]");
    this.primary = new Pane(paneEls[0], this);
    this.secondary = paneEls[1] ? new Pane(paneEls[1], this) : null;
    this.active = this.primary;
    this.primary.el.classList.add("is-active");

    this.bind();
    this.syncControls();
  }

  Viewer.prototype.panes = function () {
    return this.secondary ? [this.primary, this.secondary] : [this.primary];
  };

  Viewer.prototype.other = function (pane) {
    if (!this.secondary) return null;
    return pane === this.primary ? this.secondary : this.primary;
  };

  Viewer.prototype.syncEnabled = function () {
    return !!(this.syncBox && this.syncBox.checked);
  };

  // Run an operation on a pane and, in synced compare mode, mirror it on the other pane.
  Viewer.prototype.run = function (pane, op) {
    op(pane);
    if (!this.compareOpen || !this.syncEnabled()) return;
    var other = this.other(pane);
    if (other && other.ready) op(other);
  };

  Viewer.prototype.bind = function () {
    var self = this;
    var rootEl = this.root;

    rootEl.addEventListener("click", function (e) {
      var button = e.target.closest("[data-sv-action]");
      if (!button || !rootEl.contains(button)) return;
      self.action(button.getAttribute("data-sv-action"));
    });

    this.filterInputs.forEach(function (input) {
      input.addEventListener("input", function () {
        var key = input.getAttribute("data-sv-filter");
        var value = Number(input.value);
        self.active.setFilter(key, value);
        self.updateFilterLabel(key, value);
      });
    });

    if (this.compareSelect) {
      this.compareSelect.addEventListener("change", function () {
        var option = self.compareSelect.options[self.compareSelect.selectedIndex];
        if (option && option.value) self.openCompare(option); else self.closeCompare();
      });
    }

    if (this.syncBox) {
      this.syncBox.addEventListener("change", function () {
        if (!self.syncBox.checked || !self.compareOpen) return;
        var other = self.other(self.active);
        if (other) other.copyTransformFrom(self.active);
      });
    }

    rootEl.addEventListener("pointerenter", function () { self.hover = true; });
    rootEl.addEventListener("pointerleave", function () { self.hover = false; });

    document.addEventListener("keydown", function (e) { self.onKey(e); });

    if (window.ResizeObserver) {
      var observer = new ResizeObserver(function () {
        self.panes().forEach(function (p) { p.onResize(); });
      });
      this.panes().forEach(function (p) { observer.observe(p.stage); });
    } else {
      window.addEventListener("resize", function () {
        self.panes().forEach(function (p) { p.onResize(); });
      });
    }
  };

  Viewer.prototype.action = function (name) {
    var active = this.active;
    switch (name) {
      case "zoom-in": this.run(active, function (p) { p.zoomBy(ZOOM_STEP); }); break;
      case "zoom-out": this.run(active, function (p) { p.zoomBy(1 / ZOOM_STEP); }); break;
      case "fit": this.run(active, function (p) { p.fit(); }); break;
      case "actual": this.run(active, function (p) { p.zoomTo(1); }); break;
      case "rotate-left": this.run(active, function (p) { p.rotateBy(-90); }); break;
      case "rotate-right": this.run(active, function (p) { p.rotateBy(90); }); break;
      case "flip": this.run(active, function (p) { p.toggleFlip(); }); break;
      case "invert":
        active.setFilter("invert", !active.state.invert);
        this.syncControls();
        break;
      case "reset":
        this.panes().forEach(function (p) { p.reset(); });
        this.syncControls();
        break;
      case "compare-close": this.closeCompare(); break;
      default: break;
    }
  };

  Viewer.prototype.setActive = function (pane) {
    if (!pane || pane === this.active) return;
    this.active.el.classList.remove("is-active");
    pane.el.classList.add("is-active");
    this.active = pane;
    this.syncControls();
  };

  // Reflect the active pane's state in the toolbar (sliders, negative toggle, zoom %).
  Viewer.prototype.syncControls = function () {
    var self = this;
    var st = this.active.state;
    this.filterInputs.forEach(function (input) {
      var key = input.getAttribute("data-sv-filter");
      if (st[key] === undefined) return;
      input.value = String(st[key]);
      self.updateFilterLabel(key, st[key]);
    });
    if (this.invertButton) {
      this.invertButton.classList.toggle("is-on", !!st.invert);
      this.invertButton.setAttribute("aria-pressed", st.invert ? "true" : "false");
    }
    if (this.zoomLabel) this.zoomLabel.textContent = Math.round(st.scale * 100) + "%";
  };

  Viewer.prototype.updateFilterLabel = function (key, value) {
    var label = this.root.querySelector('[data-sv-filter-val="' + key + '"]');
    if (label) label.textContent = Math.round(value) + "%";
  };

  Viewer.prototype.onPainted = function (pane, percent) {
    if (pane === this.active && this.zoomLabel) this.zoomLabel.textContent = percent;
  };

  Viewer.prototype.onPaneReady = function (pane) {
    if (pane === this.active) this.syncControls();
  };

  /* Compare mode ------------------------------------------------------------ */
  Viewer.prototype.openCompare = function (option) {
    if (!this.secondary) return;
    var src = option.getAttribute("data-src");
    if (!src) return;
    this.compareOpen = true;
    this.root.classList.add("is-compare");
    this.secondary.el.hidden = false;
    if (this.compareBar) this.compareBar.hidden = false;
    this.secondary.load(src, option.textContent.trim());
    if (!window.ResizeObserver) this.primary.onResize();
  };

  Viewer.prototype.closeCompare = function () {
    if (this.compareSelect) this.compareSelect.value = "";
    if (!this.compareOpen || !this.secondary) return;
    this.compareOpen = false;
    this.root.classList.remove("is-compare");
    this.secondary.unload();
    this.secondary.el.hidden = true;
    if (this.compareBar) this.compareBar.hidden = true;
    this.setActive(this.primary);
    if (!window.ResizeObserver) this.primary.onResize();
  };

  /* Keyboard ------------------------------------------------------------------ */
  Viewer.prototype.onKey = function (e) {
    if (e.defaultPrevented || e.ctrlKey || e.metaKey || e.altKey) return;
    var target = e.target;
    var tag = target && target.tagName ? target.tagName.toLowerCase() : "";
    if (tag === "input" || tag === "textarea" || tag === "select" || (target && target.isContentEditable)) return;

    var self = this;
    var active = this.active;
    // Arrow keys only act while the viewer is focused or hovered so the page can still scroll.
    var engaged = this.hover || this.root.contains(document.activeElement);

    switch (e.key) {
      case "+": case "=": this.run(active, function (p) { p.zoomBy(ZOOM_STEP); }); break;
      case "-": case "_": this.run(active, function (p) { p.zoomBy(1 / ZOOM_STEP); }); break;
      case "0": this.run(active, function (p) { p.fit(); }); break;
      case "r": this.run(active, function (p) { p.rotateBy(90); }); break;
      case "R": this.run(active, function (p) { p.rotateBy(-90); }); break;
      case "ArrowLeft": if (!engaged) return; this.run(active, function (p) { p.panBy(PAN_STEP, 0); }); break;
      case "ArrowRight": if (!engaged) return; this.run(active, function (p) { p.panBy(-PAN_STEP, 0); }); break;
      case "ArrowUp": if (!engaged) return; this.run(active, function (p) { p.panBy(0, PAN_STEP); }); break;
      case "ArrowDown": if (!engaged) return; this.run(active, function (p) { p.panBy(0, -PAN_STEP); }); break;
      case "Escape":
        if (!this.compareOpen) return;
        self.closeCompare();
        break;
      default: return;
    }
    e.preventDefault();
  };

  new Viewer(root);
})();

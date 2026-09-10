(function () {
  "use strict";

  /* ---------------------------------------------------------------------
     jQuery Validation: Turkish messages + Turkish number format support
     --------------------------------------------------------------------- */
  if (window.jQuery && jQuery.validator) {
    var v = jQuery.validator;

    jQuery.extend(v.messages, {
      required: "Bu alan zorunludur.",
      remote: "Lütfen bu alanı düzeltin.",
      email: "Geçerli bir e-posta adresi girin.",
      url: "Geçerli bir adres girin.",
      date: "Geçerli bir tarih girin.",
      dateISO: "Geçerli bir tarih girin.",
      number: "Geçerli bir sayı girin.",
      digits: "Yalnızca rakam girin.",
      equalTo: "Değerler eşleşmiyor.",
      maxlength: v.format("En fazla {0} karakter girin."),
      minlength: v.format("En az {0} karakter girin."),
      rangelength: v.format("{0} ile {1} karakter arasında bir değer girin."),
      range: v.format("{0} ile {1} arasında bir değer girin."),
      max: v.format("En fazla {0} olabilir."),
      min: v.format("En az {0} olmalıdır.")
    });

    // Accept "1.250,50", "1250,50" and "1250.50" for numeric fields.
    var parseLocalized = function (value) {
      if (typeof value !== "string") return value;
      var text = value.trim().replace(/\s/g, "");
      if (!text) return NaN;
      var lastComma = text.lastIndexOf(",");
      var lastDot = text.lastIndexOf(".");
      if (lastComma >= 0 && lastDot >= 0) {
        text = lastComma > lastDot ? text.replace(/\./g, "").replace(",", ".") : text.replace(/,/g, "");
      } else if (lastComma >= 0) {
        text = (text.split(",").length - 1) === 1 ? text.replace(",", ".") : text.replace(/,/g, "");
      } else if (lastDot >= 0 && ((text.split(".").length - 1) > 1 || text.length - lastDot - 1 === 3)) {
        text = text.replace(/\./g, "");
      }
      return /^-?\d+(\.\d+)?$/.test(text) ? parseFloat(text) : NaN;
    };

    v.methods.number = function (value, element) {
      return this.optional(element) || !isNaN(parseLocalized(value));
    };

    v.methods.range = function (value, element, param) {
      var parsed = parseLocalized(value);
      return this.optional(element) || (!isNaN(parsed) && parsed >= param[0] && parsed <= param[1]);
    };

    v.methods.min = function (value, element, param) {
      var parsed = parseLocalized(value);
      return this.optional(element) || (!isNaN(parsed) && parsed >= param);
    };

    v.methods.max = function (value, element, param) {
      var parsed = parseLocalized(value);
      return this.optional(element) || (!isNaN(parsed) && parsed <= param);
    };

    window.DentistDB = window.DentistDB || {};
    window.DentistDB.parseNumber = parseLocalized;
  }

  window.DentistDB = window.DentistDB || {};
  window.DentistDB.formatMoney = function (value) {
    try {
      return new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY", minimumFractionDigits: 2 }).format(value || 0);
    } catch (e) {
      return (value || 0).toFixed(2) + " ₺";
    }
  };

  /* ---------------------------------------------------------------------
     Sidebar (mobile)
     --------------------------------------------------------------------- */
  document.addEventListener("click", function (event) {
    if (event.target.closest("[data-sidebar-toggle]")) {
      document.body.classList.toggle("sidebar-open");
      return;
    }
    if (event.target.closest("[data-sidebar-close]")) {
      document.body.classList.remove("sidebar-open");
    }
  });

  /* ---------------------------------------------------------------------
     Generic helpers: quick-fill chips, native picker buttons, confirm forms
     --------------------------------------------------------------------- */
  document.addEventListener("click", function (event) {
    var quickFill = event.target.closest(".js-quick-fill");
    if (quickFill) {
      var target = document.getElementById(quickFill.dataset.targetId);
      if (target) {
        target.value = quickFill.dataset.value || "";
        target.dispatchEvent(new Event("input", { bubbles: true }));
        target.dispatchEvent(new Event("change", { bubbles: true }));
        target.focus();
      }
    }

    var pickerButton = event.target.closest(".js-picker-button");
    if (pickerButton) {
      var targetId = pickerButton.dataset.pickerTarget;
      var input = targetId ? document.getElementById(targetId) : pickerButton.previousElementSibling;
      if (input && typeof input.showPicker === "function") {
        try { input.showPicker(); } catch (e) { input.focus(); }
      } else if (input) {
        input.focus();
      }
    }
  });

  document.addEventListener("submit", function (event) {
    var form = event.target;
    if (form.matches("form[data-confirm]")) {
      if (!window.confirm(form.dataset.confirm)) {
        event.preventDefault();
      }
    }
  });

  // Auto-submit filter forms when a select/checkbox marked with data-autosubmit changes.
  document.addEventListener("change", function (event) {
    var el = event.target;
    if (el.matches("[data-autosubmit]") && el.form) {
      el.form.submit();
    }
  });

  /* ---------------------------------------------------------------------
     Topbar quick patient search
     --------------------------------------------------------------------- */
  var quickSearch = document.querySelector("[data-quick-search]");
  if (quickSearch) {
    var input = quickSearch.querySelector("input");
    var results = quickSearch.querySelector(".quick-search-results");
    var searchUrl = quickSearch.dataset.searchUrl;
    var patientUrlTemplate = quickSearch.dataset.patientUrl;
    var timer = null;
    var activeIndex = -1;
    var currentItems = [];

    var escapeHtml = function (text) {
      return String(text == null ? "" : text).replace(/[&<>"']/g, function (c) {
        return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
      });
    };

    var hide = function () {
      results.hidden = true;
      results.innerHTML = "";
      activeIndex = -1;
      currentItems = [];
    };

    var render = function (items) {
      currentItems = items;
      activeIndex = -1;
      if (!items.length) {
        results.innerHTML = '<div class="qs-empty">Eşleşen hasta bulunamadı.</div>';
        results.hidden = false;
        return;
      }
      results.innerHTML = items.map(function (item) {
        var href = patientUrlTemplate.replace("__ID__", item.id);
        var initials = (item.name || "?").split(" ").filter(Boolean).slice(0, 2).map(function (p) { return p[0].toLocaleUpperCase("tr-TR"); }).join("");
        return '<a href="' + href + '">' +
          '<span class="avatar" style="width:28px;height:28px;font-size:.7rem">' + escapeHtml(initials) + '</span>' +
          '<span class="truncate">' + escapeHtml(item.name) + (item.alerts ? ' <span class="badge-soft danger" style="margin-left:4px">Uyarı</span>' : "") + '</span>' +
          '<span class="qs-meta">' + escapeHtml(item.phone || item.tckn || "") + '</span>' +
          '</a>';
      }).join("");
      results.hidden = false;
    };

    var search = function () {
      var term = input.value.trim();
      if (term.length < 2) { hide(); return; }
      fetch(searchUrl + "?q=" + encodeURIComponent(term), { headers: { "Accept": "application/json" } })
        .then(function (r) { return r.ok ? r.json() : []; })
        .then(render)
        .catch(hide);
    };

    input.addEventListener("input", function () {
      window.clearTimeout(timer);
      timer = window.setTimeout(search, 180);
    });

    input.addEventListener("keydown", function (event) {
      var links = results.querySelectorAll("a");
      if (event.key === "ArrowDown" && links.length) {
        event.preventDefault();
        activeIndex = Math.min(activeIndex + 1, links.length - 1);
      } else if (event.key === "ArrowUp" && links.length) {
        event.preventDefault();
        activeIndex = Math.max(activeIndex - 1, 0);
      } else if (event.key === "Enter") {
        if (activeIndex >= 0 && links[activeIndex]) {
          event.preventDefault();
          window.location.href = links[activeIndex].href;
        } else if (input.value.trim().length) {
          event.preventDefault();
          window.location.href = patientUrlTemplate.replace(/\/Details\/__ID__.*$/, "") + "?search=" + encodeURIComponent(input.value.trim());
        }
        return;
      } else if (event.key === "Escape") {
        hide();
        return;
      } else {
        return;
      }
      links.forEach(function (link, index) { link.classList.toggle("is-active", index === activeIndex); });
    });

    document.addEventListener("click", function (event) {
      if (!quickSearch.contains(event.target)) hide();
    });

    // Ctrl+K / "/" focuses the search box.
    document.addEventListener("keydown", function (event) {
      var tag = (document.activeElement && document.activeElement.tagName || "").toLowerCase();
      var typing = tag === "input" || tag === "textarea" || tag === "select";
      if ((event.ctrlKey && event.key.toLowerCase() === "k") || (!typing && event.key === "/")) {
        event.preventDefault();
        input.focus();
        input.select();
      }
    });
  }

  /* ---------------------------------------------------------------------
     Autofocus first invalid field after a server-side validation failure
     --------------------------------------------------------------------- */
  var firstInvalid = document.querySelector(".input-validation-error");
  if (firstInvalid && typeof firstInvalid.focus === "function") {
    firstInvalid.focus();
  }
})();

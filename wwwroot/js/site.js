(function () {
  const turkishMessages = {
    required: "Bu alan zorunludur.",
    remote: "Lütfen bu alanı düzeltin.",
    email: "Lütfen geçerli bir e-posta adresi girin.",
    url: "Lütfen geçerli bir adres girin.",
    date: "Lütfen geçerli bir tarih girin.",
    dateISO: "Lütfen geçerli bir tarih girin.",
    number: "Lütfen geçerli bir sayı girin.",
    digits: "Lütfen yalnızca rakam girin.",
    equalTo: "Lütfen aynı değeri tekrar girin.",
    maxlength: $.validator ? $.validator.format("Lütfen en fazla {0} karakter girin.") : undefined,
    minlength: $.validator ? $.validator.format("Lütfen en az {0} karakter girin.") : undefined,
    rangelength: $.validator ? $.validator.format("Lütfen {0} ile {1} karakter uzunluğunda bir değer girin.") : undefined,
    range: $.validator ? $.validator.format("Lütfen {0} ile {1} arasında bir değer girin.") : undefined,
    max: $.validator ? $.validator.format("Lütfen {0} değerinden küçük veya eşit bir değer girin.") : undefined,
    min: $.validator ? $.validator.format("Lütfen {0} değerinden büyük veya eşit bir değer girin.") : undefined
  };

  if ($.validator) {
    $.extend($.validator.messages, turkishMessages);
  }

  document.addEventListener("click", function (event) {
    const quickFillButton = event.target.closest(".js-quick-fill");
    if (quickFillButton) {
      const target = document.getElementById(quickFillButton.dataset.targetId);
      if (target) {
        target.value = quickFillButton.dataset.value || "";
        target.dispatchEvent(new Event("change", { bubbles: true }));
      }
    }

    const pickerButton = event.target.closest(".js-picker-button");
    if (pickerButton) {
      const targetId = pickerButton.dataset.pickerTarget;
      const input = targetId ? document.getElementById(targetId) : pickerButton.previousElementSibling;
      if (input && typeof input.showPicker === "function") {
        input.showPicker();
      } else if (input) {
        input.focus();
      }
    }

    const clearTeethButton = event.target.closest(".js-clear-teeth");
    if (clearTeethButton) {
      const selector = clearTeethButton.closest(".tooth-selector");
      if (selector) {
        selector.querySelectorAll(".tooth-check").forEach(function (checkbox) {
          checkbox.checked = false;
        });
      }
    }
  });
})();

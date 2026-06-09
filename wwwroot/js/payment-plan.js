(() => {
  const toggle = document.querySelector(".payment-plan-toggle");
  const fields = Array.from(document.querySelectorAll(".payment-plan-field"));

  if (!toggle || fields.length === 0) {
    return;
  }

  const syncPaymentPlanFields = () => {
    const isEnabled = toggle.checked;

    fields.forEach((field) => {
      field.classList.toggle("d-none", !isEnabled);
      field.querySelectorAll("input, select, textarea").forEach((input) => {
        input.disabled = !isEnabled;
      });
    });
  };

  toggle.addEventListener("change", syncPaymentPlanFields);
  syncPaymentPlanFields();
})();

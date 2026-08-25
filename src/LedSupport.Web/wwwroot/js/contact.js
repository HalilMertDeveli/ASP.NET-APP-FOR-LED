(() => {
  const form = document.getElementById("support-form");
  if (!form) return;

  const btn = form.querySelector("[data-submit-btn]");

  form.addEventListener("submit", (event) => {
    if (!form.checkValidity()) {
      event.preventDefault();
      event.stopPropagation();
      form.classList.add("was-validated");

      const firstInvalid = form.querySelector(":invalid");
      if (firstInvalid instanceof HTMLElement) {
        firstInvalid.focus();
      }
      return;
    }

    if (btn) {
      btn.setAttribute("disabled", "disabled");
      btn.textContent = "Gönderiliyor…";
    }
  });
})();

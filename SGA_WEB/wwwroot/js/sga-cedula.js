(function () {
  const cedulaInput = document.getElementById("cedula-input-recuperar");
  if (!cedulaInput) {
    return;
  }

  cedulaInput.addEventListener("input", function () {
    const digits = (cedulaInput.value || "").replace(/\D/g, "").substring(0, 9);
    let formatted = "";
    if (digits.length > 0) formatted += digits.substring(0, 1);
    if (digits.length > 1) formatted += "-" + digits.substring(1, Math.min(5, digits.length));
    if (digits.length > 5) formatted += "-" + digits.substring(5, 9);
    cedulaInput.value = formatted;
  });
})();

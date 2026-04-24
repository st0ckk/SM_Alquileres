(function () {
  const input = document.getElementById("cedula-input");
  const emailInput = document.getElementById("Email");
  const nombreInput = document.getElementById("Nombre");
  const apellidosInput = document.getElementById("Apellidos");
  const feedback = document.getElementById("cedula-feedback");
  const emailFeedback = document.getElementById("email-feedback");

  if (!input) {
    return;
  }

  input.addEventListener("input", function () {
    const digits = (this.value || "").replace(/\D/g, "").substring(0, 9);
    let formatted = digits;

    if (digits.length > 1) {
      formatted = digits.substring(0, 1) + "-" + digits.substring(1);
    }
    if (digits.length > 5) {
      formatted = formatted.substring(0, 6) + "-" + digits.substring(5);
    }

    this.value = formatted;

    if (formatted.length === 11) {
      fetch(`/Home/BuscarCedula?cedula=${encodeURIComponent(formatted)}`)
        .then((response) => (response.ok ? response.json() : null))
        .then((data) => {
          if (!data) {
            if (feedback) feedback.textContent = "";
            return;
          }
          if (nombreInput && data.nombre) {
            nombreInput.value = data.nombre;
          }
          if (apellidosInput && data.apellidos) {
            apellidosInput.value = data.apellidos;
          }
          if (feedback) {
            feedback.textContent = "Cédula ya existente.";
            feedback.style.color = "#b02a37";
          }
        })
        .catch(() => {
          if (feedback) feedback.textContent = "";
        });
    } else if (feedback) {
      feedback.textContent = "";
    }
  });

  if (emailInput) {
    emailInput.addEventListener("blur", function () {
      const correo = (emailInput.value || "").trim();
      if (!correo || !correo.includes("@")) {
        if (emailFeedback) emailFeedback.textContent = "";
        return;
      }

      fetch(`/Home/ValidarCorreo?email=${encodeURIComponent(correo)}`)
        .then((response) => (response.ok ? response.json() : null))
        .then((data) => {
          if (emailFeedback) {
            if (data?.exists === true) {
              emailFeedback.textContent = "Correo ya existente.";
              emailFeedback.style.color = "#b02a37";
            } else {
              emailFeedback.textContent = "";
            }
          }
        })
        .catch(() => {
          if (emailFeedback) emailFeedback.textContent = "";
        });
    });
  }
})();

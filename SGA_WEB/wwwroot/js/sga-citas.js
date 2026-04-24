(function () {
  const fechaEl = document.getElementById("sga-cita-fecha");
  const propEl = document.getElementById("sga-cita-propiedad");
  const idPropEl = document.getElementById("sga-cita-id-propiedad");
  const horaEl = document.getElementById("sga-cita-hora");
  if (!fechaEl || !propEl || !horaEl) {
    return;
  }

  function setMinFecha() {
    const hoy = new Date();
    const y = hoy.getFullYear();
    const m = String(hoy.getMonth() + 1).padStart(2, "0");
    const d = String(hoy.getDate()).padStart(2, "0");
    fechaEl.min = `${y}-${m}-${d}`;
  }

  function limpiarHoras(mensaje) {
    horaEl.innerHTML = "";
    const opt = document.createElement("option");
    opt.value = "";
    opt.textContent = mensaje;
    horaEl.appendChild(opt);
    horaEl.disabled = true;
  }

  async function cargarHoras() {
    const prop = (propEl.value || "").trim();
    const fecha = fechaEl.value;
    if (!prop || !fecha) {
      limpiarHoras("Seleccione fecha y propiedad");
      return;
    }

    const partes = fecha.split("-").map(Number);
    if (partes.length === 3) {
      const dt = new Date(partes[0], partes[1] - 1, partes[2]);
      if (dt.getDay() === 0) {
        limpiarHoras("Los domingos no hay visitas");
        return;
      }
    }

    horaEl.disabled = true;
    limpiarHoras("Cargando horarios...");

    try {
      const idProp = idPropEl && idPropEl.value ? String(idPropEl.value).trim() : "";
      const idQuery = idProp ? "&idPropiedad=" + encodeURIComponent(idProp) : "";
      const url =
        "/Home/HorariosCitaDisponibles?propiedad=" +
        encodeURIComponent(prop) +
        "&fecha=" +
        encodeURIComponent(fecha) +
        idQuery;
      const res = await fetch(url);
      if (!res.ok) {
        limpiarHoras("No se pudieron cargar los horarios");
        return;
      }
      const data = await res.json();
      const horas = data.horasDisponibles || [];
      horaEl.innerHTML = "";
      if (horas.length === 0) {
        limpiarHoras("No hay horarios disponibles (domingo no aplica)");
        return;
      }
      const placeholder = document.createElement("option");
      placeholder.value = "";
      placeholder.textContent = "Seleccione hora";
      horaEl.appendChild(placeholder);
      horas.forEach(function (h) {
        const o = document.createElement("option");
        o.value = String(h);
        const hh = String(h).padStart(2, "0");
        o.textContent = `${hh}:00`;
        horaEl.appendChild(o);
      });
      horaEl.disabled = false;
    } catch {
      limpiarHoras("No se pudieron cargar los horarios");
    }
  }

  fechaEl.addEventListener("change", cargarHoras);
  propEl.addEventListener("change", cargarHoras);

  document.addEventListener("DOMContentLoaded", function () {
    setMinFecha();
    if (fechaEl.value) {
      cargarHoras();
    } else {
      limpiarHoras("Seleccione fecha y propiedad");
    }
  });
})();

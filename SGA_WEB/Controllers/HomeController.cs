using Microsoft.AspNetCore.Mvc;
using SGA.Infrastructure.Services;
using SGA_WEB.Models;
using System.Text.RegularExpressions;

namespace SGA_WEB.Controllers
{
    public class HomeController : Controller
    {
        private readonly ISgaDataService _sgaDataService;
        private readonly IEmailNotificationService _emailNotificationService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            ISgaDataService sgaDataService,
            IEmailNotificationService emailNotificationService,
            ILogger<HomeController> logger)
        {
            _sgaDataService = sgaDataService;
            _emailNotificationService = emailNotificationService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? pagina)
        {
            const int tamano = 10;
            var p = pagina ?? 1;
            if (p < 1)
            {
                p = 1;
            }

            var (items, total) = await _sgaDataService.GetPropiedadesActivasPaginadoAsync(p, tamano);
            var totalPaginas = tamano <= 0 ? 0 : (int)Math.Ceiling(total / (double)tamano);
            if (totalPaginas > 0 && p > totalPaginas)
            {
                p = totalPaginas;
                (items, total) = await _sgaDataService.GetPropiedadesActivasPaginadoAsync(p, tamano);
            }

            int? idPropiedadCita = null;
            if (int.TryParse(Request.Query["idPropiedad"], out var idp) && idp > 0)
            {
                idPropiedadCita = idp;
            }

            var vm = new HomeIndexViewModel
            {
                Propiedades = items,
                PropiedadSeleccionadaQuery = Request.Query["propiedad"].ToString(),
                IdPropiedadParaCita = idPropiedadCita,
                PaginaActual = p,
                TotalRegistros = total,
                TamanoPagina = tamano
            };
            return View(vm);
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString("UsuarioEmail")))
            {
                return RedirectToAction("Index");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(Usuario modelo)
        {
            if (string.IsNullOrWhiteSpace(modelo.Email) || string.IsNullOrWhiteSpace(modelo.Contrasenna))
            {
                ViewBag.Error = "Debe completar los campos requeridos.";
                return View(modelo);
            }

            try
            {
                var email = modelo.Email.Trim().ToLowerInvariant();
                var loginResult = await _sgaDataService.ValidateCredentialsAsync(email, modelo.Contrasenna);
                if (loginResult is null)
                {
                    ViewBag.Error = "Email o contraseña incorrectos";
                    return View(modelo);
                }

                HttpContext.Session.SetString("UsuarioEmail", loginResult.Email);
                HttpContext.Session.SetString("UsuarioRol", loginResult.Rol);
                HttpContext.Session.SetString("UsuarioNombre", loginResult.NombreCompleto);
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al iniciar sesion para {Email}.", modelo.Email);
                ViewBag.Error = "Email o contraseña incorrectos";
                return View(modelo);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult CambiarContrasenna()
        {
            if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("UsuarioEmail")))
            {
                return RedirectToAction("Login");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarContrasenna(CambiarContrasennaInputModel model)
        {
            var email = HttpContext.Session.GetString("UsuarioEmail");
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var isValidCurrentPassword = await _sgaDataService.ValidateCredentialsAsync(email, model.ContrasennaActual);
            if (isValidCurrentPassword is null)
            {
                ViewBag.Error = "La contraseña actual no es correcta.";
                return View(model);
            }

            await _sgaDataService.UpdatePasswordByEmailAsync(email, model.NuevaContrasenna);
            TempData["Success"] = "Contraseña actualizada correctamente.";
            return RedirectToAction(nameof(CambiarContrasenna));
        }

        [HttpGet]
        public IActionResult RecuperarContrasenna()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecuperarContrasenna(RecuperarContrasennaInputModel model)
        {
            model.Cedula = NormalizeCedula(model.Cedula);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email.Trim().ToLowerInvariant();
            var ok = await _sgaDataService.ValidateClientRecoveryAsync(email, model.Cedula);
            if (!ok)
            {
                ViewBag.Error = "No coincide el correo con la cédula registrada.";
                return View(model);
            }

            await _sgaDataService.UpdatePasswordByEmailAsync(email, model.NuevaContrasenna);
            ViewBag.Success = "Contraseña restablecida correctamente. Ya puede iniciar sesión.";
            ModelState.Clear();
            return View();
        }

        [HttpGet]
        public IActionResult Registro()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> BuscarCedula(string cedula)
        {
            if (string.IsNullOrWhiteSpace(cedula))
            {
                return BadRequest();
            }

            var dato = await _sgaDataService.GetClientByCedulaAsync(cedula);
            if (dato is null)
            {
                return NotFound();
            }

            var partes = (dato.NombreCompleto ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var nombre = partes.Length > 0 ? partes[0] : string.Empty;
            var apellidos = partes.Length > 1 ? string.Join(" ", partes.Skip(1)) : string.Empty;

            return Json(new
            {
                dato.Cedula,
                Nombre = nombre,
                Apellidos = apellidos,
                dato.Telefono,
                dato.Email
            });
        }

        [HttpGet]
        public async Task<IActionResult> ValidarCorreo(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest();
            }

            var exists = await _sgaDataService.EmailExistsAsync(email.Trim().ToLowerInvariant());
            return Json(new { exists });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registro(Usuario modelo)
        {
            const string cedulaPattern = @"^\d-\d{4}-\d{4}$";
            var cedulaNormalizada = NormalizeCedula(modelo.Identificacion);
            if (string.IsNullOrWhiteSpace(modelo.Nombre) ||
                string.IsNullOrWhiteSpace(modelo.Apellidos) ||
                string.IsNullOrWhiteSpace(cedulaNormalizada) ||
                string.IsNullOrWhiteSpace(modelo.Telefono) ||
                string.IsNullOrWhiteSpace(modelo.Email) ||
                string.IsNullOrWhiteSpace(modelo.Contrasenna))
            {
                ViewBag.Error = "Todos los campos son obligatorios: nombre, apellidos, cédula, teléfono, correo y contraseña.";
                return View(modelo);
            }

            if (!Regex.IsMatch(cedulaNormalizada, cedulaPattern))
            {
                ViewBag.Error = "La cédula debe tener el formato x-xxxx-xxxx.";
                return View(modelo);
            }

            var correoNormalizado = modelo.Email.Trim().ToLowerInvariant();
            if (await _sgaDataService.CedulaExistsAsync(cedulaNormalizada))
            {
                ViewBag.Error = "Cédula ya existente.";
                return View(modelo);
            }

            if (await _sgaDataService.EmailExistsAsync(correoNormalizado))
            {
                ViewBag.Error = "Correo ya existente.";
                return View(modelo);
            }

            try
            {
                var fullName = $"{modelo.Nombre} {modelo.Apellidos}".Trim();
                var ok = await _sgaDataService.RegisterClientUserAsync(
                    fullName,
                    cedulaNormalizada,
                    modelo.Telefono.Trim(),
                    correoNormalizado,
                    modelo.Contrasenna);
                if (!ok)
                {
                    ViewBag.Error = "No se pudo registrar: valide cédula (única y formato) y correo (único).";
                    return View(modelo);
                }

                TempData["Success"] = "Registro exitoso. Ya puede iniciar sesión con sus credenciales.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar cliente con correo {Email}.", modelo.Email);
                ViewBag.Error = "No se pudo completar el registro.";
                return View(modelo);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarMensaje(ContactoInputModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Complete correctamente el formulario de contacto.";
                return Redirect("/Home/Index#contacto");
            }

            try
            {
                await _sgaDataService.SaveContactMessageAsync(model.NombreCompleto, model.Email.Trim().ToLowerInvariant(), model.Asunto, model.Mensaje);
                TempData["ContactSuccess"] = "Mensaje enviado correctamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar mensaje de contacto para {Email}.", model.Email);
                TempData["ContactError"] = "No se pudo guardar el mensaje. Intente nuevamente.";
            }

            return Redirect("/Home/Index#contacto");
        }

        [HttpGet]
        public async Task<IActionResult> HorariosCitaDisponibles(string propiedad, DateTime fecha, int? idPropiedad = null)
        {
            if (string.IsNullOrWhiteSpace(propiedad))
            {
                return Json(new { horasDisponibles = Array.Empty<int>() });
            }

            var idProp = idPropiedad is > 0 ? idPropiedad : null;

            var fechaDia = fecha.Date;
            if (fechaDia < DateTime.Today || !EsDiaPermitidoCita(fechaDia))
            {
                return Json(new { horasDisponibles = Array.Empty<int>() });
            }

            var ocupadas = await _sgaDataService.GetHorasOcupadasCitaAsync(propiedad.Trim(), fechaDia, idProp);
            var setOcup = ocupadas.ToHashSet();
            var todas = Enumerable.Range(8, 10);
            var disponibles = todas.Where(h => !setOcup.Contains(h)).ToList();

            if (fechaDia == DateTime.Today)
            {
                disponibles = disponibles
                    .Where(h => new DateTime(fechaDia.Year, fechaDia.Month, fechaDia.Day, h, 0, 0, DateTimeKind.Local) > DateTime.Now)
                    .ToList();
            }

            return Json(new { horasDisponibles = disponibles });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgendarCita(CitaInputModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["CitaError"] = "Complete correctamente el formulario de cita.";
                return Redirect("/Home/Index#agendar-cita");
            }

            var propiedad = model.PropiedadInteres.Trim();
            var fechaVisita = DateTime.SpecifyKind(
                model.FechaCita.Date.AddHours(model.HoraCita),
                DateTimeKind.Unspecified);

            if (!EsDiaPermitidoCita(fechaVisita) || !EsHoraPermitidaCita(model.HoraCita))
            {
                TempData["CitaError"] = "Las visitas solo se agendan de lunes a sábado, entre las 8:00 y las 17:00.";
                return Redirect("/Home/Index#agendar-cita");
            }

            if (fechaVisita.Date < DateTime.Today || fechaVisita <= DateTime.Now)
            {
                TempData["CitaError"] = "Seleccione una fecha y hora futuras.";
                return Redirect("/Home/Index#agendar-cita");
            }

            try
            {
                var idPropCita = model.IdPropiedadCita is > 0 ? model.IdPropiedadCita : null;
                var ok = await _sgaDataService.ScheduleAppointmentAsync(
                    model.NombreCompleto,
                    model.Email.Trim().ToLowerInvariant(),
                    model.Telefono,
                    fechaVisita,
                    propiedad,
                    model.Mensaje,
                    idPropCita);

                if (!ok)
                {
                    TempData["CitaError"] = "Ese horario ya está reservado para esta propiedad. Elija otro horario disponible.";
                    return Redirect("/Home/Index#agendar-cita");
                }

                try
                {
                    await _emailNotificationService.SendAppointmentEmailAsync(
                        model.Email.Trim().ToLowerInvariant(),
                        model.NombreCompleto,
                        fechaVisita,
                        propiedad);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "No se pudo enviar correo de cita a {Email}.", model.Email);
                    TempData["CitaWarning"] = "La cita se registró, pero no se pudo enviar el correo de confirmación.";
                }

                TempData["CitaSuccess"] = "Solicitud enviada correctamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agendar cita para {Email}.", model.Email);
                TempData["CitaError"] = "No se pudo enviar la solicitud de cita. Intente nuevamente.";
            }

            return Redirect("/Home/Index#agendar-cita");
        }

        private static bool EsDiaPermitidoCita(DateTime fecha) =>
            fecha.DayOfWeek is not DayOfWeek.Sunday;

        private static bool EsHoraPermitidaCita(int hora) =>
            hora is >= 8 and <= 17;

        private static string NormalizeCedula(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var digits = new string(input.Where(char.IsDigit).ToArray());
            if (digits.Length == 9)
            {
                return $"{digits[0]}-{digits.Substring(1, 4)}-{digits.Substring(5, 4)}";
            }

            return input.Trim();
        }
    }
}

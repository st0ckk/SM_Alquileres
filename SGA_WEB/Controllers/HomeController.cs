using Microsoft.AspNetCore.Mvc;
using SGA.Infrastructure.Services;
using SGA_WEB.Models;
using System.Text.RegularExpressions;

namespace SGA_WEB.Controllers
{
    public class HomeController : Controller
    {
        private readonly ISgaDataService _sgaDataService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ISgaDataService sgaDataService, ILogger<HomeController> logger)
        {
            _sgaDataService = sgaDataService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
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
                TempData["Success"] = "Sesion iniciada correctamente.";
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
            TempData["Success"] = "Sesion cerrada correctamente.";
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
            return RedirectToAction("Index");
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

            try
            {
                var fullName = $"{modelo.Nombre} {modelo.Apellidos}".Trim();
                var ok = await _sgaDataService.RegisterClientUserAsync(
                    fullName,
                    cedulaNormalizada,
                    modelo.Telefono.Trim(),
                    modelo.Email.Trim().ToLowerInvariant(),
                    modelo.Contrasenna);
                if (!ok)
                {
                    ViewBag.Error = "No se pudo registrar: valide cédula (única y formato) y correo (único).";
                    return View(modelo);
                }

                ViewBag.Success = "¡Registro exitoso! Ya puede iniciar sesión con sus credenciales.";
                return View();
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgendarCita(CitaInputModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Complete correctamente el formulario de cita.";
                return Redirect("/Home/Index#agendar-cita");
            }

            try
            {
                await _sgaDataService.ScheduleAppointmentAsync(
                    model.NombreCompleto,
                    model.Email.Trim().ToLowerInvariant(),
                    model.Telefono,
                    model.FechaVisita,
                    model.PropiedadInteres,
                    model.Mensaje);

                TempData["CitaSuccess"] = "Solicitud enviada correctamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agendar cita para {Email}.", model.Email);
                TempData["CitaError"] = "No se pudo enviar la solicitud de cita. Intente nuevamente.";
            }

            return Redirect("/Home/Index#agendar-cita");
        }

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

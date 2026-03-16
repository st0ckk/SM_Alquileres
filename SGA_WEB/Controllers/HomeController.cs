using Microsoft.AspNetCore.Mvc;
using SGA_WEB.Models;

namespace SGA_WEB.Controllers
{
    public class HomeController : Controller
    {
        //Registro, Recuperar Contraseña

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(Usuario modelo)
        {
            // Validar credenciales
            if (modelo.Email == "admin@gmail.com" && modelo.Contrasenna == "123456")
            {
                // Login exitoso - redirigir al Index
                return RedirectToAction("Index", "Home");
            }
            else
            {
                // Credenciales incorrectas
                ViewBag.Error = "Email o contraseña incorrectos";
                return View(modelo);
            }
        }

        [HttpGet]
        public IActionResult Registro()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Registro(Usuario modelo)
        {
            // Validar que el modelo sea válido
            if (ModelState.IsValid)
            {
                // Aquí se implementará la lógica de registro (guardar en base de datos)
                // Por ahora solo mostramos mensaje de éxito
                ViewBag.Success = "¡Registro exitoso! Ya puede iniciar sesión con sus credenciales.";
                return View();
            }
            else
            {
                ViewBag.Error = "Por favor, complete todos los campos correctamente.";
                return View(modelo);
            }
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using SGA.Infrastructure.Services;
using SGA_WEB.Models;

namespace SGA_WEB.Controllers;

public class ClienteController : Controller
{
    private readonly ISgaDataService _sgaDataService;

    public ClienteController(ISgaDataService sgaDataService)
    {
        _sgaDataService = sgaDataService;
    }

    [HttpGet]
    public async Task<IActionResult> Notificaciones()
    {
        var usuarioEmail = HttpContext.Session.GetString("UsuarioEmail");
        if (string.IsNullOrWhiteSpace(usuarioEmail))
        {
            TempData["Error"] = "Debe iniciar sesion para ver sus notificaciones.";
            return RedirectToAction("Login", "Home");
        }

        var rol = HttpContext.Session.GetString("UsuarioRol");
        if (string.Equals(rol, "Administrador", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Esta vista es para clientes.";
            return RedirectToAction("Index", "Admin");
        }

        var model = new ClienteNotificacionesViewModel
        {
            Citas = await _sgaDataService.GetCitasByClientEmailAsync(usuarioEmail)
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Mensajes()
    {
        var usuarioEmail = HttpContext.Session.GetString("UsuarioEmail");
        if (string.IsNullOrWhiteSpace(usuarioEmail))
        {
            TempData["Error"] = "Debe iniciar sesion para enviar mensajes.";
            return RedirectToAction("Login", "Home");
        }

        var rol = HttpContext.Session.GetString("UsuarioRol");
        if (string.Equals(rol, "Administrador", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Esta vista es para clientes.";
            return RedirectToAction("Index", "Admin");
        }

        return View(new ClienteMensajeInputModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarMensaje(ClienteMensajeInputModel model)
    {
        var usuarioEmail = HttpContext.Session.GetString("UsuarioEmail");
        if (string.IsNullOrWhiteSpace(usuarioEmail))
        {
            TempData["Error"] = "Debe iniciar sesion para enviar mensajes.";
            return RedirectToAction("Login", "Home");
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Complete asunto y mensaje para enviar.";
            return RedirectToAction("Mensajes");
        }

        await _sgaDataService.SaveClientMessageAsync(usuarioEmail.Trim().ToLowerInvariant(), model.Asunto, model.Mensaje);
        TempData["Success"] = "Mensaje enviado correctamente.";
        return RedirectToAction("Mensajes");
    }
}

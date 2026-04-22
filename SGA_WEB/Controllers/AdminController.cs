using Microsoft.AspNetCore.Mvc;
using SGA.Infrastructure.Services;
using SGA_WEB.Models;

namespace SGA_WEB.Controllers;

public class AdminController : Controller
{
    private readonly ISgaDataService _sgaDataService;

    public AdminController(ISgaDataService sgaDataService)
    {
        _sgaDataService = sgaDataService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? fechaDesde, DateTime? fechaHasta, string? estado, string? tab = null)
    {
        var role = HttpContext.Session.GetString("UsuarioRol");
        if (!string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "No tiene permisos para acceder a esta seccion.";
            return RedirectToAction("Index", "Home");
        }

        var vm = new AdminDashboardViewModel
        {
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta,
            Estado = estado ?? string.Empty,
            Citas = await _sgaDataService.GetCitasAsync(fechaDesde, fechaHasta, estado),
            Clientes = await _sgaDataService.GetClientesAsync(),
            Mensajes = await _sgaDataService.GetMensajesContactoAsync()
        };

        ViewBag.ActiveTab = string.Equals(tab, "clientes", StringComparison.OrdinalIgnoreCase)
            ? "clientes"
            : string.Equals(tab, "mensajes", StringComparison.OrdinalIgnoreCase)
                ? "mensajes"
                : "citas";
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCitaEstado(int idCita, string estado)
    {
        var role = HttpContext.Session.GetString("UsuarioRol");
        if (!string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "No tiene permisos para actualizar citas.";
            return RedirectToAction("Index", new { tab = "citas" });
        }

        var estadoNormalizado = (estado ?? string.Empty).Trim().ToUpperInvariant();
        if (estadoNormalizado is not ("PENDIENTE" or "APROBADA" or "CANCELADA"))
        {
            TempData["Error"] = "Estado de cita no valido.";
            return RedirectToAction("Index", new { tab = "citas" });
        }

        await _sgaDataService.UpdateCitaStatusAsync(idCita, estadoNormalizado);
        TempData["Success"] = "Estado de cita actualizado.";
        return RedirectToAction("Index", new { tab = "citas" });
    }
}
